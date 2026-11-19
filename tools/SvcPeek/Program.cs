using System.IO.Compression;

if (args.Length < 2)
{
    Console.Error.WriteLine("Uso: SvcPeek <c1.bin> <carpeta>");
    return 1;
}

var rom = File.ReadAllBytes(args[0]);
Directory.CreateDirectory(args[1]);
if (args.Length >= 4 && args[3] == "pair")
{
    var merged = new byte[16 * 1024 * 1024];
    var half = 8 * 1024 * 1024;
    for (var i = 0; i < half; i++)
    {
        merged[i * 2] = rom[i];
        merged[i * 2 + 1] = rom[half + i];
    }

    var path = Path.Combine(args[1], "pair-mame.png");
    WriteSheet(merged, DecodeMame, path, 0, 32, 12);
    Console.WriteLine(path);
    return 0;
}

var starts = new List<int>();
if (args.Length >= 3)
{
    starts.Add(int.Parse(args[2]));
}
else
{
    var tiles = rom.Length / 128;
    var run = 0;
    for (var tile = 0; tile < tiles && starts.Count < 8; tile++)
    {
        var filled = Filled(rom, tile);
        run = filled >= 70 ? run + 1 : 0;
        if (run == 24)
        {
            var start = Math.Max(0, tile - 23);
            starts.Add(start);
            Console.WriteLine($"denso en tile {start}");
            tile += 4000;
            run = 0;
        }
    }
}

foreach (var start in starts)
{
    var decode = args.Length >= 4 && args[3] == "mame" ? (Func<byte[], int, byte[]>)DecodeMame : DecodeNibble;
    var path = Path.Combine(args[1], $"{(args.Length >= 4 ? args[3] : "nibble")}-{start}.png");
    WriteSheet(rom, decode, path, start, columns: 32, rows: 12);
    Console.WriteLine(path);
}

return 0;

static byte[] DecodeMame(byte[] rom, int tile) => DecodePlanes(rom, tile, lsbLeft: true);

static byte[] DecodePlanes(byte[] rom, int tile, bool lsbLeft)
{
    var pix = new byte[16 * 16];
    var p = tile * 128;
    if (p + 128 > rom.Length)
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

static int Filled(byte[] rom, int tile)
{
    var p = tile * 128;
    if (p + 128 > rom.Length)
        return 0;
    var n = 0;
    for (var i = 0; i < 128; i++)
    {
        var v = rom[p + i];
        if ((v & 15) != 0) n++;
        if ((v >> 4) != 0) n++;
    }

    return n;
}

static byte[] DecodeNibble(byte[] rom, int tile)
{
    var pix = new byte[16 * 16];
    var p = tile * 128;
    if (p + 128 > rom.Length)
        return pix;
    for (var y = 0; y < 16; y++)
    {
        for (var x = 0; x < 16; x++)
        {
            var v = rom[p + y * 8 + (x >> 1)];
            pix[y * 16 + x] = (x & 1) != 0 ? (byte)(v >> 4) : (byte)(v & 15);
        }
    }

    return pix;
}

static void WriteSheet(byte[] rom, Func<byte[], int, byte[]> decode, string path, int startTile, int columns, int rows)
{
    var w = columns * 16;
    var h = rows * 16;
    var rgba = new byte[w * h * 4];
    for (var row = 0; row < rows; row++)
    {
        for (var col = 0; col < columns; col++)
        {
            var tile = decode(rom, startTile + row * columns + col);
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
    using (var zlib = new ZLibStream(compressed, CompressionLevel.Fastest, true))
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
