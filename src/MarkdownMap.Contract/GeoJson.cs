using System.Collections.Generic;

namespace MarkdownMap.Contract
{
    /// <summary>
    /// The Stage 1 -> Stage 2 contract: a typed, mixed-geometry GeoJSON FeatureCollection.
    /// See docs/feature-schema.md (ADR-0006). POCOs are intentionally dependency-free;
    /// serialization options (camelCase, null-omission) are configured by the producer.
    /// </summary>
    public sealed class FeatureCollection
    {
        public string Type { get; set; } = "FeatureCollection";
        public CollectionProperties Properties { get; set; } = new CollectionProperties();
        public List<Feature> Features { get; set; } = new List<Feature>();
    }

    public sealed class CollectionProperties
    {
        public int SchemaVersion { get; set; } = 1;
        public string Title { get; set; } = "";

        /// <summary>[minLon, minLat, maxLon, maxLat]</summary>
        public double[] Bounds { get; set; } = new double[4];
    }

    public sealed class Feature
    {
        public string Type { get; set; } = "Feature";
        public FeatureProperties Properties { get; set; } = new FeatureProperties();
        public Geometry Geometry { get; set; } = new Geometry();
    }

    /// <summary>
    /// Flexible property bag across all feature kinds; kind-irrelevant members stay null
    /// and are omitted on serialization (e.g. barrierClass on a poi).
    /// </summary>
    public sealed class FeatureProperties
    {
        /// <summary>poi | road | barrier | water | park | place</summary>
        public string Kind { get; set; } = "";
        public string? Name { get; set; }

        /// <summary>Provenance, e.g. "n29445653" / "w12345". Ignored by Stage 2 ranking.</summary>
        public string? OsmId { get; set; }

        // poi / place
        public string? Category { get; set; }
        public int? Importance { get; set; }

        /// <summary>landmark | destination | minor | structure</summary>
        public string? Tier { get; set; }

        /// <summary>Narrative salience (ADR-0018): core | budgeted | clustered. poi only.</summary>
        public string? Salience { get; set; }

        // poi (street attribution lands in a later step; null for now)
        public string? Street { get; set; }
        public bool? StreetApprox { get; set; }

        // barrier
        public string? BarrierClass { get; set; }
    }

    public sealed class Geometry
    {
        /// <summary>Point | LineString | Polygon | MultiLineString | MultiPolygon</summary>
        public string Type { get; set; } = "Point";

        /// <summary>
        /// Point => double[] [lon,lat]; LineString => double[][]; Polygon => double[][][].
        /// </summary>
        public object Coordinates { get; set; } = new double[0];
    }
}
