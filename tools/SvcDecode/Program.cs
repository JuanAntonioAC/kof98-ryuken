using System.IO.Compression;

if (args.Length < 2)
{
    Console.Error.WriteLine("Uso: SvcDecode <svcsplus.zip> <carpeta>");
    return 1;
}

Directory.CreateDirectory(args[1]);
using var zip = ZipFile.OpenRead(args[0]);
var cpu = new byte[0x800000];
Copy(zip, "svc-p1sp.bin", cpu, 0);
Copy(zip, "svc-p2sp.bin", cpu, 0x400000);
WordSwap(cpu);
PxDecrypt(cpu);
Patch(cpu);
if (args.Length >= 3 && args[2] == "trace")
{
    TraceSpritePointer(cpu);
    Console.WriteLine("--- saltos a 5D9C ---");
    var jumps = 0;
    for (var at = 0; at + 4 < cpu.Length && jumps < 20; at += 2)
    {
        if ((at & 0x100000) != 0)
            continue;
        var op = Be16(cpu, at);
        var hi = op & 0xFF00;
        if (hi != 0x6000 && hi != 0x6100)
            continue;
        var disp = (op & 0xFF) == 0 ? (int)(short)Be16(cpu, at + 2) : (sbyte)(op & 0xFF);
        var dest = at + 2 + disp;
        if (dest != 0x5D9C && dest != 0x5D9E && at + 4 + disp != 0x5D9C)
            continue;
        Console.WriteLine($"salto @{at:X6} -> {dest:X}");
        jumps++;
    }

    Console.WriteLine("--- tabla 456A30 ---");
    for (var i = -8; i < 16; i++)
    {
        var ptr = ReadBE32(cpu, 0x456A30 + i * 4);
        Console.WriteLine($"{i,3} {ptr:X8}");
        if (ptr > 0x200000 && ptr < cpu.Length - 32)
            Hex(cpu, ptr, 32);
    }
    for (var at = 0; at + 4 <= cpu.Length; at += 2)
    {
        if ((at & 0x100000) != 0)
            continue;
        if (ReadBE32(cpu, at) == 0x5D9C)
            Console.WriteLine($"  long @{at:X6}");
        if (Be16(cpu, at) == 0x5D9C)
            Console.WriteLine($"  word @{at:X6}");
    }
    return 0;
}
if (args.Length >= 3 && args[2] == "def")
{
    FindDefs(cpu, zip, args[1]);
    return 0;
}
if (args.Length >= 3 && args[2] == "xref")
{
    Console.WriteLine("--- upload 5C80 ---");
    Hex(cpu, 0x5C80, 0x180);
    Console.WriteLine("--- dis 6B5C ---");
    Dis(cpu, 0x6B5C, 55);
    Console.WriteLine("--- dis 5D3C ---");
    Dis(cpu, 0x5D3C, 24);
    return 0;
}
if (args.Length >= 3 && args[2] == "bank")
{
    var bankGfx = LoadGfx(zip);
    foreach (var id in new[] { 0x1A, 0x1B })
    {
        var origin = id * 4096;
        var ranked = new List<(int Score, int Start)>();
        for (var start = origin; start + 35 < origin + 4096; start++)
        {
            var body = 0;
            var head = 0;
            var feet = 0;
            for (var col = 0; col < 5; col++)
            {
                for (var row = 0; row < 7; row++)
                {
                    var fill = Filled(bankGfx, start + col * 7 + row);
                    if (fill < 24)
                        continue;
                    body++;
                    if (row <= 1 && col is >= 1 and <= 3)
                        head++;
                    if (row >= 5)
                        feet++;
                }
            }

            if (head >= 2 && feet >= 4 && body >= 16)
                ranked.Add((body * 4 + head + feet, start));
        }

        ranked.Sort((a, b) => b.Score.CompareTo(a.Score));
        var kept = 0;
        var seen = new List<int>();
        foreach (var item in ranked)
        {
            if (seen.Any(old => Math.Abs(old - item.Start) < 20))
                continue;
            seen.Add(item.Start);
            var tiles = new int[35];
            for (var i = 0; i < tiles.Length; i++)
                tiles[i] = item.Start + i;
            var path = Path.Combine(args[1], $"pose-{id:X}-{item.Start}.png");
            DrawGrid(bankGfx, tiles, 5, 7, path);
            Console.WriteLine($"{path} score={item.Score}");
            kept++;
            if (kept == 3)
                break;
        }

        Console.WriteLine($"id {id:X} candidatos={ranked.Count} dibujos={kept}");
    }
    return 0;
}
if (args.Length >= 3 && args[2] == "col")
{
    var colGfx = LoadGfx(zip);
    HuntColumns(colGfx, args[1]);
    return 0;
}
if (args.Length >= 3 && args[2] == "base")
{
    Console.WriteLine("--- tabla FA7E8 ---");
    for (var i = 0; i < 42; i++)
    {
        var p = (int)ReadBE32(cpu, 0xFA7E8 + i * 4);
        Console.WriteLine($"{i:X2} {p:X8}");
    }

    foreach (var p in new[] { (int)ReadBE32(cpu, 0xFA7E8), (int)ReadBE32(cpu, 0xFA7E8 + 0x10 * 4) })
    {
        if (p > 0 && p < cpu.Length - 32)
            Hex(cpu, p, 32);
    }

    Console.WriteLine("--- despues de medidas ---");
    Hex(cpu, 0xFD3E0, 0x80);
    var run = 0;
    var runAt = 0;
    for (var at = 0xF0000; at + 4 < 0x100000; at += 4)
    {
        var p = (int)ReadBE32(cpu, at);
        var good = p > 0x10000 && p < cpu.Length - 32 && (p & 1) == 0;
        if (good)
        {
            if (run == 0)
                runAt = at;
            run++;
            continue;
        }

        if (run >= 16)
            Console.WriteLine($"tabla {runAt:X6} n={run} primero={ReadBE32(cpu, runAt):X8}");
        run = 0;
    }

    if (run >= 16)
        Console.WriteLine($"tabla {runAt:X6} n={run}");
    Hex(cpu, 0xFD2BE, 0x30);
    Hex(cpu, 0xFD33E, 0x20);
    Hex(cpu, 0xFB9C0, 0x40);
    Hex(cpu, 0xFB840, 0x30);
    var shown = 0;
    for (var at = 0; at + 8 < cpu.Length && shown < 20; at += 2)
    {
        if ((at & 0x100000) != 0)
            continue;
        if (Be16(cpu, at) != 0x0C40)
            continue;
        var id = Be16(cpu, at + 2);
        if (id is not (0x10 or 0x11))
            continue;
        var bra = Be16(cpu, at + 4);
        if ((bra & 0xFF00) != 0x6700)
            continue;
        var dest = (bra & 0xFF) == 0
            ? at + 6 + (short)Be16(cpu, at + 6)
            : at + 6 + (sbyte)(bra & 0xFF);
        Console.WriteLine($"id {id:X} @{at:X6} -> {dest:X6}");
        if (dest > 0 && dest < cpu.Length - 48)
            Hex(cpu, dest, 48);
        shown++;
    }

    Console.WriteLine($"casos={shown}");
    return 0;
    var shownDead = 0;
    for (var at = 0; at + 6 < cpu.Length && shownDead < 30; at += 2)
    {
        if ((at & 0x100000) != 0)
            continue;
        var op = Be16(cpu, at);
        var immAt = -1;
        if (op is 0x0C40 or 0x0C41 or 0x0C80)
            immAt = at + 2;
        else if (op == 0x0C6C)
            immAt = at + 2;
        else
            continue;
        var imm = Be16(cpu, immAt);
        if (imm is not (0x25 or 0x26 or 0x38 or 0x27))
            continue;
        Console.WriteLine($"cmp @{at:X6} #{imm:X} op={op:X4}");
        Hex(cpu, at, 16);
        shown++;
    }

    Console.WriteLine($"cmp={shown}");
    return 0;
    for (var at = 0; at + 6 < cpu.Length && shown < 25; at += 2)
    {
        if ((at & 0x100000) != 0)
            continue;
        if (Be16(cpu, at) != 0x28BC)
            continue;
        var entry = (int)ReadBE32(cpu, at + 2);
        if (entry < 0x1000 || entry >= cpu.Length - 0x80)
            continue;
        var usesId = false;
        var spawns = false;
        for (var i = 0; i < 0x60; i += 2)
        {
            if (Be16(cpu, entry + i) == 0x302C && Be16(cpu, entry + i + 2) == 0x00D2)
                usesId = true;
            if (Be16(cpu, entry + i) == 0x4EB9 && ReadBE32(cpu, entry + i + 2) is 0x5D76 or 0x5D9C or 0x6FA0)
                spawns = true;
        }

        if (!usesId || !spawns)
            continue;
        Console.WriteLine($"estado @{at:X6} -> {entry:X6}");
        shown++;
    }

    Console.WriteLine($"estados={shown}");
    return 0;
    var shownOld = 0;
    foreach (var target in new[] { 0x5FA0, 0x5F22, 0x5F06, 0x6822, 0x6BC0 })
    {
        for (var at = 0; at + 6 < cpu.Length && shown < 40; at += 2)
        {
            if ((at & 0x100000) != 0)
                continue;
            var op = Be16(cpu, at);
            var dest = -1;
            if (op is 0x4EB9 or 0x4EF9)
                dest = (int)ReadBE32(cpu, at + 2);
            else if (op is 0x4EBA or 0x6100 or 0x6000)
                dest = at + 2 + (short)Be16(cpu, at + 2);
            else if ((op & 0xF1FF) == 0x41FA)
                dest = at + 2 + (short)Be16(cpu, at + 2);
            if (dest != target)
                continue;
            Console.WriteLine($"@{at:X6} -> {target:X}");
            shown++;
        }
    }

    Console.WriteLine($"llamadas={shown}");
    return 0;
    Console.WriteLine("--- 6A218 ---");
    for (var i = 0; i < 24; i++)
    {
        var at = 0x6A218 + i * 8;
        Console.WriteLine($"{i:X2} {Be16(cpu, at):X4} {Be16(cpu, at + 2):X4} {Be16(cpu, at + 4):X4} {Be16(cpu, at + 6):X4}");
    }

    Console.WriteLine("--- 70986 ---");
    for (var i = 0; i < 24; i++)
    {
        var at = 0x70986 + i * 8;
        Console.WriteLine($"{i:X2} {Be16(cpu, at):X4} {Be16(cpu, at + 2):X4} {Be16(cpu, at + 4):X4} {Be16(cpu, at + 6):X4}");
    }
    var reads = 0;
    for (var at = 0; at + 8 < cpu.Length; at += 2)
    {
        if ((at & 0x100000) != 0)
            continue;
        if ((Be16(cpu, at) & 0xF1FF) != 0x302C || Be16(cpu, at + 2) != 0x00D2)
            continue;
        var shifted = false;
        var lea = -1;
        for (var i = 4; i < 24; i += 2)
        {
            var op = Be16(cpu, at + i);
            if ((op & 0xF0C0) == 0xE0C0 || (op & 0xF1F8) == 0xE148)
                shifted = true;
            if (op == 0x41F9 || op == 0x43F9 || op == 0x45F9)
                lea = (int)ReadBE32(cpu, at + i + 2);
        }

        if (!shifted && lea < 0)
            continue;
        reads++;
        Console.WriteLine($"@{at:X6} lea={(lea < 0 ? "-" : lea.ToString("X"))}");
    }

    Console.WriteLine($"con tabla={reads}");
    return 0;
}
if (args.Length >= 3 && args[2] == "set")
{
    var poseGfx = LoadGfx(zip);
    var frames = 0;
    for (var at = 0; at + 96 < cpu.Length && frames < 6; at += 2)
    {
        if ((at & 0x100000) != 0)
            continue;
        var tiles = new List<int>();
        var baseTile = -1;
        for (var i = 0; i < 16; i++)
        {
            var low = Be16(cpu, at + i * 4);
            var attr = Be16(cpu, at + 2 + i * 4);
            var high = (attr >> 4) & 0xF;
            if (high == 0)
                break;
            var tile = low | (high << 16);
            if (tile < 70000 || tile > 130000 || Filled(poseGfx, tile) < 8)
                break;
            if (baseTile < 0)
                baseTile = tile;
            if (Math.Abs(tile - baseTile) > 4000)
                break;
            tiles.Add(tile);
        }

        if (tiles.Count < 10)
            continue;
        var path = Path.Combine(args[1], $"list-{at:X}.png");
        DrawGrid(poseGfx, tiles.ToArray(), 1, tiles.Count, path);
        Console.WriteLine($"{path} n={tiles.Count} tile={tiles[0]}");
        Hex(cpu, at, 40);
        frames++;
        at += tiles.Count * 4;
    }

    Console.WriteLine($"listas={frames}");
    return 0;
}
if (args.Length >= 3 && args[2] == "slots")
{
    Directory.CreateDirectory(args[1]);
    var slotGfx = LoadGfx(zip);
    ScanSlots(cpu, slotGfx, args[1]);
    return 0;
}
if (args.Length >= 3 && args[2] == "boot")
{
    Console.WriteLine("--- refs 800100 ---");
    var refs = 0;
    for (var at = 0; at + 4 <= cpu.Length; at += 2)
    {
        if (ReadBE32(cpu, at) != 0x00800100)
            continue;
        if ((at & 0x100000) != 0)
            continue;
        Console.WriteLine($"  @{at:X6}");
        refs++;
    }

    Console.WriteLine($"refs={refs}");
    Hex(cpu, 0xC5F0, 0x60);
    Hex(cpu, 0xA46F0, 0x40);
    Hex(cpu, 0xA4720, 0x120);
    Hex(cpu, 0x2A8080, 0x40);
    Hex(cpu, 0x2A90A0, 0x40);
    var bootGfx = LoadGfx(zip);
    var column = new int[18];
    for (var i = 0; i < column.Length; i++)
        column[i] = 0xB852 + i;
    DrawGrid(bootGfx, column, 1, column.Length, Path.Combine(args[1], "col-b852.png"));
    var block = new int[6 * 12];
    for (var i = 0; i < block.Length; i++)
        block[i] = 0xB820 + i;
    DrawGrid(bootGfx, block, 6, 12, Path.Combine(args[1], "block-b820.png"));
    Console.WriteLine("dibujos listos");
    return 0;
}
if (args.Length >= 3 && args[2] == "hunt")
{
    Directory.CreateDirectory(args[1]);
    var huntGfx = LoadGfx(zip);
    HuntBodies(huntGfx, args[1]);
    return 0;
}
if (args.Length >= 3 && args[2] == "cpu")
{
    FindPointerTables(cpu);
    Console.WriteLine("--- handlers 01B164 ---");
    for (var i = 0; i < 38; i++)
    {
        var ptr = ReadBE32(cpu, 0x01B164 + i * 4);
        Console.WriteLine($"{i:X2} {ptr:X8}");
    }

    Hex(cpu, (int)ReadBE32(cpu, 0x01B164 + 0x10 * 4), 96);
    Hex(cpu, (int)ReadBE32(cpu, 0x01B164 + 0x11 * 4), 96);
    Hex(cpu, 0x071900, 0x80);
    return 0;
}

var gfx = new byte[0x4000000];
for (var pair = 0; pair < 4; pair++)
{
    var odd = Read(zip, $"svc-c{pair * 2 + 1}.bin");
    var even = Read(zip, $"svc-c{pair * 2 + 2}.bin");
    var baseOff = pair * 0x1000000;
    for (var i = 0; i < odd.Length; i++)
    {
        gfx[baseOff + i * 2] = odd[i];
        gfx[baseOff + i * 2 + 1] = even[i];
    }
}

CxDecrypt(gfx);
if (args.Length >= 3 && args[2] == "scan")
{
    ScanStrips(cpu, gfx, args[1]);
    return 0;
}
for (var pair = 0; pair < 4; pair++)
{
    var slice = new byte[0x1000000];
    Buffer.BlockCopy(gfx, pair * 0x1000000, slice, 0, slice.Length);
    var start = FirstDense(slice);
    Console.WriteLine($"par {pair} denso={start}");
    WriteSheet(slice, Path.Combine(args[1], $"survey-{pair}.png"), 0, 64, 20, Math.Max(1, (slice.Length / 128) / (64 * 20)), false);
    WriteSheet(slice, Path.Combine(args[1], $"cols-{pair}-{start}.png"), start, 10, 18, 1, true);
}
foreach (var start in new[] { 70000, 90000, 110000 })
    WriteSheet(Slice(gfx, 0), Path.Combine(args[1], $"body-0-{start}.png"), start, 12, 16, 1, true);
foreach (var start in new[] { 20000, 60000, 100000 })
    WriteSheet(Slice(gfx, 1), Path.Combine(args[1], $"body-1-{start}.png"), start, 12, 16, 1, true);

var hits = FindFrames(cpu, gfx);
Console.WriteLine($"grupos={hits.Count}");
for (var n = 0; n < Math.Min(6, hits.Count); n++)
{
    var hit = hits[n];
    var path = Path.Combine(args[1], $"frame-{n}-{hit.Offset:X}.png");
    DrawFrame(gfx, hit, path);
    Console.WriteLine($"{path} partes={hit.Parts.Count} tile={hit.Parts[0].Tile}");
}

return 0;

static List<Frame> FindFrames(byte[] cpu, byte[] gfx)
{
    var hits = new List<Frame>();
    for (var at = 0; at + 16 < cpu.Length; at += 2)
    {
        var count = Be16(cpu, at);
        if (count is < 6 or > 28)
            continue;
        if (at + 2 + count * 8 > cpu.Length)
            continue;
        var parts = new List<Part>();
        var ok = true;
        for (var i = 0; i < count; i++)
        {
            var o = at + 2 + i * 8;
            var x = Signed16(cpu, o);
            var y = Signed16(cpu, o + 2);
            var tileLow = Be16(cpu, o + 4);
            var attr = Be16(cpu, o + 6);
            var tile = tileLow | (((attr >> 4) & 0xF) << 16);
            if (x is < -180 or > 180 || y is < -220 or > 140 || tile < 4096)
            {
                ok = false;
                break;
            }

            parts.Add(new Part(x, y, tile, attr));
        }

        if (!ok)
            continue;
        var minX = parts.Min(part => part.X);
        var maxX = parts.Max(part => part.X);
        var minY = parts.Min(part => part.Y);
        var maxY = parts.Max(part => part.Y);
        var minT = parts.Min(part => part.Tile);
        var maxT = parts.Max(part => part.Tile);
        if (maxX - minX is < 32 or > 220 || maxY - minY is < 48 or > 240 || maxT - minT > 20000)
            continue;
        var filled = parts.Count(part => Filled(gfx, part.Tile) >= 20);
        if (filled < count - 2)
            continue;
        hits.Add(new Frame(at, parts));
        at += count * 8;
        if (hits.Count >= 8)
            break;
    }

    return hits;
}

static void DrawFrame(byte[] gfx, Frame frame, string path)
{
    var minX = frame.Parts.Min(part => part.X);
    var minY = frame.Parts.Min(part => part.Y);
    var maxX = frame.Parts.Max(part => part.X) + 16;
    var maxY = frame.Parts.Max(part => part.Y) + 16;
    var w = Math.Max(16, maxX - minX);
    var h = Math.Max(16, maxY - minY);
    var rgba = new byte[w * h * 4];
    foreach (var part in frame.Parts)
    {
        var tile = DecodeTile(gfx, part.Tile);
        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 16; x++)
            {
                var n = tile[y * 16 + x];
                if (n == 0)
                    continue;
                var dx = part.X - minX + x;
                var dy = part.Y - minY + y;
                if ((uint)dx >= (uint)w || (uint)dy >= (uint)h)
                    continue;
                var o = (dy * w + dx) * 4;
                var g = (byte)(n * 17);
                rgba[o] = g;
                rgba[o + 1] = g;
                rgba[o + 2] = g;
                rgba[o + 3] = 255;
            }
        }
    }

    WritePng(path, w, h, rgba);
}

static int Be16(byte[] rom, int offset) => (rom[offset] << 8) | rom[offset + 1];

static int Signed16(byte[] rom, int offset)
{
    var value = Be16(rom, offset);
    return value >= 0x8000 ? value - 0x10000 : value;
}

static byte[] Slice(byte[] gfx, int pair)
{
    var slice = new byte[0x1000000];
    Buffer.BlockCopy(gfx, pair * 0x1000000, slice, 0, slice.Length);
    return slice;
}

static void Dis(byte[] rom, int pc, int count)
{
    for (var n = 0; n < count && pc + 1 < rom.Length; n++)
    {
        var op = Be16(rom, pc);
        var len = 2;
        string text;
        if (op == 0x4E75) text = "rts";
        else if (op == 0x4E71) text = "nop";
        else if ((op & 0xFFF8) == 0x42A8)
        {
            var d = (short)Be16(rom, pc + 2);
            text = $"clr.l ${d:X}(a{op & 7})";
            len = 4;
        }
        else if ((op & 0xFFF8) == 0x0640)
        {
            var imm = Be16(rom, pc + 2);
            text = $"addi.w #{imm:X},d{op & 7}";
            len = 4;
        }
        else if (((op & 0xF100) == 0xE100 || (op & 0xF100) == 0xE900) && (op & 0x00C0) != 0x00C0)
            text = $"shift ${(op >> 9) & 7},d{op & 7}";
        else if ((op & 0xF000) == 0x7000 && (op & 0x0100) == 0)
            text = $"moveq #{(sbyte)(op & 0xFF)},d{(op >> 9) & 7}";
        else if ((op & 0xF1F8) == 0x41E8)
        {
            var d16 = (short)Be16(rom, pc + 2);
            text = $"lea ${d16:X}(a{op & 7}),a{(op >> 9) & 7}";
            len = 4;
        }
        else if ((op & 0xF1FF) == 0x41F9)
        {
            var abs = ReadBE32(rom, pc + 2);
            text = $"lea ${abs:X}.l,a{(op >> 9) & 7}";
            len = 6;
        }
        else if ((op & 0xF1F8) == 0x41F8)
        {
            var d = (short)Be16(rom, pc + 2);
            text = $"lea ${pc + 2 + d:X}(pc),a{(op >> 9) & 7}";
            len = 4;
        }
        else if ((op & 0xFFC0) == 0x4E80)
        {
            var d = (short)Be16(rom, pc + 2);
            text = $"jsr ${pc + 2 + d:X}";
            len = 4;
        }
        else if ((op & 0xFF00) == 0x6100)
        {
            var d = (short)Be16(rom, pc + 2);
            text = $"bsr ${pc + 2 + d:X}";
            len = 4;
        }
        else if ((op & 0xF000) == 0x6000 && (op & 0x00FF) != 0)
        {
            var d = (sbyte)(op & 0xFF);
            text = $"bra ${pc + 2 + d:X}";
        }
        else if ((op & 0xF0F8) == 0x50C8)
        {
            var d = (short)Be16(rom, pc + 2);
            text = $"dbra d{op & 7},${pc + 2 + d:X}";
            len = 4;
        }
        else if ((op & 0xC000) == 0 && ((op >> 12) & 3) != 0)
        {
            var sizeCode = (op >> 12) & 3;
            var size = sizeCode == 1 ? 1 : sizeCode == 2 ? 4 : 2;
            var srcMode = (op >> 3) & 7;
            var srcReg = op & 7;
            var dstReg = (op >> 9) & 7;
            var dstMode = (op >> 6) & 7;
            var at = pc + 2;
            var src = Ea(rom, ref at, srcMode, srcReg, size);
            var dst = Ea(rom, ref at, dstMode, dstReg, size);
            text = $"move.{(size == 1 ? "b" : size == 4 ? "l" : "w")} {src},{dst}";
            len = at - pc;
        }
        else
            text = $"dc.w ${op:X4}";

        Console.WriteLine($"{pc:X6}  {text}");
        pc += len;
    }
}

static string Ea(byte[] rom, ref int at, int mode, int reg, int size)
{
    switch (mode)
    {
        case 0: return $"d{reg}";
        case 1: return $"a{reg}";
        case 2: return $"(a{reg})";
        case 3: return $"(a{reg})+";
        case 4: return $"-(a{reg})";
        case 5:
            var d16 = (short)Be16(rom, at);
            at += 2;
            return $"${d16:X}(a{reg})";
        case 6:
            at += 2;
            return $"(a{reg},idx)";
        case 7:
            if (reg == 0)
            {
                var abs = (short)Be16(rom, at);
                at += 2;
                return $"${abs:X}.w";
            }
            if (reg == 1)
            {
                var abs = ReadBE32(rom, at);
                at += 4;
                return $"${abs:X}.l";
            }
            if (reg == 4)
            {
                if (size == 4)
                {
                    var imm = ReadBE32(rom, at);
                    at += 4;
                    return $"#{imm:X}";
                }
                var w = Be16(rom, at);
                at += 2;
                return $"#{w:X}";
            }
            at += 2;
            return "ea7";
        default:
            return "?";
    }
}

static void FindCalls(byte[] cpu, int target)
{
    var shown = 0;
    for (var at = 0; at + 6 < cpu.Length && shown < 15; at += 2)
    {
        if ((at & 0x100000) != 0)
            continue;
        var op = Be16(cpu, at);
        var hit = false;
        if (op == 0x4EB9 && ReadBE32(cpu, at + 2) == target)
            hit = true;
        if (op == 0x6100)
        {
            var disp = (short)Be16(cpu, at + 2);
            if (at + 2 + disp == target)
                hit = true;
        }

        if (!hit)
            continue;
        Console.WriteLine($"call @{at:X6}");
        Hex(cpu, Math.Max(0, at - 16), 24);
        shown++;
    }

    Console.WriteLine($"llamadas={shown}");
}

static void TraceSpritePointer(byte[] cpu)
{
    Console.WriteLine("-- escrituras a +3C/+80 --");
    for (var at = 0; at + 4 < cpu.Length; at += 2)
    {
        var op = Be16(cpu, at);
        var disp = Be16(cpu, at + 2);
        if (disp != 0x3C && disp != 0x80 && disp != 0x84)
            continue;
        var store = op is >= 0x2940 and <= 0x297F;
        var load = op is 0x202C or 0x206C or 0x222C or 0x226C;
        if (!store && !load)
            continue;
        if ((at & 0x100000) != 0)
            continue;
        Console.WriteLine($"{at:X6} {(store ? "guarda" : "lee")} +{disp:X} op={op:X4}");
    }

    Console.WriteLine("-- base 10A000 --");
    for (var at = 0; at + 4 <= cpu.Length; at += 2)
    {
        if ((at & 0x100000) != 0)
            continue;
        if (ReadBE32(cpu, at) == 0x0010A000)
            Console.WriteLine($"  @{at:X6}");
    }
}

static void HuntBodies(byte[] gfx, string folder)
{
    var maxTile = gfx.Length / 128;
    var found = new List<(int Score, int Tile, int Width, int Height)>();
    foreach (var height in new[] { 10, 12, 14 })
    {
        foreach (var width in new[] { 4, 5, 6 })
        {
            var span = width * height;
            for (var tile = 20000; tile + span < maxTile; tile += 8)
            {
                var score = BodyScore(gfx, tile, width, height);
                if (score < width * height * 5)
                    continue;
                found.Add((score, tile, width, height));
            }
        }
    }

    found.Sort((a, b) => b.Score.CompareTo(a.Score));
    var kept = new List<(int Score, int Tile, int Width, int Height)>();
    foreach (var item in found)
    {
        var near = false;
        foreach (var old in kept)
        {
            if (Math.Abs(old.Tile - item.Tile) < 48)
            {
                near = true;
                break;
            }
        }

        if (near)
            continue;
        kept.Add(item);
        if (kept.Count == 10)
            break;
    }

    var n = 0;
    foreach (var item in kept)
    {
        var tiles = new int[item.Width * item.Height];
        for (var i = 0; i < tiles.Length; i++)
            tiles[i] = item.Tile + i;
        var path = Path.Combine(folder, $"body-{n}-{item.Tile}.png");
        DrawGrid(gfx, tiles, item.Width, item.Height, path);
        Console.WriteLine($"{path} score={item.Score} {item.Width}x{item.Height}");
        n++;
    }
}

static int BodyScore(byte[] gfx, int start, int width, int height)
{
    var body = 0;
    var empty = 0;
    var top = 0;
    var bottom = 0;
    for (var col = 0; col < width; col++)
    {
        for (var row = 0; row < height; row++)
        {
            var fill = QuickFill(gfx, start + col * height + row);
            if (fill >= 3 && fill <= 14)
                body++;
            if (fill == 0)
                empty++;
            if (row < 3 && fill >= 2)
                top++;
            if (row >= height - 3 && fill >= 2)
                bottom++;
        }
    }

    if (top < 2 || bottom < 2 || empty > width * height / 3)
        return 0;
    return body * 8 + top + bottom - empty;
}

static int QuickFill(byte[] gfx, int tile)
{
    var p = tile * 128;
    if ((uint)p >= (uint)gfx.Length)
        return 0;
    var n = 0;
    for (var i = 0; i < 128; i += 8)
    {
        if (gfx[p + i] != 0)
            n++;
    }

    return n;
}

static void ScanSlots(byte[] cpu, byte[] gfx, string folder)
{
    const int table = 0xA4720;
    var drawn = 0;
    for (var slot = 0; slot < 38; slot++)
    {
        var rec = table + slot * 16;
        Console.Write($"{slot:X2} {rec:X6}");
        for (var i = 0; i < 16; i++)
            Console.Write($" {cpu[rec + i]:X2}");
        Console.WriteLine();
        for (var off = 0; off <= 12; off += 4)
        {
            var ptr = ReadBE32(cpu, rec + off);
            if (ptr < 0x10000 || ptr >= cpu.Length - 64 || (ptr & 1) != 0)
                continue;
            var tiles = ReadTileRun(cpu, ptr, 24);
            if (tiles.Count < 8)
                continue;
            var path = Path.Combine(folder, $"slot-{slot:X2}-{ptr:X}.png");
            DrawGrid(gfx, tiles.ToArray(), 1, tiles.Count, path);
            Console.WriteLine($"  ptr {ptr:X8} -> {path} tile={tiles[0]:X}");
            drawn++;
        }
    }

    Console.WriteLine($"tiras={drawn}");
}

static List<int> ReadTileRun(byte[] cpu, int ptr, int max)
{
    var tiles = new List<int>();
    for (var i = 0; i < max; i++)
    {
        var tile = Be16(cpu, ptr + i * 4);
        var attr = Be16(cpu, ptr + i * 4 + 2);
        if (tile < 0x20 || attr == 0 || attr == 0xFFFF)
            break;
        tiles.Add(tile);
    }

    return tiles;
}

static void FindDefs(byte[] cpu, ZipArchive zip, string folder)
{
    Hex(cpu, 0x100, 0x30);
    Hex(cpu, 0x200, 0x20);
    Hex(cpu, 0x400100, 0x30);
    Hex(cpu, 0x400200, 0x20);
    Hex(cpu, 0xC440, 0x50);
    Hex(cpu, 0xDCE8, 0x40);
    Hex(cpu, 0xA565E, 0x20);
    Hex(cpu, 0xA56DE, 0x20);
    var shown = 0;
    for (var at = 0; at + 4 < cpu.Length && shown < 10; at += 2)
    {
        if ((at & 0x100000) != 0)
            continue;
        if (Be16(cpu, at) != 0x302C || Be16(cpu, at + 2) != 0x00D2)
            continue;
        Console.WriteLine($"id @{at:X6}");
        Dis(cpu, at, 18);
        shown++;
    }
    var tables = new List<int>();
    for (var at = 0; at + 38 * 4 < cpu.Length; at += 4)
    {
        if ((at & 0x100000) != 0)
            continue;
        var good = 0;
        var unique = new HashSet<int>();
        for (var i = 0; i < 38; i++)
        {
            var p = ReadBE32(cpu, at + i * 4);
            unique.Add(p);
            if (IsDef(cpu, p))
                good++;
        }

        if (good < 30 || unique.Count < 30)
            continue;
        var ryu = ReadBE32(cpu, at + 0x10 * 4);
        var ken = ReadBE32(cpu, at + 0x11 * 4);
        if (ryu == ken || !IsDef(cpu, ryu) || !IsDef(cpu, ken))
            continue;
        tables.Add(at);
        Console.WriteLine($"tabla {at:X6} buenas={good} ryu={ryu:X8} ken={ken:X8}");
        DumpDef(cpu, "ryu", ryu);
        DumpDef(cpu, "ken", ken);
        at += 37 * 4;
        if (tables.Count >= 8)
            break;
    }

    Console.WriteLine($"tablas={tables.Count}");
    if (tables.Count == 0)
        return;

    var gfx = LoadGfx(zip);
    foreach (var at in tables)
    {
        var ryu = ReadBE32(cpu, at + 0x10 * 4);
        var stream = ReadBE32(cpu, ryu + 0x18);
        TryDraw(cpu, gfx, stream, Path.Combine(folder, $"ryu-{at:X}.png"));
    }
}

static void DumpDef(byte[] cpu, string name, int def)
{
    if (!IsDef(cpu, def))
    {
        Console.WriteLine($"  {name} no es definicion");
        return;
    }

    var stream = ReadBE32(cpu, def + 0x18);
    Console.WriteLine($"  {name} +18={stream:X8}");
    Hex(cpu, stream, 48);
}

static bool DrawBank(byte[] cpu, byte[] gfx, int bank, string path)
{
    var best = -1;
    var bestScore = 0;
    for (var frame = 0; frame < 24; frame++)
    {
        var at = bank + frame * 1024;
        if (at < 0 || at + 1024 > cpu.Length)
            break;
        var score = 0;
        var distinct = 0;
        var seen = new HashSet<int>();
        for (var i = 0; i < 128; i++)
        {
            var low = Be16(cpu, at + i * 4);
            var attr = Be16(cpu, at + 2 + i * 4);
            var tile = low | (((attr >> 4) & 0xF) << 16);
            if ((uint)tile >= (uint)(gfx.Length / 128))
                continue;
            if (Filled(gfx, tile) < 6)
                continue;
            if (seen.Add(tile))
                distinct++;
            score++;
        }

        if (score > bestScore && distinct > 8)
        {
            bestScore = score;
            best = frame;
        }
    }

    if (best < 0 || bestScore < 20)
        return false;
    var origin = bank + best * 1024;
    var tiles = new int[128];
    for (var i = 0; i < tiles.Length; i++)
    {
        var low = Be16(cpu, origin + i * 4);
        var attr = Be16(cpu, origin + 2 + i * 4);
        tiles[i] = low | (((attr >> 4) & 0xF) << 16);
    }

    DrawGrid(gfx, tiles, 8, 16, path);
    Console.WriteLine($"  {path} frame={best} llenos={bestScore}");
    return true;
}

static bool TryFrame(byte[] cpu, byte[] gfx, int stream, string path)
{
    foreach (var skipHeader in new[] { 8, 0 })
    {
        var at = stream + skipHeader;
        if (at < 0 || at + 8 >= cpu.Length)
            continue;
        var width = cpu[at] + 1;
        var height = cpu[at + 1] + 1;
        if (width is < 2 or > 10 || height is < 4 or > 20)
            continue;
        var cells = width * height;
        if (at + 4 + cells * 4 >= cpu.Length)
            continue;
        var tiles = new int[cells];
        var filled = 0;
        for (var i = 0; i < cells; i++)
        {
            var low = Be16(cpu, at + 4 + i * 4);
            var attr = Be16(cpu, at + 6 + i * 4);
            var tile = low | (((attr >> 4) & 0xF) << 16);
            if (tile <= 0 || tile >= gfx.Length / 128)
                tile = low;
            tiles[i] = tile;
            if (Filled(gfx, tile) >= 8)
                filled++;
        }

        if (filled < cells / 3)
            continue;
        DrawGrid(gfx, tiles, width, height, path);
        Console.WriteLine($"  {path} header={skipHeader} {width}x{height} llenos={filled}");
        return true;
    }

    return false;
}

static bool TryDraw(byte[] cpu, byte[] gfx, int stream, string path)
{
    if (stream < 0 || stream + 64 >= cpu.Length)
        return false;
    foreach (var skipHeader in new[] { 0, 8 })
    {
        var at = stream + skipHeader;
        var width = cpu[at] + 1;
        var height = cpu[at + 1] + 1;
        if (width is < 2 or > 12 || height is < 4 or > 24)
            continue;
        var cells = width * height;
        if (at + 4 + cells * 4 >= cpu.Length)
            continue;
        var tiles = new int[cells];
        var filled = 0;
        for (var i = 0; i < cells; i++)
        {
            var low = Be16(cpu, at + 4 + i * 4);
            var attr = Be16(cpu, at + 6 + i * 4);
            var tile = low | (((attr >> 4) & 0xF) << 16);
            if (tile <= 0 || tile >= gfx.Length / 128)
                tile = low;
            tiles[i] = tile;
            if (Filled(gfx, tile) >= 10)
                filled++;
        }

        if (filled < cells / 2)
            continue;
        DrawGrid(gfx, tiles, width, height, path);
        Console.WriteLine($"  {path} header={skipHeader} {width}x{height} llenos={filled}");
        return true;
    }

    return false;
}

static byte[] LoadGfx(ZipArchive zip)
{
    var gfx = new byte[0x4000000];
    for (var pair = 0; pair < 4; pair++)
    {
        var odd = Read(zip, $"svc-c{pair * 2 + 1}.bin");
        var even = Read(zip, $"svc-c{pair * 2 + 2}.bin");
        var baseOff = pair * 0x1000000;
        for (var i = 0; i < odd.Length; i++)
        {
            gfx[baseOff + i * 2] = odd[i];
            gfx[baseOff + i * 2 + 1] = even[i];
        }
    }

    CxDecrypt(gfx);
    return gfx;
}

static bool IsDef(byte[] cpu, int p)
{
    if (p < 0x1000 || p > cpu.Length - 0x40 || (p & 1) != 0)
        return false;
    return RomPtr(cpu, ReadBE32(cpu, p + 0x18))
        && RomPtr(cpu, ReadBE32(cpu, p + 0x20))
        && RomPtr(cpu, ReadBE32(cpu, p + 0x28));
}

static bool RomPtr(byte[] cpu, int value) =>
    value >= 0x1000 && value < cpu.Length - 0x40 && (value & 1) == 0;

static void SearchXrefs(byte[] cpu)
{
    int[] needles =
    {
        0x0010A239, 0x0010A131, 0x0010A1E8, 0x0010A000,
        0x003C0000, 0x003C0002, 0x003C0004, 0x003C0006
    };
    foreach (var needle in needles)
    {
        var shown = 0;
        Console.WriteLine($"-- {needle:X8} --");
        for (var at = 0; at + 4 <= cpu.Length && shown < 12; at += 2)
        {
            if (ReadBE32(cpu, at) != needle)
                continue;
            Console.WriteLine($"  @{at:X6}");
            Hex(cpu, Math.Max(0, at - 12), 28);
            shown++;
        }

        Console.WriteLine($"  mostrados={shown}");
    }
}

static void ReportCpu(byte[] cpu)
{
    Console.Write("vector:");
    for (var i = 0; i < 16; i++)
        Console.Write($" {cpu[i]:X2}");
    Console.WriteLine();
    var ssp = ReadBE32(cpu, 0);
    var pc = ReadBE32(cpu, 4);
    Console.WriteLine($"ssp={ssp:X8} pc={pc:X8}");
    var rts = Count(cpu, 0x4E, 0x75);
    var rtsSwap = Count(cpu, 0x75, 0x4E);
    var jsr = Count(cpu, 0x4E, 0xB9);
    Console.WriteLine($"rts={rts} rts_swap={rtsSwap} jsr={jsr}");
    DumpNames(cpu);
    Hex(cpu, 0xB1180, 0x40);
    Console.WriteLine("--- lista cerca de RYU en 0E40C0 ---");
    Hex(cpu, 0x0E40C0, 0x180);
    Console.WriteLine("--- lista cerca de KEN en 0E4B70 ---");
    Hex(cpu, 0x0E4B70, 0xC0);
}

static void Hex(byte[] rom, int offset, int length)
{
    for (var at = offset; at < offset + length && at < rom.Length; at += 16)
    {
        Console.Write($"{at:X6}");
        var text = new char[16];
        for (var i = 0; i < 16; i++)
        {
            var c = at + i < rom.Length ? rom[at + i] : (byte)0;
            Console.Write($" {c:X2}");
            text[i] = c is >= 32 and < 127 ? (char)c : '.';
        }

        Console.WriteLine("  " + new string(text));
    }
}

static void Find(byte[] rom, ReadOnlySpan<byte> needle)
{
    for (var i = 0; i < rom.Length - needle.Length; i++)
    {
        var ok = true;
        for (var n = 0; n < needle.Length; n++)
        {
            if (rom[i + n] != needle[n])
            {
                ok = false;
                break;
            }
        }

        if (ok)
            Console.WriteLine($"{System.Text.Encoding.ASCII.GetString(needle)} @ {i:X6}");
    }
}

static void DumpNames(byte[] cpu)
{
    var start = 0xB1000;
    var end = Math.Min(cpu.Length, start + 0x400);
    Console.WriteLine("--- nombres ---");
    for (var at = start; at + 8 <= end; at += 8)
    {
        var letters = 0;
        for (var i = 0; i < 8; i++)
        {
            var c = cpu[at + i];
            if (c == 0)
                break;
            if (c is >= (byte)'A' and <= (byte)'Z' or (byte)' ' or (byte)'-')
                letters++;
            else
            {
                letters = -1;
                break;
            }
        }

        if (letters >= 2)
            Console.WriteLine($"{at:X6} {System.Text.Encoding.ASCII.GetString(cpu, at, 8).Replace("\0", " ")}");
    }

    Console.WriteLine("--- punteros cerca de la tabla ---");
    for (var at = 0xB0E00; at + 4 <= 0xB1800; at += 4)
    {
        var value = ReadBE32(cpu, at);
        if (value > 0x100 && value < cpu.Length - 16 && (value & 1) == 0)
            Console.WriteLine($"{at:X6} -> {value:X8}");
    }
}

static void Patch(byte[] cpu)
{
    WriteBE16(cpu, 0x9e90, 0x000f);
    WriteBE16(cpu, 0x9e92, 0xc9c0);
    WriteBE16(cpu, 0xa10c, 0x4eb9);
    WriteBE16(cpu, 0xa10e, 0x000e);
    WriteBE16(cpu, 0xa110, 0x9750);
}

static void PxDecrypt(byte[] src)
{
    var dst = (byte[])src.Clone();
    int[] sec = { 0x06, 0x07, 0x01, 0x02, 0x03, 0x04, 0x05, 0x00 };
    var words = src.Length / 2;
    for (var i = 0; i < words; i++)
    {
        var ofst = BitSwap(i & 0x007fff, 0x0f, 0x00, 0x08, 0x09, 0x0b, 0x0a, 0x0c, 0x0d, 0x04, 0x03, 0x01, 0x07, 0x06, 0x02, 0x05, 0x0e);
        ofst += i & 0x078000;
        ofst += sec[(i & 0xf80000) >> 19] << 19;
        src[i * 2] = dst[ofst * 2];
        src[i * 2 + 1] = dst[ofst * 2 + 1];
    }
}

static void CxDecrypt(byte[] rom)
{
    var dst = (byte[])rom.Clone();
    byte[] idxTbl = { 0, 1, 0, 1, 2, 3, 2, 3, 3, 4, 3, 4, 4, 5, 4, 5 };
    int[][] bits =
    {
        new[] { 3, 0, 1, 2 },
        new[] { 2, 3, 0, 1 },
        new[] { 1, 2, 3, 0 },
        new[] { 0, 1, 2, 3 },
        new[] { 3, 2, 1, 0 },
        new[] { 3, 0, 2, 1 }
    };
    var tiles = rom.Length / 0x80;
    for (var i = 0; i < tiles; i++)
    {
        var idx = idxTbl[(i & 0xf00) >> 8];
        var ofst = BitSwap(i & 0xff, 7, 6, 5, 4, bits[idx][0], bits[idx][1], bits[idx][2], bits[idx][3]);
        ofst += i & 0x0fffff00;
        Buffer.BlockCopy(dst, ofst * 0x80, rom, i * 0x80, 0x80);
    }
}

static int BitSwap(int val, params int[] srcFromMsb)
{
    var result = 0;
    var n = srcFromMsb.Length;
    for (var dest = 0; dest < n; dest++)
        result |= ((val >> srcFromMsb[dest]) & 1) << (n - 1 - dest);
    return result;
}

static void WordSwap(byte[] rom)
{
    for (var i = 0; i < rom.Length; i += 2)
        (rom[i], rom[i + 1]) = (rom[i + 1], rom[i]);
}

static int Count(byte[] rom, byte a, byte b)
{
    var n = 0;
    for (var i = 0; i < rom.Length - 1; i += 2)
    {
        if (rom[i] == a && rom[i + 1] == b)
            n++;
    }

    return n;
}

static int ReadBE32(byte[] rom, int offset) =>
    (rom[offset] << 24) | (rom[offset + 1] << 16) | (rom[offset + 2] << 8) | rom[offset + 3];

static void WriteBE16(byte[] rom, int offset, int value)
{
    rom[offset] = (byte)(value >> 8);
    rom[offset + 1] = (byte)value;
}

static int FirstDense(byte[] rom)
{
    var tiles = rom.Length / 128;
    var run = 0;
    for (var tile = 0; tile < tiles; tile++)
    {
        run = Filled(rom, tile) >= 40 ? run + 1 : 0;
        if (run == 12)
            return Math.Max(0, tile - 11);
    }

    return 0;
}

static int Filled(byte[] rom, int tile)
{
    var p = tile * 128;
    if (tile < 0 || p < 0 || p + 128 > rom.Length)
        return 0;
    var n = 0;
    for (var i = 0; i < 128; i++)
    {
        if (rom[p + i] != 0)
            n++;
    }

    return n;
}

static void HuntColumns(byte[] gfx, string folder)
{
    var best = new List<(int Score, int Start, int Length)>();
    var runStart = 74000;
    var run = 1;
    for (var tile = 74000; tile < 110000; tile++)
    {
        var link = EdgeLink(gfx, tile, tile + 1);
        if (link)
        {
            run++;
            continue;
        }

        if (run >= 6)
            best.Add((run, runStart, run));
        runStart = tile + 1;
        run = 1;
    }

    best.Sort((a, b) => b.Length.CompareTo(a.Length));
    var kept = 0;
    var used = new List<int>();
    foreach (var item in best)
    {
        var near = false;
        foreach (var old in used)
        {
            if (Math.Abs(old - item.Start) < item.Length)
            {
                near = true;
                break;
            }
        }

        if (near)
            continue;
        used.Add(item.Start);
        var cols = 4;
        var tiles = new int[cols * item.Length];
        for (var c = 0; c < cols; c++)
            for (var r = 0; r < item.Length; r++)
                tiles[c * item.Length + r] = item.Start + c * item.Length + r;
        var path = Path.Combine(folder, $"col-{item.Start}-h{item.Length}.png");
        DrawGrid(gfx, tiles, cols, item.Length, path);
        Console.WriteLine($"{path} tile={item.Start} alto={item.Length}");
        kept++;
        if (kept == 6)
            break;
    }

    Console.WriteLine($"columnas={kept} candidatas={best.Count}");
}

static bool EdgeLink(byte[] gfx, int a, int b)
{
    var top = DecodeTile(gfx, a);
    var bot = DecodeTile(gfx, b);
    var agree = 0;
    var ink = 0;
    for (var x = 0; x < 16; x++)
    {
        var p = top[15 * 16 + x] > 0;
        var q = bot[x] > 0;
        if (p == q)
            agree++;
        if (p || q)
            ink++;
    }

    return agree >= 14 && ink >= 3;
}

static byte[] DecodeTile(byte[] rom, int tile)
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
                var color = ((bp0 >> bit) & 1)
                    | (((bp1 >> bit) & 1) << 1)
                    | (((bp2 >> bit) & 1) << 2)
                    | (((bp3 >> bit) & 1) << 3);
                pix[(yOfs[block] + row) * 16 + xOfs[block] + bit] = (byte)color;
            }
        }
    }

    return pix;
}

static void WriteSheet(byte[] rom, string path, int startTile, int columns, int rows, int step, bool columnMajor)
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
            var tile = DecodeTile(rom, index);
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

static byte[] Read(ZipArchive zip, string name)
{
    var entry = zip.GetEntry(name) ?? throw new FileNotFoundException(name);
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

static void ScanStrips(byte[] cpu, byte[] gfx, string folder)
{
    var shown = 0;
    for (var at = 0; at + 16 < cpu.Length && shown < 8; at += 2)
    {
        var width = cpu[at] + 1;
        var height = cpu[at + 1] + 1;
        var skip = Be16(cpu, at + 2);
        if (width is < 2 or > 8 || height is < 6 or > 18)
            continue;
        var cells = width * height;
        if (at + 4 + cells * 4 > cpu.Length)
            continue;
        var tiles = new int[cells];
        var ok = true;
        for (var i = 0; i < cells; i++)
        {
            var low = Be16(cpu, at + 4 + i * 4);
            if (low < 50)
            {
                ok = false;
                break;
            }

            var tile = low;
            if (Filled(gfx, tile) < 8)
            {
                ok = false;
                break;
            }

            tiles[i] = tile;
        }

        if (!ok || tiles.Distinct().Count() < Math.Max(3, cells / 3))
            continue;
        var path = Path.Combine(folder, $"pose-{shown}-{at:X}.png");
        DrawGrid(gfx, tiles, width, height, path);
        Console.WriteLine($"{path} {width}x{height} skip={skip:X} tile={tiles[0]:X}");
        shown++;
        at += 4 + cells * 4;
    }

    Console.WriteLine($"poses={shown}");
}

static void DrawGrid(byte[] gfx, int[] tiles, int width, int height, string path)
{
    var w = width * 16;
    var h = height * 16;
    var rgba = new byte[w * h * 4];
    for (var col = 0; col < width; col++)
    {
        for (var row = 0; row < height; row++)
        {
            var tile = DecodeTile(gfx, tiles[col * height + row]);
            for (var y = 0; y < 16; y++)
            {
                for (var x = 0; x < 16; x++)
                {
                    var n = tile[y * 16 + x];
                    if (n == 0)
                        continue;
                    var dx = col * 16 + x;
                    var dy = row * 16 + y;
                    var o = (dy * w + dx) * 4;
                    var g = (byte)(n * 17);
                    rgba[o] = g;
                    rgba[o + 1] = g;
                    rgba[o + 2] = g;
                    rgba[o + 3] = 255;
                }
            }
        }
    }

    WritePng(path, w, h, rgba);
}

static void FindPointerTables(byte[] cpu)
{
    int[] needles = { 0x000B1194, 0x000B1197, 0x000B1297, 0x000B12A7 };
    for (var at = 0; at + 4 <= cpu.Length; at += 2)
    {
        var value = (int)ReadBE32(cpu, at);
        for (var n = 0; n < needles.Length; n++)
        {
            if (value == needles[n])
                Console.WriteLine($"ref {needles[n]:X8} @ {at:X6}");
        }
    }

    var run = 0;
    var runStart = 0;
    for (var at = 0; at + 4 <= cpu.Length; at += 4)
    {
        var value = (int)ReadBE32(cpu, at);
        var good = value >= 0x10000 && value < cpu.Length - 64 && (value & 1) == 0;
        if (good)
        {
            if (run == 0)
                runStart = at;
            run++;
            continue;
        }

        if (run is >= 24 and <= 64)
        {
            Console.WriteLine($"tabla {runStart:X6} n={run} primero={ReadBE32(cpu, runStart):X8}");
            Hex(cpu, (int)ReadBE32(cpu, runStart), 48);
        }

        run = 0;
    }
}

static void Copy(ZipArchive zip, string name, byte[] dest, int offset)
{
    var data = Read(zip, name);
    Buffer.BlockCopy(data, 0, dest, offset, data.Length);
}

record Part(int X, int Y, int Tile, int Attr);
record Frame(int Offset, List<Part> Parts);
