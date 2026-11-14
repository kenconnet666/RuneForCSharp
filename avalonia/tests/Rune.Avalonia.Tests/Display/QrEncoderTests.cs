using System.Text;

namespace Rune.Avalonia.Tests;

/// <summary>
/// 二维码编码器（孪生文件 QrEncoder，三个平台一样，测一份）：Reed–Solomon 按 ISO/IEC 18004 附录 I 的例子、格式与版本信息按标准的表、
/// 容量与校正图形位置；再用这里另写的解码器把矩阵读回来（功能图形、之字形顺序、分块交织都独立实现），内容与纠错码字都对得上。
/// </summary>
public class QrEncoderTests
{
    static readonly Dictionary<QrErrorCorrection, string[]> FormatTable = new()
    {
        [QrErrorCorrection.L] = ["111011111000100", "111001011110011", "111110110101010", "111100010011101", "110011000101111", "110001100011000", "110110001000001", "110100101110110"],
        [QrErrorCorrection.M] = ["101010000010010", "101000100100101", "101111001111100", "101101101001011", "100010111111001", "100000011001110", "100111110010111", "100101010100000"],
        [QrErrorCorrection.Q] = ["011010101011111", "011000001101000", "011111100110001", "011101000000110", "010010010110100", "010000110000011", "010111011011010", "010101111101101"],
        [QrErrorCorrection.H] = ["001011010001001", "001001110111110", "001110011100111", "001100111010000", "000011101100010", "000001001010101", "000110100001100", "000100000111011"],
    };

    /// <summary>被测的几种版本与纠错等级的分块（ISO/IEC 18004 表 9）：块数、每块纠错码字数。</summary>
    static readonly Dictionary<string, (int Count, int EccLength)> Blocks = new()
    {
        ["3-M"] = (1, 26),
        ["5-H"] = (4, 22),
        ["13-L"] = (4, 26),
        ["17-Q"] = (16, 28),
        ["37-H"] = (70, 30),
    };

    /// <summary>测试自己的解码：按标准读出格式信息、去掉掩码、按之字形取码字、拆块、核对纠错、读出字节模式的内容。</summary>
    static (string Text, QrErrorCorrection Ecc, int Mask) Decode(QrMatrix qr)
    {
        var (size, version) = (qr.Size, qr.Version);
        int At(int x, int y) => qr.IsDark(x, y) ? 1 : 0;

        // 格式信息：左上角那份（第 8 列自上而下、第 8 行自右而左），与右上、左下那份一致
        var first = new List<int>();
        for (var y = 0; y <= 5; y++) first.Add(At(8, y));
        first.AddRange([At(8, 7), At(8, 8), At(7, 8)]);
        for (var x = 5; x >= 0; x--) first.Add(At(x, 8));
        var second = new List<int>();
        for (var x = size - 1; x >= size - 8; x--) second.Add(At(x, 8));
        for (var y = size - 7; y < size; y++) second.Add(At(8, y));
        Assert.Equal(first, second);
        var format = first.Select((b, index) => b << index).Aggregate(0, (a, b) => a | b);
        var (ecc, mask) = FormatTable.SelectMany(pair => pair.Value.Select((bits, m) => (pair.Key, m, bits))).Where(entry => Convert.ToInt32(entry.bits, 2) == format).Select(entry => (entry.Key, entry.m)).Single();
        Assert.Equal(1, At(8, size - 8));

        // 功能图形
        var fixedModules = new HashSet<(int, int)>();
        void Mark(int x, int y) => fixedModules.Add((x, y));
        for (var i = 0; i < size; i++)
        {
            Mark(6, i);
            Mark(i, 6);
        }
        for (var y = 0; y < 9; y++)
            for (var x = 0; x < 9; x++)
            {
                Mark(x, y);
                if (x < 8) Mark(size - 1 - x, y);
                if (y < 8) Mark(x, size - 1 - y);
            }
        Mark(8, size - 8);
        var centers = QrEncoder.AlignmentPositions(version);
        foreach (var cy in centers)
            foreach (var cx in centers)
            {
                // 落在定位图形里的三个角不画（落在定时图形上的照画）
                var inFinder = (cx < 9 || cx > size - 9) && (cy < 9 || cy > size - 9);
                if (inFinder && !(cx > size - 9 && cy > size - 9)) continue;
                for (var dy = -2; dy <= 2; dy++)
                    for (var dx = -2; dx <= 2; dx++) Mark(cx + dx, cy + dy);
            }
        if (version >= 7)
            for (var a = size - 11; a < size - 8; a++)
                for (var b = 0; b < 6; b++)
                {
                    Mark(a, b);
                    Mark(b, a);
                }
        Assert.Equal(QrEncoder.RawDataModules(version), size * size - fixedModules.Count);

        // 之字形：从右下角两列一组，上去、下来交替，跳过第 6 列
        var bits = new List<int>();
        var upward = true;
        for (var x = size - 1; x > 0; x -= 2)
        {
            if (x == 6) x = 5;
            for (var k = 0; k < size; k++)
            {
                var y = upward ? size - 1 - k : k;
                foreach (var cx in new[] { x, x - 1 })
                {
                    if (fixedModules.Contains((cx, y))) continue;
                    var flip = mask switch
                    {
                        0 => (cx + y) % 2 == 0,
                        1 => y % 2 == 0,
                        2 => cx % 3 == 0,
                        3 => (cx + y) % 3 == 0,
                        4 => (y / 2 + cx / 3) % 2 == 0,
                        5 => cx * y % 2 + cx * y % 3 == 0,
                        6 => (cx * y % 2 + cx * y % 3) % 2 == 0,
                        _ => ((cx + y) % 2 + cx * y % 3) % 2 == 0,
                    };
                    bits.Add(At(cx, y) ^ (flip ? 1 : 0));
                }
            }
            upward = !upward;
        }
        var codewords = new List<int>();
        for (var i = 0; i + 8 <= bits.Count; i += 8) codewords.Add(bits.Skip(i).Take(8).Aggregate(0, (v, b) => (v << 1) | b));

        // 拆块：块数与每块纠错码字数照标准的表 9，前面的块短一个数据码字
        var (blockCount, eccLength) = Blocks[$"{version}-{ecc}"];
        var dataTotal = QrEncoder.RawDataModules(version) / 8 - blockCount * eccLength;
        Assert.Equal(dataTotal, QrEncoder.DataCodewords(version, ecc));
        var shortData = dataTotal / blockCount;
        var longBlocks = dataTotal % blockCount;
        var lengths = Enumerable.Range(0, blockCount).Select(i => i < blockCount - longBlocks ? shortData : shortData + 1).ToArray();
        var blocks = lengths.Select(_ => new List<int>()).ToArray();
        var next = 0;
        for (var c = 0; c <= shortData; c++)
            for (var b = 0; b < blockCount; b++)
                if (c < lengths[b]) blocks[b].Add(codewords[next++]);
        var parities = blocks.Select(_ => new List<int>()).ToArray();
        for (var c = 0; c < eccLength; c++)
            foreach (var parity in parities) parity.Add(codewords[next++]);
        for (var b = 0; b < blockCount; b++) Assert.Equal(parities[b], QrEncoder.ReedSolomon(blocks[b], eccLength));

        // 字节模式：0100、字符数（版本 1–9 是 8 位，之后 16 位）、字节
        var stream = blocks.SelectMany(block => block).SelectMany(value => Enumerable.Range(0, 8).Select(i => (value >> (7 - i)) & 1)).ToList();
        var position = 0;
        int Read(int length)
        {
            var value = stream.Skip(position).Take(length).Aggregate(0, (v, b) => (v << 1) | b);
            position += length;
            return value;
        }
        Assert.Equal(0b0100, Read(4));
        var count = Read(version <= 9 ? 8 : 16);
        var bytes = Enumerable.Range(0, count).Select(_ => (byte)Read(8)).ToArray();
        return (Encoding.UTF8.GetString(bytes), ecc, mask);
    }

    [Fact]
    public void Reed_solomon_matches_the_standard_examples()
    {
        Assert.Equal([0xa5, 0x24, 0xd4, 0xc1, 0xed, 0x36, 0xc7, 0x87, 0x2c, 0x55], QrEncoder.ReedSolomon([16, 32, 12, 86, 97, 128, 236, 17, 236, 17, 236, 17, 236, 17, 236, 17], 10));
        Assert.Equal([196, 35, 39, 119, 235, 215, 231, 226, 93, 23], QrEncoder.ReedSolomon([32, 91, 11, 120, 209, 114, 220, 77, 67, 64, 236, 17, 236, 17, 236, 17], 10));
    }

    [Fact]
    public void Format_and_version_bits_match_the_tables()
    {
        foreach (var (ecc, table) in FormatTable)
            for (var mask = 0; mask < 8; mask++) Assert.Equal(table[mask], Convert.ToString(QrEncoder.FormatBits(ecc, mask), 2).PadLeft(15, '0'));
        Assert.Equal([0x07c94, 0x085bc, 0x15683, 0x28c69], new[] { QrEncoder.VersionBits(7), QrEncoder.VersionBits(8), QrEncoder.VersionBits(21), QrEncoder.VersionBits(40) });
    }

    [Fact]
    public void Capacity_and_alignment_positions()
    {
        QrErrorCorrection[] levels = [QrErrorCorrection.L, QrErrorCorrection.M, QrErrorCorrection.Q, QrErrorCorrection.H];
        Assert.Equal([19, 16, 13, 9], levels.Select(ecc => QrEncoder.DataCodewords(1, ecc)));
        Assert.Equal([2956, 2334, 1666, 1276], levels.Select(ecc => QrEncoder.DataCodewords(40, ecc)));
        Assert.Equal(216, QrEncoder.DataCodewords(10, QrErrorCorrection.M));
        Assert.Empty(QrEncoder.AlignmentPositions(1));
        Assert.Equal([6, 18], QrEncoder.AlignmentPositions(2));
        Assert.Equal([6, 22, 38], QrEncoder.AlignmentPositions(7));
        Assert.Equal([6, 34, 60, 86, 112, 138], QrEncoder.AlignmentPositions(32));
        Assert.Equal([6, 30, 58, 86, 114, 142, 170], QrEncoder.AlignmentPositions(40));
    }

    [Fact]
    public void Picks_the_smallest_version_and_throws_when_it_does_not_fit()
    {
        Assert.Equal(1, QrEncoder.Encode(new string('a', 17), QrErrorCorrection.L).Version);
        Assert.Equal(2, QrEncoder.Encode(new string('a', 18), QrErrorCorrection.L).Version);
        Assert.Equal(40, QrEncoder.Encode(new string('a', 2953), QrErrorCorrection.L).Version);
        Assert.Throws<ArgumentException>(() => QrEncoder.Encode(new string('a', 2954), QrErrorCorrection.L));
        Assert.Equal(37, QrEncoder.Encode("a", minVersion: 5).Size);
    }

    [Fact]
    public void Finder_patterns_sit_in_three_corners()
    {
        var qr = QrEncoder.Encode("https://example.com");
        string[] finder = ["1111111", "1000001", "1011101", "1011101", "1011101", "1000001", "1111111"];
        string[] Read(int left, int top) => [.. Enumerable.Range(0, 7).Select(y => string.Concat(Enumerable.Range(0, 7).Select(x => qr.IsDark(left + x, top + y) ? '1' : '0')))];
        Assert.Equal(finder, Read(0, 0));
        Assert.Equal(finder, Read(qr.Size - 7, 0));
        Assert.Equal(finder, Read(0, qr.Size - 7));
    }

    [Fact]
    public void Decoding_returns_the_text_for_every_level_block_layout_and_mask()
    {
        (string Text, QrErrorCorrection Ecc, int? Mask)[] cases =
        [
            ("https://example.com/?q=二维码", QrErrorCorrection.M, null),
            ("扫码登录：请在 120 秒内确认", QrErrorCorrection.H, null),
            (string.Concat(Enumerable.Repeat("供应商对账单 2026-11 ", 12)), QrErrorCorrection.Q, null),
            (new string('x', 400), QrErrorCorrection.L, 3),
            (string.Concat(Enumerable.Repeat("长内容", 120)), QrErrorCorrection.H, null),
        ];
        foreach (var (text, ecc, mask) in cases)
        {
            var qr = QrEncoder.Encode(text, ecc, mask: mask);
            Assert.Equal((text, ecc, qr.Mask), Decode(qr));
            if (mask is { } chosen) Assert.Equal(chosen, qr.Mask);
        }
    }

    [Fact]
    public void Svg_path_merges_runs_and_offsets_by_the_quiet_zone()
    {
        var qr = QrEncoder.Encode("https://example.com");
        // 第一行从定位图形开始：7 个深色模块连成一段
        Assert.StartsWith("M2 2h7v1h-7z", QrEncoder.SvgPath(qr, 2));
        Assert.Contains($"viewBox=\"0 0 {qr.Size + 4} {qr.Size + 4}\"", QrEncoder.Svg(qr, 2, "#000", "#fff"));
    }
}
