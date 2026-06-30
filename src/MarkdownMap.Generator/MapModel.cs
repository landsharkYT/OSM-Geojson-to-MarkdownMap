using System.Collections.Generic;

namespace MarkdownMap.Generator;

/// <summary>
/// The Generator's structured output (docs/map-model.md). The MarkdownMap is rendered
/// *from* this; the Explorer consumes its JSON. One source of truth, two views.
/// </summary>
public sealed class MapModel
{
    public string Title { get; set; } = "";

    /// <summary>[minLon, minLat, maxLon, maxLat], or empty.</summary>
    public double[] Bounds { get; set; } = new double[0];

    public List<PromotedFeature> Features { get; set; } = new();
    public List<MinorFeature> Minors { get; set; } = new();
    public List<Edge> Edges { get; set; } = new();
    public List<District> Districts { get; set; } = new();
    public List<TerrainEntry> Terrain { get; set; } = new();

    /// <summary>The rendered MarkdownMap (byte-identical to the Generator's string output).</summary>
    public string Markdown { get; set; } = "";
}

/// <summary>A tokenized, graphed feature.</summary>
public sealed class PromotedFeature
{
    public string Token { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public int Importance { get; set; }
    public string Tier { get; set; } = "";
    public string? Street { get; set; }
    public bool StreetApprox { get; set; }
    public string? District { get; set; }
    public double Lon { get; set; }
    public double Lat { get; set; }
}

/// <summary>A minor feature: clustered (counted) in markdown, plotted faintly in the Explorer.</summary>
public sealed class MinorFeature
{
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string? District { get; set; }
    public double Lon { get; set; }
    public double Lat { get; set; }
}

/// <summary>A proximity-graph connection (one direction).</summary>
public sealed class Edge
{
    public string FromToken { get; set; } = "";
    public string ToToken { get; set; } = "";
    public string ToName { get; set; } = "";
    public int Meters { get; set; }
    public string Dir { get; set; } = "";   // 8-wind
    public string Bucket { get; set; } = "";
    public string? Crosses { get; set; }    // barrier label, if the straight line crosses one
}

public sealed class District
{
    public string Name { get; set; } = "";
    public string? Street { get; set; }     // dominant member street
    public string SpineDir { get; set; } = "";
    public List<string> SpineTokens { get; set; } = new();
    public int PromotedCount { get; set; }
    public int ClusteredCount { get; set; }
    public double AnchorLon { get; set; }
    public double AnchorLat { get; set; }
}

public sealed class TerrainEntry
{
    public string Name { get; set; } = "";
    public string Kind { get; set; } = "";       // water | park | barrier
    public string KindLabel { get; set; } = "";  // water | park | barrier:<class>
    public string Note { get; set; } = "";
    public string Position { get; set; } = "";
    public string GeometryType { get; set; } = "";          // LineString | Polygon
    public List<double[][]> Parts { get; set; } = new();    // each part: [[lon,lat],...]
}
