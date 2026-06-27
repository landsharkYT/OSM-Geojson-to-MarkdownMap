using System;
using System.IO;
using System.Linq;
using MarkdownMap.Contract;
using MarkdownMap.Generator;
using MarkdownMap.Normalizer;

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: markdownmap <input.osm|input.geojson> [output.md]");
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
        fc = new OsmNormalizer().NormalizeFile(input);   // Stage 1 + Stage 2 end-to-end
        break;
    case ".geojson":
    case ".json":
        fc = GeoJsonReader.Read(File.ReadAllText(input)); // Stage 2 only
        break;
    default:
        Console.Error.WriteLine($"unsupported input '{ext}' (want .osm or .geojson)");
        return 1;
}

var markdown = new MapGenerator().Generate(fc);
File.WriteAllText(output, markdown);

var poi = fc.Features.Count(f => f.Properties.Kind == "poi");
Console.WriteLine($"title: {fc.Properties.Title}");
Console.WriteLine($"poi features: {poi}");
Console.WriteLine($"wrote: {output}");
return 0;
