using System;
using System.Collections.Generic;
using MarkdownMap.Contract;

namespace MarkdownMap.Normalizer;

/// <summary>Result of classifying an OSM element as a poi. Null = not a poi (gated out).</summary>
public sealed record Classification(string Category, int Importance, string Tier, string Salience);

/// <summary>
/// Inclusion gate + taxonomy + importance scoring for step 1 (poi only).
/// Implements docs/feature-schema.md sections 3 (gate), 4 (taxonomy), 5 (importance).
/// Pure and OSM-library-agnostic: takes a plain tag map so it is trivially unit-tested.
/// </summary>
public static class Classifier
{
    // section 3 — RP noise dropped entirely (never becomes a Feature).
    private static readonly HashSet<string> DropAmenity = new(StringComparer.Ordinal)
    {
        "parking", "parking_space", "parking_entrance", "bench", "bicycle_parking",
        "waste_basket", "post_box", "drinking_water", "recycling", "toilets",
        "vending_machine", "bicycle_repair_station", "fountain", "clock", "hunting_stand",
    };
    private static readonly HashSet<string> DropNatural = new(StringComparer.Ordinal)
    {
        "tree", "tree_row", "scrub", "wood", "stone", "shrub", "grassland",
    };

    private static readonly HashSet<string> FoodAmenity = new(StringComparer.Ordinal)
    {
        "restaurant", "cafe", "bar", "pub", "fast_food", "biergarten", "food_court", "ice_cream",
    };
    private static readonly HashSet<string> CivicAmenity = new(StringComparer.Ordinal)
    {
        "school", "library", "post_office", "townhall", "community_centre", "college",
        "university", "kindergarten", "police", "fire_station", "courthouse",
        "dentist", "clinic", "pharmacy", "hospital", "doctors", "veterinary", "social_facility",
    };
    private static readonly HashSet<string> Leisure = new(StringComparer.Ordinal)
    {
        "marina", "fitness_centre", "sports_centre", "swimming_pool", "playground", "pitch",
        "slipway", "track", "dog_park", "garden", "golf_course", "stadium", "ice_rink",
    };
    // Non-food entertainment/recreation amenities → leisure venues (budgeted destinations).
    private static readonly HashSet<string> LeisureAmenity = new(StringComparer.Ordinal)
    {
        "theatre", "cinema", "arts_centre", "events_venue", "nightclub", "boat_rental",
        "spa", "makerspace", "studio", "coworking_space",
    };
    // Commercial-service amenities → shop (budgeted, chain-penalised like any shop).
    private static readonly HashSet<string> ShopAmenity = new(StringComparer.Ordinal)
    {
        "bank", "fuel", "marketplace", "car_wash", "car_rental",
    };
    private static readonly HashSet<string> LodgingTourism = new(StringComparer.Ordinal)
    {
        "hotel", "hostel", "guest_house", "motel", "chalet", "apartment",
    };
    private static readonly HashSet<string> LandmarkTourism = new(StringComparer.Ordinal)
    {
        "artwork", "viewpoint", "attraction", "museum", "gallery", "monument",
    };
    private static readonly HashSet<string> WorshipBuilding = new(StringComparer.Ordinal)
    {
        "church", "cathedral", "chapel", "mosque", "synagogue", "temple",
    };
    private static readonly HashSet<string> ResidentialBuilding = new(StringComparer.Ordinal)
    {
        "house", "apartments", "houseboat", "floating_home", "detached", "semidetached_house",
        "residential", "terrace", "bungalow", "static_caravan",
    };
    // Named institutional buildings that are singular civic landmarks → civic core (ADR-0019).
    // `university`/`college` are handled separately (many halls per campus → budgeted venue-band);
    // `stadium` → leisure core; `train_station` → the station path; `dormitory` → lodging core.
    private static readonly HashSet<string> InstitutionCoreBuilding = new(StringComparer.Ordinal)
    {
        "school", "hospital", "civic", "government", "public", "fire_station",
    };
    // Scene landmarks among man_made (the rest — poles, masts, manholes — are infrastructure noise).
    // A pier is often a LineString; RepresentativePoint reduces it to a point like any way.
    private static readonly HashSet<string> ManMadeLandmark = new(StringComparer.Ordinal)
    {
        "pier", "tower", "lighthouse", "water_tower", "bridge", "obelisk", "windmill", "communications_tower",
    };
    // Civic-scale offices worth a token; generic office=company/lawyer/insurance is dropped.
    private static readonly HashSet<string> OfficeCivic = new(StringComparer.Ordinal)
    {
        "government", "educational_institution", "research", "research_institute", "diplomatic",
    };

    public static Classification? Classify(IReadOnlyDictionary<string, string> tags)
    {
        // A name resolved from name/brand/operator (ADR-0012) counts as named for scoring.
        bool hasName = NameResolver.Resolve(tags) is not null;
        string? Tag(string k) => tags.TryGetValue(k, out var v) ? v : null;

        var amenity = Tag("amenity");
        var natural = Tag("natural");

        // section 3 — inclusion gate.
        if (amenity is not null && DropAmenity.Contains(amenity)) return null;
        if (natural is not null && DropNatural.Contains(natural)) return null;

        var (klass, subclass) = ClassifyKind(tags, amenity, hasName);
        if (klass is null || subclass is null) return null;

        string category = $"{klass}.{subclass}";
        string salience = SalienceClassifier.Of(category);

        // Base score by (class, salience). Budgeted commemoratives rank below venues; budgeted civic
        // (private practice / office) sits just above commodity. A pier is core (a moorage is the
        // waterfront's address), so it lands in the default 80 below (ADR-0018).
        int baseScore = (klass, budgeted: salience == SalienceClassifier.Budgeted) switch
        {
            ("landmark", true) => 45,
            // Private practices (pharmacy/clinic) sit above commodity; civic OFFICES rank below
            // venues (a dept/gov office is less scene-worthy than a place you enter) — base 45.
            // Offices rank below venues (45); a campus HALL sits at the venue band (55 → 65 with a
            // name, then an area nudge in the Normalizer); private practices sit above commodity (60).
            ("civic", true) => OfficeCivic.Contains(subclass) ? 45
                                : SalienceClassifier.IsAreaRankedBuilding(category) ? 55 : 60,
            _ => BaseScore(klass, subclass),
        };

        // Chain penalty (ADR-0018): a `brand`-tagged Feature is a chain, so it loses the promotion
        // budget to independents. The penalty exceeds the name bonus, netting a chain below an
        // equivalent independent while still keeping it above the clustered floor.
        bool isChain = tags.ContainsKey("brand") || tags.ContainsKey("brand:wikidata");
        int score = baseScore
                    + (hasName ? 10 : 0)
                    + (klass == "landmark" ? 5 : 0)
                    - (isChain ? 15 : 0);
        score = Math.Max(0, Math.Min(100, score));

        return new Classification(category, score, TierOf(score), salience);
    }

    private static (string? klass, string? subclass) ClassifyKind(
        IReadOnlyDictionary<string, string> tags, string? amenity, bool hasName)
    {
        string? Tag(string k) => tags.TryGetValue(k, out var v) ? v : null;

        var tourism = Tag("tourism");
        if (tourism is not null && LandmarkTourism.Contains(tourism)) return ("landmark", tourism);
        if (amenity == "place_of_worship") return ("landmark", "place_of_worship");
        if (tags.ContainsKey("historic")) return ("landmark", Tag("historic") ?? "site");

        var building = Tag("building");
        if (building is not null && WorshipBuilding.Contains(building)) return ("landmark", building);

        var manMade = Tag("man_made");
        if (manMade is not null && ManMadeLandmark.Contains(manMade)) return ("landmark", manMade);

        if (amenity is not null && CivicAmenity.Contains(amenity)) return ("civic", amenity);
        if (tags.TryGetValue("healthcare", out var hc)) return ("civic", hc);
        var office = Tag("office");
        if (office is not null && OfficeCivic.Contains(office)) return ("civic", office);

        // A named rail/transit station is an arrival landmark (core), distinct from deferred bus
        // stops/routes (ADR-0019). The rail LINE stays a barrier; this is the station node/building.
        if ((Tag("railway") == "station" || Tag("public_transport") == "station") && hasName)
            return ("civic", "station");

        if (amenity is not null && FoodAmenity.Contains(amenity)) return ("food", amenity);
        if (amenity is not null && LeisureAmenity.Contains(amenity)) return ("leisure", amenity);
        if (amenity is not null && ShopAmenity.Contains(amenity)) return ("shop", amenity);

        var shop = Tag("shop");
        if (shop == "deli") return ("food", "deli");
        if (shop is not null) return ("shop", shop);
        var craft = Tag("craft");
        if (craft is not null) return ("shop", craft); // small producers/services, like a shop

        var leisure = Tag("leisure");
        if (leisure is not null && Leisure.Contains(leisure)) return ("leisure", leisure);

        if (amenity == "student_accommodation") return ("lodging", "student_accommodation");
        if (tourism is not null && LodgingTourism.Contains(tourism)) return ("lodging", tourism);

        // Named institutional buildings (ADR-0019). Placed after functional amenity/shop tags so a
        // cafe inside a `building=university` classifies as the cafe. Named-only: an unnamed hall is
        // a weak anchor and clusters as a generic building (as before), same gate as residential.
        if (building is not null && hasName)
        {
            if (InstitutionCoreBuilding.Contains(building)) return ("civic", building);
            if (building == "stadium") return ("leisure", "stadium");
            if (building == "train_station") return ("civic", "station");
            // A campus's many halls: `_building` suffix keeps them budgeted (see SalienceClassifier),
            // distinct from the institution itself (amenity=university → civic.university, core).
            if (building is "university" or "college") return ("civic", building + "_building");
            if (building == "dormitory") return ("lodging", "student_accommodation"); // dorm-as-address, core
        }

        // Named residential structures only — unnamed buildings are out of scope (clustered later).
        if (building is not null && hasName && ResidentialBuilding.Contains(building))
            return ("residential", building);

        return (null, null);
    }

    // Base score for the non-budgeted (core) cases; budgeted landmark/civic bases are set in Classify.
    private static int BaseScore(string klass, string subclass) => klass switch
    {
        "landmark" or "civic" => 80, // core institutions / destinations
        "food" or "shop" or "leisure" or "lodging" => 55,
        _ => 30, // residential
    };

    private static string TierOf(int score) =>
        score >= 75 ? "landmark"
        : score >= 50 ? "destination"
        : score >= 25 ? "minor"
        : "structure";
}
