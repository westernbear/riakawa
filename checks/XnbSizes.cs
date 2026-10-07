using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Text.Json;

// Read dimensions using the pinned game's FNA decoder, without a graphics device.
internal static class XnbSizes
{
    internal static void Export(string gameContent, string tmlDirectory, string output)
    {
        var assembly = Assembly.LoadFrom(Path.Combine(tmlDirectory, "Libraries/FNA/1.0.0/FNA.dll"));
        var decoderType = assembly.GetType("Microsoft.Xna.Framework.Content.LzxDecoder", true)!;
        var sizes = new SortedDictionary<string, int[]>();
        foreach (var directory in new[] { Path.Combine(gameContent, "Images"), Path.Combine(tmlDirectory, "Content/Images") }) {
            foreach (var file in Directory.EnumerateFiles(directory, "*.xnb")) {
                string name = Path.GetFileNameWithoutExtension(file);
                if (name is "FishingLine" or "Dust") {
                    sizes[name + "/0"] = ReadSize(file, decoderType);
                    continue;
                }
                if (!(name.StartsWith("Item_") || name.StartsWith("Projectile_") ||
                      name.StartsWith("Extra_") || name.StartsWith("Glow_"))) continue;
                string[] parts = name.Split('_');
                if (parts.Length != 2 || !int.TryParse(parts[1], out _)) continue;
                sizes[(parts[0] == "Glow" ? "GlowMask" : parts[0]) + "/" + parts[1]] = ReadSize(file, decoderType);
            }
        }
        if (sizes.Count < 1000) throw new InvalidDataException("Incomplete game texture directory.");
        File.WriteAllText(output, JsonSerializer.Serialize(sizes, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"PASS: decoded {sizes.Count} XNB texture dimensions");
    }

    private static int[] ReadSize(string path, Type decoderType)
    {
        using var input = File.OpenRead(path);
        using var header = new BinaryReader(input);
        if (new string(header.ReadChars(3)) != "XNB") throw new InvalidDataException(path);
        header.ReadByte();
        if (header.ReadByte() != 5) throw new InvalidDataException("Unsupported XNB version: " + path);
        bool compressed = (header.ReadByte() & 0x80) != 0;
        if (header.ReadInt32() != input.Length) throw new InvalidDataException("Invalid XNB length: " + path);
        using var decoded = new MemoryStream();
        if (compressed) {
            int length = header.ReadInt32();
            if (length <= 0 || length > 64 * 1024 * 1024) throw new InvalidDataException(path);
            var decoder = Activator.CreateInstance(decoderType, 16)!;
            var decompress = decoderType.GetMethod("Decompress")!.CreateDelegate<Func<Stream, int, Stream, int, int>>(decoder);
            while (input.Position < input.Length && decoded.Length < length) {
                int first = header.ReadByte(), second = header.ReadByte();
                int block = (first << 8) | second, frame = 32768;
                if (first == 255) {
                    frame = (second << 8) | header.ReadByte();
                    block = (header.ReadByte() << 8) | header.ReadByte();
                }
                long next = input.Position + block;
                if (block <= 0 || frame <= 0 || next > input.Length || decoded.Length + frame > length ||
                    decompress(input, block, decoded, frame) != 0)
                    throw new InvalidDataException("Invalid XNB block: " + path);
                input.Position = next;
            }
            if (decoded.Length != length) throw new InvalidDataException("Truncated XNB: " + path);
        }
        else input.CopyTo(decoded);
        decoded.Position = 0;
        using var content = new BinaryReader(decoded);
        if (content.Read7BitEncodedInt() != 1 || !content.ReadString().Contains("Texture2DReader"))
            throw new InvalidDataException("Not a single texture: " + path);
        content.ReadInt32(); // Reader version.
        if (content.Read7BitEncodedInt() != 0 || content.Read7BitEncodedInt() != 1)
            throw new InvalidDataException("Unexpected XNB resources: " + path);
        content.ReadInt32(); // Surface format.
        int width = content.ReadInt32(), height = content.ReadInt32();
        if (width <= 0 || height <= 0 || width > 16384 || height > 16384) throw new InvalidDataException(path);
        return [width, height];
    }
}
