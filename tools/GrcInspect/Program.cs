using System.Globalization;
using System.IO.Compression;

string? pngDir = null;
var listCells = false;
var cells = new List<int>();
var files = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--png")
    {
        if (i + 1 >= args.Length)
        {
            Console.Error.WriteLine("Falta la carpeta despues de --png");
            return 1;
        }
        pngDir = args[++i];
    }
    else if (args[i] == "--list")
        listCells = true;
    else if (args[i] == "--cells")
    {
        if (i + 1 >= args.Length)
        {
            Console.Error.WriteLine("Faltan los indices despues de --cells");
            return 1;
        }
        foreach (var part in args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries))
            cells.Add(int.Parse(part, CultureInfo.InvariantCulture));
    }
    else
    {
        files.Add(args[i]);
    }
}

if (files.Count == 0)
{
    Console.Error.WriteLine("Uso: GrcInspect [--png carpeta] <archivo.grc> [mas archivos]");
    return 1;
}

if (pngDir != null)
    Directory.CreateDirectory(pngDir);

var failed = 0;
foreach (var path in files)
{
    try
    {
        Inspect(path, pngDir, cells, listCells);
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"ERROR {path}");
        Console.WriteLine(ex.Message);
    }
    Console.WriteLine();
}

return failed == 0 ? 0 : 2;

static void Inspect(string path, string? pngDir, List<int> cells, bool listCells)
{
    var file = File.ReadAllBytes(path);
    var decoded = new byte[Math.Max(file.Length * 8, 1024 * 1024)];
    var size = Decode(file, decoded);
    Console.WriteLine(path);
    Console.WriteLine($"file={file.Length} decoded={size}");
    if (size < 32)
    {
        Console.WriteLine("decoded demasiado corto");
        return;
    }

    Console.Write("u32:");
    for (var i = 0; i < 8; i++)
        Console.Write($" {ReadU32(decoded, i * 4).ToString("X8", CultureInfo.InvariantCulture)}");
    Console.WriteLine();

    var s2 = (int)ReadU32(decoded, 8);
    var s3 = (int)ReadU32(decoded, 12);
    Console.WriteLine($"s2={s2} s3={s3}");
    if (s2 <= 0 || s3 <= s2 || s3 >= size)
    {
        Console.WriteLine("s2/s3 fuera de rango");
        return;
    }

    var tileBytes = size - s3;
    Console.WriteLine($"tileBytes={tileBytes} tiles128={(tileBytes % 128 == 0 ? (tileBytes / 128).ToString() : "no-divisible")}");

    var dirBytes = s3 - s2;
    Console.WriteLine($"dirBytes={dirBytes}");
    var shown = Math.Min(dirBytes / 4, 12);
    Console.Write("dir:");
    for (var i = 0; i < shown; i++)
        Console.Write($" {ReadU32(decoded, s2 + i * 4)}");
    Console.WriteLine();

    var first = (int)ReadU32(decoded, s2);
    if (first <= 0 || s2 + first + 4 >= s3)
    {
        Console.WriteLine("primer offset de sprite no usable");
        return;
    }

    var metas = 0;
    var limit = Math.Min(first / 4, 4096);
    for (var i = 0; i < limit; i++)
    {
        var rel = (int)ReadU32(decoded, s2 + i * 4);
        if (rel < first || s2 + rel + 4 >= s3)
            break;
        var meta = s2 + rel;
        var type = decoded[meta + 1] & 15;
        var sw = decoded[meta + 2];
        var sh = decoded[meta + 3];
        if (metas < 8)
            Console.WriteLine($"sprite[{metas}] rel={rel} type={type} sw={sw} sh={sh}");
        metas++;
    }
    Console.WriteLine($"spritesLeidos={metas}");
    if (listCells)
    {
        var full = 0;
        var wide = 0;
        for (var i = 0; i < metas; i++)
        {
            var rel = (int)ReadU32(decoded, s2 + i * 4);
            var meta = s2 + rel;
            var sw = decoded[meta + 2];
            var sh = decoded[meta + 3];
            if (sh >= 6)
                full++;
            if (sw >= 4 && sh >= 6)
                wide++;
        }
        Console.WriteLine($"altos={full} cuerpos={wide}");
    }
    if (cells.Count > 0 && pngDir != null)
    {
        var cellStem = Path.GetFileNameWithoutExtension(path);
        foreach (var index in cells)
        {
            var outPath = Path.Combine(pngDir, $"{cellStem}-cell{index}.png");
            Console.WriteLine(ExportIndex(decoded, s2, s3, index, outPath));
        }
        return;
    }
    if (pngDir == null)
        return;

    var stem = Path.GetFileNameWithoutExtension(path);
    var parent = Path.GetFileName(Path.GetDirectoryName(path)) ?? "grc";
    var written = ExportSample(decoded, s2, s3, Path.Combine(pngDir, $"{parent}-{stem}.png"));
    Console.WriteLine($"png={written}");
}

static string ExportIndex(byte[] dec, int s2, int s3, int index, string pngPath)
{
    var first = (int)ReadU32(dec, s2);
    var limit = Math.Min(first / 4, 8192);
    if (index < 0 || index >= limit)
        return $"sprite {index} fuera de rango";
    var rel = (int)ReadU32(dec, s2 + index * 4);
    if (rel < first)
        return $"sprite {index} offset {rel} no usable";
    var metaAt = s2 + rel;
    var type = dec[metaAt + 1] & 15;
    var widthTiles = dec[metaAt + 2];
    var heightTiles = dec[metaAt + 3];
    if (widthTiles == 0 || heightTiles == 0 || widthTiles > 32 || heightTiles > 32)
        return $"sprite {index} type={type} {widthTiles}x{heightTiles}";
    var maskWords = type == 5;
    var rec = metaAt + 4 + (maskWords ? widthTiles * 2 : widthTiles + (widthTiles & 1));
    var w = widthTiles * 16;
    var h = heightTiles * 16;
    var pix = new byte[w * h * 4];
    var p = rec;
    var off = rec;
    var used = 0;
    var maxTile = 0;
    for (var col = 0; col < widthTiles; col++)
    {
        var bits = maskWords
            ? (uint)(dec[metaAt + 4 + col * 2] | (dec[metaAt + 5 + col * 2] << 8))
            : dec[metaAt + 4 + col];
        for (var row = 0; row < heightTiles; row++)
        {
            var on = maskWords ? (bits & 0x8000u) != 0 : (bits & 0x80u) != 0;
            if (on)
            {
                var odd = off & 1;
                var tile = (dec[p + odd] | (dec[p + odd + 1] << 8)) & 0xFFF;
                if (tile > maxTile)
                    maxTile = tile;
                Blit(dec, s3, tile, pix, w, h, col * 16, row * 16);
                p += 3;
                off += 3;
                used++;
            }
            bits <<= 1;
        }
    }

    WritePng(pngPath, w, h, pix);
    return $"{pngPath} sprite={index} type={type} {w}x{h} tiles={used} max={maxTile}";
}

static string ExportSample(byte[] dec, int s2, int s3, string pngPath)
{
    var chosen = 0;
    var bestArea = -1;
    var limit = Math.Min((int)ReadU32(dec, s2) / 4, 80);
    for (var i = 0; i < limit; i++)
    {
        var rel = (int)ReadU32(dec, s2 + i * 4);
        var meta = s2 + rel;
        var sw = dec[meta + 2];
        var sh = dec[meta + 3];
        var area = sw * sh;
        if (area > bestArea && sw >= 3 && sh >= 5 && sw <= 16 && sh <= 16)
        {
            bestArea = area;
            chosen = i;
        }
    }

    var rel0 = (int)ReadU32(dec, s2 + chosen * 4);
    var metaAt = s2 + rel0;
    var type = dec[metaAt + 1] & 15;
    var widthTiles = dec[metaAt + 2];
    var heightTiles = dec[metaAt + 3];
    var maskWords = type == 5;
    var rec = metaAt + 4 + (maskWords ? widthTiles * 2 : widthTiles + (widthTiles & 1));
    var w = widthTiles * 16;
    var h = heightTiles * 16;
    var pix = new byte[w * h * 4];
    var p = rec;
    var off = rec;
    for (var col = 0; col < widthTiles; col++)
    {
        var bits = maskWords
            ? (uint)(dec[metaAt + 4 + col * 2] | (dec[metaAt + 5 + col * 2] << 8))
            : dec[metaAt + 4 + col];
        for (var row = 0; row < heightTiles; row++)
        {
            var on = maskWords ? (bits & 0x8000u) != 0 : (bits & 0x80u) != 0;
            if (on)
            {
                var odd = off & 1;
                var tile = (dec[p + odd] | (dec[p + odd + 1] << 8)) & 0xFFF;
                Blit(dec, s3, tile, pix, w, h, col * 16, row * 16);
                p += 3;
                off += 3;
            }
            bits <<= 1;
        }
    }

    WritePng(pngPath, w, h, pix);
    return $"{pngPath} sprite={chosen} {w}x{h}";
}

static void Blit(byte[] dec, int s3, int tile, byte[] pix, int w, int h, int dx, int dy)
{
    if (tile <= 0)
        return;
    var src = s3 + tile * 128;
    for (var ty = 0; ty < 16; ty++)
    {
        for (var tx = 0; tx < 16; tx++)
        {
            var x = dx + tx;
            var y = dy + ty;
            if ((uint)x >= (uint)w || (uint)y >= (uint)h)
                continue;
            var v = dec[src + ty * 8 + (tx >> 1)];
            var n = (tx & 1) != 0 ? (byte)(v >> 4) : (byte)(v & 15);
            if (n == 0)
                continue;
            var o = (y * w + x) * 4;
            var g = (byte)(n * 17);
            pix[o] = g;
            pix[o + 1] = g;
            pix[o + 2] = g;
            pix[o + 3] = 255;
        }
    }
}

static void WritePng(string path, int w, int h, byte[] rgba)
{
    using var output = File.Create(path);
    var sig = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
    output.Write(sig);
    WriteChunk(output, "IHDR", BuildIhdr(w, h));
    var raw = new byte[(w * 4 + 1) * h];
    for (var y = 0; y < h; y++)
        Buffer.BlockCopy(rgba, y * w * 4, raw, y * (w * 4 + 1) + 1, w * 4);
    using var compressed = new MemoryStream();
    using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, true))
        zlib.Write(raw);
    WriteChunk(output, "IDAT", compressed.ToArray());
    WriteChunk(output, "IEND", Array.Empty<byte>());
}

static byte[] BuildIhdr(int w, int h)
{
    var b = new byte[13];
    WriteBe32(b, 0, (uint)w);
    WriteBe32(b, 4, (uint)h);
    b[8] = 8;
    b[9] = 6;
    return b;
}

static void WriteChunk(Stream output, string type, byte[] data)
{
    var head = new byte[4];
    WriteBe32(head, 0, (uint)data.Length);
    output.Write(head);
    var tag = System.Text.Encoding.ASCII.GetBytes(type);
    output.Write(tag);
    output.Write(data);
    var crcSrc = new byte[tag.Length + data.Length];
    Buffer.BlockCopy(tag, 0, crcSrc, 0, tag.Length);
    Buffer.BlockCopy(data, 0, crcSrc, tag.Length, data.Length);
    WriteBe32(head, 0, Crc32(crcSrc));
    output.Write(head);
}

static void WriteBe32(byte[] b, int o, uint value)
{
    b[o] = (byte)(value >> 24);
    b[o + 1] = (byte)(value >> 16);
    b[o + 2] = (byte)(value >> 8);
    b[o + 3] = (byte)value;
}

static uint Crc32(byte[] data)
{
    var crc = 0xFFFFFFFFu;
    foreach (var value in data)
    {
        crc ^= value;
        for (var i = 0; i < 8; i++)
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
    }
    return ~crc;
}

static int Decode(byte[] input, byte[] output)
{
    var i = 0;
    var o = 0;
    while (i < input.Length)
    {
        var c = input[i++];
        if (c < 0xC0)
        {
            if (o >= output.Length) return -1;
            output[o++] = c;
        }
        else if (c == 0xC0)
        {
            break;
        }
        else if (c == 0xC1)
        {
            if (i >= input.Length || o >= output.Length) return -1;
            output[o++] = input[i++];
        }
        else
        {
            var count = c - 0xC0;
            if (i >= input.Length) return -1;
            var back = input[i++];
            if (back <= 0 || back > o) return -1;
            while (count-- > 0)
            {
                if (o >= output.Length) return -1;
                output[o] = output[o - back];
                o++;
            }
        }
    }
    return o;
}

static uint ReadU32(byte[] data, int offset)
{
    return (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24));
}
