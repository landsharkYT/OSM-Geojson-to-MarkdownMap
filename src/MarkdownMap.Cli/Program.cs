using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using MarkdownMap.Contract;
using MarkdownMap.Generator;
using MarkdownMap.Normalizer;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: markdownmap <input.osm|input.geojson> [output.md|output.geojson]");
    return 1;
}

var input = args[0];
if (!File.Exists(input))
{
    Console.Error.WriteLine($"input not found: {input}");
    return 1;
}

var output = args.Length >= 2 ? args[1] : Path.ChangeExtension(input, ".map.md");
var ext = Path.GetExtension(input).ToLowerInvariant();

FeatureCollection fc;
switch (ext)
{
    case ".osm":
        fc = new OsmNormalizer().NormalizeFile(input);   // Stage 1
        break;
    case ".geojson":
    case ".json":
        fc = GeoJsonReader.Read(File.ReadAllText(input)); // Stage 2 only
        break;
    default:
        Console.Error.WriteLine($"unsupported input '{ext}' (want .osm or .geojson)");
        return 1;
}

// .geojson output = dump Stage 1 contract; otherwise render the MarkdownMap (Stage 2).
if (Path.GetExtension(output).ToLowerInvariant() == ".geojson")
{
    File.WriteAllText(output, JsonSerializer.Serialize(fc, NormalizerJson.Options));
}
else
{
    File.WriteAllText(output, new MapGenerator().Generate(fc));
}

var byKind = fc.Features.GroupBy(f => f.Properties.Kind)
    .OrderBy(g => g.Key, StringComparer.Ordinal).Select(g => $"{g.Key}:{g.Count()}");
Console.WriteLine($"title: {fc.Properties.Title}");
Console.WriteLine($"features: {fc.Features.Count}  [{string.Join(", ", byKind)}]");
Console.WriteLine($"wrote: {output}");
return 0;
