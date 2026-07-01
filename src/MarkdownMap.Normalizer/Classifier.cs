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
        "residential", "terrace", "bungalow", "dormitory", "static_caravan",
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

        // Chain penalty (ADR-0018): a `brand`-tagged Feature is a chain, so it loses the promotion
        // budget to independents. The penalty exceeds the name bonus, netting a chain below an
        // equivalent independent while still keeping it above the clustered floor.
        bool isChain = tags.ContainsKey("brand") || tags.ContainsKey("brand:wikidata");
        int score = BaseScore(klass, subclass)
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

        if (amenity is not null && CivicAmenity.Contains(amenity)) return ("civic", amenity);
        if (tags.TryGetValue("healthcare", out var hc)) return ("civic", hc);

        if (amenity is not null && FoodAmenity.Contains(amenity)) return ("food", amenity);

        var shop = Tag("shop");
        if (shop == "deli") return ("food", "deli");
        if (shop is not null) return ("shop", shop);

        var leisure = Tag("leisure");
        if (leisure is not null && Leisure.Contains(leisure)) return ("leisure", leisure);

        if (tourism is not null && LodgingTourism.Contains(tourism)) return ("lodging", tourism);

        // Named residential structures only — unnamed buildings are out of scope (clustered later).
        if (building is not null && hasName && ResidentialBuilding.Contains(building))
            return ("residential", building);

        return (null, null);
    }

    // Private civic practices (dentist, clinic, …) are budgeted, not institutional, so they score
    // like commodity destinations rather than landmarks (ADR-0018).
    private static readonly HashSet<string> PrivateCivic = new(StringComparer.Ordinal)
    { "dentist", "clinic", "pharmacy", "doctors", "veterinary" };

    private static int BaseScore(string klass, string subclass) => klass switch
    {
        "civic" => PrivateCivic.Contains(subclass) ? 60 : 80,
        "landmark" => 80,
        "food" or "shop" or "leisure" or "lodging" => 55,
        _ => 30, // residential
    };

    private static string TierOf(int score) =>
        score >= 75 ? "landmark"
        : score >= 50 ? "destination"
        : score >= 25 ? "minor"
        : "structure";
}
