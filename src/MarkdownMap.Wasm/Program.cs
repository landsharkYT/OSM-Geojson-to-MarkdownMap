using System.Runtime.InteropServices.JavaScript;
using MarkdownMap.Bridge;
using MarkdownMap.Contract;

// Entry point; the Explorer calls the [JSExport] methods below after the runtime loads.
return 0;

/// <summary>
/// Thin JS-facing shim over <see cref="MapBuilder"/> (the testable logic lives there).
/// Each method returns MapModel JSON, or {"error": "..."} on failure, and reports coarse
/// progress out to JS via <see cref="ReportProgress"/> (ADR-0010).
/// </summary>
public static partial class MarkdownMapWasm
{
    // Implemented by the worker via setModuleImports('progress', { report }). The build
    // thread is blocked while this runs, but postMessage from the handler still reaches the
    // (free) main thread, so the bar updates live.
    [JSImport("report", "progress")]
    internal static partial void ReportProgress(int phase, [JSMarshalAs<JSType.Number>] double value);

    private static void Report(BuildPhase phase, long value) => ReportProgress((int)phase, value);

    /// <summary>Full pipeline: raw .osm bytes (Uint8Array from JS) → MapModel JSON.</summary>
    [JSExport]
    internal static string BuildFromOsm(byte[] osmBytes, string optionsJson) =>
        MapBuilder.FromOsm(osmBytes, optionsJson, Report);

    /// <summary>Generator only: GeoJSON text → MapModel JSON.</summary>
    [JSExport]
    internal static string BuildFromGeoJson(string geojson, string optionsJson) =>
        MapBuilder.FromGeoJson(geojson, optionsJson, Report);

    /// <summary>Re-render markdown from an existing model with new settings (ADR-0011) — no rebuild.</summary>
    [JSExport]
    internal static string Render(string mapModelJson, string optionsJson) =>
        MapBuilder.RenderFromModel(mapModelJson, optionsJson);
}
