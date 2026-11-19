if (args.Length == 0)
{
    Console.Error.WriteLine("Uso: GrcRoundtrip <archivo.grc> [mas archivos]");
    return 1;
}

var failed = 0;
failed += Synthetic();
foreach (var path in args)
{
    if (!Roundtrip(path))
        failed++;
}

return failed == 0 ? 0 : 2;

static int Synthetic()
{
    var src = new byte[4096];
    for (var i = 0; i < src.Length; i++)
        src[i] = (byte)(i * 17 + (i % 64));
    for (var i = 100; i < 400; i++)
        src[i] = src[i - 7];
    src[500] = 0xC0;
    src[501] = 0xC1;
    src[502] = 0xFF;
    return Same("sintetico", src, Encode(src)) ? 0 : 1;
}

static bool Roundtrip(string path)
{
    var file = File.ReadAllBytes(path);
    var decoded = new byte[Math.Max(file.Length * 12, 1024 * 1024)];
    var size = Decode(file, decoded);
    Console.WriteLine(path);
    if (size < 0)
    {
        Console.WriteLine("  no se pudo decodificar el original");
        Console.WriteLine();
        return false;
    }

    var plain = new byte[size];
    Buffer.BlockCopy(decoded, 0, plain, 0, size);
    var watch = System.Diagnostics.Stopwatch.StartNew();
    var encoded = Encode(plain);
    watch.Stop();
    var again = new byte[size + 64];
    var size2 = Decode(encoded, again);
    var equal = size2 == size;
    if (equal)
    {
        for (var i = 0; i < size; i++)
        {
            if (again[i] != plain[i])
            {
                equal = false;
                Console.WriteLine($"  primer byte distinto en {i}: {plain[i]:X2} != {again[i]:X2}");
                break;
            }
        }
    }

    Console.WriteLine($"  original={file.Length} decoded={size} reencoded={encoded.Length} ratio={(encoded.Length * 100.0 / Math.Max(size, 1)):F1}% time={watch.ElapsedMilliseconds}ms");
    Console.WriteLine(equal ? "  IGUAL" : $"  DISTINTO decoded2={size2}");
    Console.WriteLine();
    return equal;
}

static byte[] Encode(byte[] data)
{
    var output = new List<byte>(data.Length);
    var i = 0;
    while (i < data.Length)
    {
        var matched = BestMatch(data, i);
        var use = matched.Length >= 3 || (matched.Length == 2 && LiteralCost(data, i, 2) > 2);
        if (use)
        {
            output.Add((byte)(0xC0 + matched.Length));
            output.Add((byte)matched.Distance);
            i += matched.Length;
            continue;
        }

        var value = data[i++];
        if (value >= 0xC0)
            output.Add(0xC1);
        output.Add(value);
    }

    output.Add(0xC0);
    return output.ToArray();
}

static (int Length, int Distance) BestMatch(byte[] data, int i)
{
    var maxLen = Math.Min(63, data.Length - i);
    var maxDist = Math.Min(255, i);
    var bestLen = 0;
    var bestDist = 0;
    if (maxLen < 2 || maxDist < 1)
        return (0, 0);

    var first = data[i];
    for (var dist = 1; dist <= maxDist; dist++)
    {
        if (data[i - dist] != first)
            continue;
        var len = 1;
        while (len < maxLen && data[i + len] == data[i - dist + len])
            len++;
        if (len > bestLen)
        {
            bestLen = len;
            bestDist = dist;
            if (bestLen == maxLen)
                break;
        }
    }

    return (bestLen, bestDist);
}

static int LiteralCost(byte[] data, int i, int length)
{
    var cost = 0;
    for (var n = 0; n < length; n++)
        cost += data[i + n] >= 0xC0 ? 2 : 1;
    return cost;
}

static bool Same(string name, byte[] plain, byte[] encoded)
{
    var again = new byte[plain.Length + 64];
    var size = Decode(encoded, again);
    var equal = size == plain.Length;
    if (equal)
    {
        for (var i = 0; i < plain.Length; i++)
        {
            if (again[i] == plain[i])
                continue;
            equal = false;
            break;
        }
    }

    Console.WriteLine($"{name}: {(equal ? "IGUAL" : "DISTINTO")} plain={plain.Length} packed={encoded.Length}");
    return equal;
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
