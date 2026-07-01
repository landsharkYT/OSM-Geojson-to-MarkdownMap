using System.Collections.Generic;
using MarkdownMap.Contract;
using MarkdownMap.Normalizer;
using Xunit;

namespace MarkdownMap.Normalizer.Tests;

/// <summary>ADR-0018: parse-time salience + the chain importance penalty.</summary>
public class SalienceTests
{
    private static Classification Classify(params (string k, string v)[] tags)
    {
        var d = new Dictionary<string, string>();
        foreach (var (k, v) in tags) d[k] = v;
        return Classifier.Classify(d)!;
    }

    private static Classification? TryClassify(params (string k, string v)[] tags)
    {
        var d = new Dictionary<string, string>();
        foreach (var (k, v) in tags) d[k] = v;
        return Classifier.Classify(d);
    }

    [Fact]
    public void Man_made_pier_is_core_others_are_set_dressing()
    {
        // A named moorage is the waterfront's address → core (always promotes, bypasses the budget).
        var pier = Classify(("man_made", "pier"), ("name", "Roanoke Reef"));
        Assert.Equal("landmark.pier", pier.Category);
        Assert.Equal("core", pier.Salience);
        Assert.Equal(95, pier.Importance); // 80 base + 10 name + 5 landmark

        // Other man_made are walk-past set-dressing → budgeted, below venues.
        var lighthouse = Classify(("man_made", "lighthouse"), ("name", "Alki Point"));
        Assert.Equal("budgeted", lighthouse.Salience);
        Assert.Equal(60, lighthouse.Importance); // 45 base + 10 + 5

        // Infrastructure man_made is not a Feature at all.
        Assert.Null(TryClassify(("man_made", "utility_pole")));
        Assert.Null(TryClassify(("man_made", "surveillance")));
    }

    [Fact]
    public void Dorms_are_core_and_civic_offices_rank_below_venues()
    {
        // A dorm is a residential address (like a moorage) → core, always promotes.
        var dorm = Classify(("amenity", "student_accommodation"), ("name", "Alder Hall"));
        Assert.Equal("lodging.student_accommodation", dorm.Category);
        Assert.Equal("core", dorm.Salience);

        // A civic office competes but ranks BELOW venues (base 45), so it doesn't displace cafés.
        var gov = Classify(("office", "government"), ("name", "County Office"));
        Assert.Equal("civic.government", gov.Category);
        Assert.Equal("budgeted", gov.Salience);
        Assert.Equal(55, gov.Importance); // 45 base + 10 name
        var cafe = Classify(("amenity", "cafe"), ("name", "Indie"));
        Assert.True(gov.Importance < cafe.Importance, "an office ranks below a café");

        Assert.Null(TryClassify(("office", "company"), ("name", "Generic LLC"))); // generic office dropped
    }

    [Fact]
    public void Civic_splits_into_institution_core_and_private_budgeted()
    {
        var school = Classify(("amenity", "school"), ("name", "Rivertown Elementary"));
        var dentist = Classify(("amenity", "dentist"), ("name", "Bright Smiles"));

        Assert.Equal("core", school.Salience);
        Assert.Equal("budgeted", dentist.Salience);
        // A private practice no longer ranks like an institution.
        Assert.True(school.Importance > dentist.Importance, $"{school.Importance} !> {dentist.Importance}");
    }

    [Fact]
    public void A_chain_scores_below_an_equivalent_independent()
    {
        var indie = Classify(("amenity", "cafe"), ("name", "Bluebird Coffee"));
        var chain = Classify(("amenity", "cafe"), ("name", "Chainbucks"), ("brand", "Chainbucks"));

        Assert.True(chain.Importance < indie.Importance, $"chain {chain.Importance} !< indie {indie.Importance}");
        // Both are still budgeted commodities; the chain just loses the budget first.
        Assert.Equal("budgeted", chain.Salience);
        Assert.Equal("budgeted", indie.Salience);
    }

    [Theory]
    [InlineData("theatre", "leisure.theatre")]   // entertainment venue → leisure
    [InlineData("cinema", "leisure.cinema")]
    [InlineData("boat_rental", "leisure.boat_rental")]
    [InlineData("bank", "shop.bank")]            // commercial service → shop
    [InlineData("fuel", "shop.fuel")]
    public void Non_food_destination_amenities_are_budgeted_venues(string amenity, string category)
    {
        var c = Classify(("amenity", amenity), ("name", "Sample"));
        Assert.Equal(category, c.Category);
        Assert.Equal("budgeted", c.Salience);
        Assert.Equal(65, c.Importance); // 55 base + 10 name
    }

    [Fact]
    public void Craft_is_a_shop_and_atm_stays_dropped()
    {
        Assert.Equal("shop.brewery", Classify(("craft", "brewery"), ("name", "Ballast")).Category);
        Assert.Null(TryClassify(("amenity", "atm")));            // a machine, not a place
        Assert.Null(TryClassify(("amenity", "charging_station")));
    }

    [Fact]
    public void Worship_and_artwork_land_on_opposite_sides_of_the_core_line()
    {
        Assert.Equal("core", Classify(("amenity", "place_of_worship"), ("name", "St. Anne's")).Salience);
        Assert.Equal("budgeted", Classify(("tourism", "artwork"), ("name", "The Mural")).Salience);
    }

    [Fact]
    public void A_campus_hall_is_a_budgeted_venue_band_destination_not_core()
    {
        // A named lecture hall (building=university) is one of many per campus → it must COMPETE for the
        // promotion budget, not flood it as core (ADR-0019). It sits at the venue band (55 → 65).
        var hall = Classify(("building", "university"), ("name", "Maple Hall"));
        Assert.Equal("civic.university_building", hall.Category);
        Assert.Equal("budgeted", hall.Salience);
        Assert.Equal(65, hall.Importance); // 55 base + 10 name (area nudge is applied later, in the Normalizer)
        Assert.True(SalienceClassifier.IsAreaRankedBuilding(hall.Category));

        // A café competes on equal footing — same band — so a campus promotes a realistic MIX.
        var cafe = Classify(("amenity", "cafe"), ("name", "The Grind"));
        Assert.Equal(hall.Importance, cafe.Importance);
    }

    [Fact]
    public void Singular_institution_buildings_are_core_but_a_hall_is_not()
    {
        // building=hospital/school/civic/... are singular civic landmarks → core (few, major).
        var hospital = Classify(("building", "hospital"), ("name", "General Hospital"));
        Assert.Equal("civic.hospital", hospital.Category);
        Assert.Equal("core", hospital.Salience);
        Assert.False(SalienceClassifier.IsAreaRankedBuilding(hospital.Category));

        var stadium = Classify(("building", "stadium"), ("name", "The Bowl"));
        Assert.Equal("core", stadium.Salience); // leisure.stadium is a core venue

        // The institution itself (amenity=university) stays core — only the HALL is budgeted.
        Assert.Equal("core", Classify(("amenity", "university"), ("name", "State University")).Salience);
    }

    [Fact]
    public void A_dorm_building_is_core_like_amenity_student_accommodation()
    {
        // building=dormitory is the same real thing as amenity=student_accommodation → core, not
        // clustered-residential (ADR-0019 consistency fix).
        var dorm = Classify(("building", "dormitory"), ("name", "Cedar House"));
        Assert.Equal("lodging.student_accommodation", dorm.Category);
        Assert.Equal("core", dorm.Salience);
    }

    [Fact]
    public void A_named_station_is_a_core_arrival_landmark()
    {
        Assert.Equal("core", Classify(("railway", "station"), ("name", "Central Station")).Salience);
        Assert.Equal("civic.station", Classify(("public_transport", "station"), ("name", "North Station")).Category);
        // An unnamed station is a weak anchor — not a Feature (same gate as an unnamed hall).
        Assert.Null(TryClassify(("railway", "station")));
    }

    [Fact]
    public void An_unnamed_institutional_building_is_not_a_feature()
    {
        // Named-only gate: an unnamed hall/institution building clusters as a generic building (dropped
        // here), it does not become a category-label token.
        Assert.Null(TryClassify(("building", "university")));
        Assert.Null(TryClassify(("building", "hospital")));
    }

    [Fact]
    public void A_cafe_inside_a_university_building_classifies_as_the_cafe()
    {
        // The functional amenity wins over the building envelope.
        var c = Classify(("building", "university"), ("amenity", "cafe"), ("name", "Atrium Coffee"));
        Assert.Equal("food.cafe", c.Category);
    }

    [Fact]
    public void Civic_salience_is_an_allowlist_so_unenumerated_civic_defaults_to_budgeted()
    {
        // The core-leak fix (ADR-0018 note): a `healthcare=*` passthrough and social_facility used to
        // fall through to CORE at importance 90 (always promoted). Now civic is an ALLOWLIST — only the
        // public institutions are core; everything else civic competes at the venue band (65).
        foreach (var (k, v) in new[] { ("healthcare", "psychotherapist"), ("healthcare", "physiotherapist"),
                                       ("healthcare", "alternative"), ("amenity", "social_facility") })
        {
            var c = Classify((k, v), ("name", "Sample"));
            Assert.Equal("budgeted", c.Salience);
            Assert.Equal(65, c.Importance); // 55 venue band + 10 name — no longer 90
        }
    }

    [Fact]
    public void Private_practices_tie_venues_instead_of_outranking_them()
    {
        // Private practices dropped 60 → 55 base so a dentist no longer outranks a café or a hall.
        var dentist = Classify(("amenity", "dentist"), ("name", "Bright Smiles"));
        var cafe = Classify(("amenity", "cafe"), ("name", "The Grind"));
        var hall = Classify(("building", "university"), ("name", "Maple Hall"));
        Assert.Equal("budgeted", dentist.Salience);
        Assert.Equal(cafe.Importance, dentist.Importance);   // tie the venue band
        Assert.Equal(hall.Importance, dentist.Importance);
    }

    [Fact]
    public void The_core_public_institutions_stay_core()
    {
        foreach (var amenity in new[] { "school", "library", "hospital", "post_office", "police",
                                        "fire_station", "community_centre", "kindergarten" })
            Assert.Equal("core", Classify(("amenity", amenity), ("name", "Sample")).Salience);
        // ...and they still outrank the budgeted competitors.
        Assert.True(Classify(("amenity", "library"), ("name", "X")).Importance
                    > Classify(("amenity", "dentist"), ("name", "Y")).Importance);
    }
}
