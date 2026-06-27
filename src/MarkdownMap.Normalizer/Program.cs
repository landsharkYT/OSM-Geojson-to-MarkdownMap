using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using MarkdownMap.Normalizer;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: normalizer <input.osm> [output.geojson]");
    return 1;
}

var inputPath = args[0];
if (!File.Exists(inputPath))
{
    Console.Error.WriteLine($"input not found: {inputPath}");
    return 1;
}

var outputPath = args.Length >= 2
    ? args[1]
    : Path.ChangeExtension(inputPath, ".poi.geojson");

var fc = new OsmNormalizer().NormalizeFile(inputPath);
File.WriteAllText(outputPath, JsonSerializer.Serialize(fc, NormalizerJson.Options));

var byKind = fc.Features
    .GroupBy(f => f.Properties.Kind)
    .OrderBy(g => g.Key, StringComparer.Ordinal)
    .Select(g => $"{g.Key}:{g.Count()}");

Console.WriteLine($"title: {fc.Properties.Title}");
Console.WriteLine($"features: {fc.Features.Count}  [{string.Join(", ", byKind)}]");
Console.WriteLine($"wrote: {outputPath}");
return 0;
