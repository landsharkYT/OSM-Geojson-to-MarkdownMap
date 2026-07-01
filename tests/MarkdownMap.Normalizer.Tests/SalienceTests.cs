using System.Collections.Generic;
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

    [Fact]
    public void Worship_and_artwork_land_on_opposite_sides_of_the_core_line()
    {
        Assert.Equal("core", Classify(("amenity", "place_of_worship"), ("name", "St. Anne's")).Salience);
        Assert.Equal("budgeted", Classify(("tourism", "artwork"), ("name", "The Mural")).Salience);
    }
}
