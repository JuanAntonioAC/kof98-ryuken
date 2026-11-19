using System.IO.Compression;

if (args.Length < 2)
{
    Console.Error.WriteLine("Uso: SvcAssemble <svc.zip> <carpeta>");
    return 1;
}

Directory.CreateDirectory(args[1]);
using var zip = ZipFile.OpenRead(args[0]);
var chips = zip.Entries
    .Where(entry => entry.Name.Contains("-c", StringComparison.OrdinalIgnoreCase) && entry.Length == 8 * 1024 * 1024)
    .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
    .ToList();
if (chips.Count < 2)
{
    Console.Error.WriteLine($"no hay pares C ROM en {args[0]}");
    return 1;
}

if (args.Length >= 5 && args[2] == "cols")
{
    var pair = int.Parse(args[3]);
    var start = int.Parse(args[4]);
    var odd = Read(chips[pair * 2]);
    var even = Read(chips[pair * 2 + 1]);
    var merged = Interleave(odd, even);
    var columns = 12;
    var rows = 16;
    WriteSheet(merged, tile => DecodePlanes(merged, tile, true), Path.Combine(args[1], $"cols-{pair}-{start}.png"), start, columns, rows, 1, true);
    return 0;
}

Console.WriteLine(string.Join(", ", chips.Select(entry => entry.Name)));
var pairCount = Math.Min(4, chips.Count / 2);
for (var pair = 0; pair < pairCount; pair++)
{
    var odd = Read(chips[pair * 2]);
    var even = Read(chips[pair * 2 + 1]);
    var merged = Interleave(odd, even);
    var start = FirstDense(merged);
    Console.WriteLine($"par {pair} {chips[pair * 2].Name}+{chips[pair * 2 + 1].Name} denso={start}");
    var tag = $"p{pair}";
    WriteSheet(merged, tile => DecodePlanes(merged, tile, true), Path.Combine(args[1], $"{tag}-lsb-{start}.png"), start, 32, 10);
    var step = Math.Max(1, (merged.Length / 128) / (64 * 24));
    WriteSheet(merged, tile => DecodePlanes(merged, tile, true), Path.Combine(args[1], $"{tag}-survey.png"), 0, 64, 24, step);
}

return 0;

static byte[] Read(ZipArchiveEntry entry)
{
    using var input = entry.Open();
    var data = new byte[entry.Length];
    var got = 0;
    while (got < data.Length)
    {
        var n = input.Read(data, got, data.Length - got);
        if (n == 0)
            break;
        got += n;
    }

    return data;
}

static byte[] Interleave(byte[] odd, byte[] even)
{
    var merged = new byte[odd.Length + even.Length];
    for (var i = 0; i < odd.Length; i++)
    {
        merged[i * 2] = odd[i];
        merged[i * 2 + 1] = even[i];
    }

    return merged;
}

static int FirstDense(byte[] rom)
{
    var tiles = rom.Length / 128;
    var run = 0;
    for (var tile = 0; tile < tiles; tile++)
    {
        run = Filled(rom, tile) >= 80 ? run + 1 : 0;
        if (run == 16)
            return Math.Max(0, tile - 15);
    }

    return 0;
}

static int Filled(byte[] rom, int tile)
{
    var p = tile * 128;
    var n = 0;
    for (var i = 0; i < 128; i++)
    {
        if (rom[p + i] != 0)
            n++;
    }

    return n;
}

static byte[] DecodePlanes(byte[] rom, int tile, bool lsbLeft)
{
    var pix = new byte[16 * 16];
    var p = tile * 128;
    if (p < 0 || p + 128 > rom.Length)
        return pix;
    int[] xOfs = { 8, 8, 0, 0 };
    int[] yOfs = { 0, 8, 0, 8 };
    for (var block = 0; block < 4; block++)
    {
        for (var row = 0; row < 8; row++)
        {
            var bp0 = rom[p++];
            var bp2 = rom[p++];
            var bp1 = rom[p++];
            var bp3 = rom[p++];
            for (var bit = 0; bit < 8; bit++)
            {
                var shift = lsbLeft ? bit : 7 - bit;
                var color = ((bp0 >> shift) & 1)
                    | (((bp1 >> shift) & 1) << 1)
                    | (((bp2 >> shift) & 1) << 2)
                    | (((bp3 >> shift) & 1) << 3);
                pix[(yOfs[block] + row) * 16 + xOfs[block] + bit] = (byte)color;
            }
        }
    }

    return pix;
}

static void WriteSheet(byte[] rom, Func<int, byte[]> decode, string path, int startTile, int columns, int rows, int step = 1, bool columnMajor = false)
{
    var w = columns * 16;
    var h = rows * 16;
    var rgba = new byte[w * h * 4];
    for (var row = 0; row < rows; row++)
    {
        for (var col = 0; col < columns; col++)
        {
            var index = columnMajor
                ? startTile + (col * rows + row) * step
                : startTile + (row * columns + col) * step;
            var tile = decode(index);
            for (var y = 0; y < 16; y++)
            {
                for (var x = 0; x < 16; x++)
                {
                    var n = tile[y * 16 + x];
                    var o = (((row * 16) + y) * w + (col * 16) + x) * 4;
                    var g = (byte)(n * 17);
                    rgba[o] = g;
                    rgba[o + 1] = g;
                    rgba[o + 2] = g;
                    rgba[o + 3] = n == 0 ? (byte)0 : (byte)255;
                }
            }
        }
    }

    WritePng(path, w, h, rgba);
    Console.WriteLine(path);
}

static void WritePng(string path, int w, int h, byte[] rgba)
{
    using var output = File.Create(path);
    output.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
    var ihdr = new byte[13];
    Be(ihdr, 0, (uint)w);
    Be(ihdr, 4, (uint)h);
    ihdr[8] = 8;
    ihdr[9] = 6;
    Chunk(output, "IHDR", ihdr);
    var raw = new byte[(w * 4 + 1) * h];
    for (var y = 0; y < h; y++)
        Buffer.BlockCopy(rgba, y * w * 4, raw, y * (w * 4 + 1) + 1, w * 4);
    using var compressed = new MemoryStream();
    using (var zlib = new System.IO.Compression.ZLibStream(compressed, CompressionLevel.Fastest, true))
        zlib.Write(raw);
    Chunk(output, "IDAT", compressed.ToArray());
    Chunk(output, "IEND", Array.Empty<byte>());
}

static void Chunk(Stream output, string type, byte[] data)
{
    var head = new byte[4];
    Be(head, 0, (uint)data.Length);
    output.Write(head);
    var tag = System.Text.Encoding.ASCII.GetBytes(type);
    output.Write(tag);
    output.Write(data);
    var src = new byte[4 + data.Length];
    Buffer.BlockCopy(tag, 0, src, 0, 4);
    Buffer.BlockCopy(data, 0, src, 4, data.Length);
    Be(head, 0, Crc(src));
    output.Write(head);
}

static void Be(byte[] b, int o, uint value)
{
    b[o] = (byte)(value >> 24);
    b[o + 1] = (byte)(value >> 16);
    b[o + 2] = (byte)(value >> 8);
    b[o + 3] = (byte)value;
}

static uint Crc(byte[] data)
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
