using System.Collections.Generic;
using MarkdownMap.Normalizer;
using Xunit;

namespace MarkdownMap.Normalizer.Tests;

public class ClassifierTests
{
    private static Dictionary<string, string> Tags(params string[] kv)
    {
        var d = new Dictionary<string, string>();
        for (int i = 0; i < kv.Length; i += 2) d[kv[i]] = kv[i + 1];
        return d;
    }

    [Theory]
    [InlineData("amenity", "parking")]
    [InlineData("amenity", "bench")]
    [InlineData("amenity", "bicycle_parking")]
    [InlineData("amenity", "waste_basket")]
    [InlineData("natural", "tree")]
    public void Gate_drops_rp_noise(string k, string v)
    {
        Assert.Null(Classifier.Classify(Tags(k, v, "name", "Whatever")));
    }

    [Fact]
    public void Unnamed_building_is_not_a_poi()
    {
        Assert.Null(Classifier.Classify(Tags("building", "house")));
    }

    [Fact]
    public void Named_houseboat_is_minor_residential()
    {
        var c = Classifier.Classify(Tags("building", "houseboat", "name", "The Ark"));
        Assert.NotNull(c);
        Assert.Equal("residential.houseboat", c!.Category);
        Assert.Equal(40, c.Importance); // base 30 + name 10
        Assert.Equal("minor", c.Tier);
    }

    [Fact]
    public void Bar_with_name_is_food_destination()
    {
        var c = Classifier.Classify(Tags("amenity", "bar", "name", "Sample Tavern"));
        Assert.Equal("food.bar", c!.Category);
        Assert.Equal(65, c.Importance); // 55 + 10
        Assert.Equal("destination", c.Tier);
    }

    [Fact]
    public void School_is_civic_landmark_tier()
    {
        var c = Classifier.Classify(Tags("amenity", "school", "name", "Sample School"));
        Assert.Equal("civic.school", c!.Category);
        Assert.Equal(90, c.Importance); // 80 + 10, no landmark bonus
        Assert.Equal("landmark", c.Tier);
    }

    [Fact]
    public void Artwork_gets_landmark_bonus()
    {
        var c = Classifier.Classify(Tags("tourism", "artwork", "name", "Mural"));
        Assert.Equal("landmark.artwork", c!.Category);
        Assert.Equal(95, c.Importance); // 80 + 10 + 5
        Assert.Equal("landmark", c.Tier);
    }

    [Fact]
    public void Deli_is_food_not_shop()
    {
        var c = Classifier.Classify(Tags("shop", "deli", "name", "Sample Deli"));
        Assert.Equal("food.deli", c!.Category);
    }

    [Fact]
    public void Generic_shop_is_destination()
    {
        var c = Classifier.Classify(Tags("shop", "convenience", "name", "Sample Market"));
        Assert.Equal("shop.convenience", c!.Category);
        Assert.Equal("destination", c.Tier);
    }

    [Fact]
    public void Unnamed_cafe_still_included_but_lower_score()
    {
        var c = Classifier.Classify(Tags("amenity", "cafe"));
        Assert.Equal("food.cafe", c!.Category);
        Assert.Equal(55, c.Importance); // no name bonus
        Assert.Equal("destination", c.Tier);
    }
}
