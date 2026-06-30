using System.Runtime.InteropServices.JavaScript;
using MarkdownMap.Bridge;

// Entry point; the Explorer calls the [JSExport] methods below after the runtime loads.
return 0;

/// <summary>
/// Thin JS-facing shim over <see cref="MapBuilder"/> (the testable logic lives there).
/// Each method returns MapModel JSON, or {"error": "..."} on failure.
/// </summary>
public static partial class MarkdownMapWasm
{
    /// <summary>Full pipeline: raw .osm bytes (Uint8Array from JS) → MapModel JSON.</summary>
    [JSExport]
    internal static string BuildFromOsm(byte[] osmBytes) => MapBuilder.FromOsm(osmBytes);

    /// <summary>Generator only: GeoJSON text → MapModel JSON.</summary>
    [JSExport]
    internal static string BuildFromGeoJson(string geojson) => MapBuilder.FromGeoJson(geojson);
}
