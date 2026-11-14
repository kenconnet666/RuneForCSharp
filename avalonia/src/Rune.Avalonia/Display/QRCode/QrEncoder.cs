using System.Text;

namespace Rune.Avalonia;

/// <summary>二维码的纠错等级：能恢复约 7%、15%、25%、30% 的损坏（中间放 logo 时用 H）。</summary>
public enum QrErrorCorrection
{
    /// <summary>约 7%。</summary>
    L,

    /// <summary>约 15%（默认）。</summary>
    M,

    /// <summary>约 25%。</summary>
    Q,

    /// <summary>约 30%：中间放 logo 时用它。</summary>
    H,
}

/// <summary>编码好的二维码：边长（模块数，不含静区）与每个模块的深浅。</summary>
public sealed class QrMatrix
{
    readonly bool[] dark;

    internal QrMatrix(int version, QrErrorCorrection ecc, int mask, bool[] dark)
    {
        Version = version;
        Size = version * 4 + 17;
        Ecc = ecc;
        Mask = mask;
        this.dark = dark;
    }

    /// <summary>版本（1–40）。</summary>
    public int Version { get; }

    /// <summary>边长（模块数）：版本 × 4 + 17。</summary>
    public int Size { get; }

    /// <summary>纠错等级。</summary>
    public QrErrorCorrection Ecc { get; }

    /// <summary>用的掩码（0–7）。</summary>
    public int Mask { get; }

    /// <summary>第 y 行第 x 列是不是深色（越界为 false）。</summary>
    public bool IsDark(int x, int y) => x >= 0 && x < Size && y >= 0 && y < Size && dark[y * Size + x];
}

/// <summary>
/// 二维码编码器（vue-tsx-pro 的 utils/qr/encode，ISO/IEC 18004）：文字按 UTF-8 走字节模式，四档纠错，自动选最小的版本（1–40）与罚分最低的掩码。
/// 结构参考 Nayuki 的 QR Code generator（MIT）：Reed–Solomon 纠错、分块交织、功能图形、之字形放置数据、掩码罚分。
/// 只做字节模式：链接、中文都是字节模式，数字、字母数字模式省下的不多。数据放不下最大的版本时抛 ArgumentException。
/// 三个平台各有一份逐字相同的副本。
/// </summary>
public static class QrEncoder
{
    // 每个版本、每档纠错：每块的纠错码字数、块数（ISO/IEC 18004 表 9；下标 0 不用）
    static readonly int[][] EccCodewordsPerBlock =
    [
        [-1, 7, 10, 15, 20, 26, 18, 20, 24, 30, 18, 20, 24, 26, 30, 22, 24, 28, 30, 28, 28, 28, 28, 30, 30, 26, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
        [-1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28],
        [-1, 13, 22, 18, 26, 18, 24, 18, 22, 20, 24, 28, 26, 24, 20, 30, 24, 28, 28, 26, 30, 28, 30, 30, 30, 30, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
        [-1, 17, 28, 22, 16, 22, 28, 26, 26, 24, 28, 24, 28, 22, 24, 24, 30, 28, 28, 26, 28, 30, 24, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30],
    ];

    static readonly int[][] EccBlocks =
    [
        [-1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4, 4, 4, 4, 4, 6, 6, 6, 6, 7, 8, 8, 9, 9, 10, 12, 12, 12, 13, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 24, 25],
        [-1, 1, 1, 1, 2, 2, 4, 4, 4, 5, 5, 5, 8, 9, 9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49],
        [-1, 1, 1, 2, 2, 4, 4, 6, 6, 8, 8, 8, 10, 12, 16, 12, 17, 16, 18, 21, 20, 23, 23, 25, 27, 29, 34, 34, 35, 38, 40, 43, 45, 48, 51, 53, 56, 59, 62, 65, 68],
        [-1, 1, 1, 2, 4, 4, 4, 5, 6, 8, 8, 11, 11, 16, 16, 18, 16, 19, 21, 25, 25, 25, 34, 30, 32, 35, 37, 40, 42, 45, 48, 51, 54, 57, 60, 63, 66, 70, 74, 77, 81],
    ];

    /// <summary>格式信息里纠错等级的两位（L、M、Q、H 依次是 01、00、11、10）。</summary>
    static int EccFormatBits(QrErrorCorrection ecc) => ecc switch
    {
        QrErrorCorrection.L => 1,
        QrErrorCorrection.M => 0,
        QrErrorCorrection.Q => 3,
        _ => 2,
    };

    /// <summary>掩码条件：第 y 行第 x 列为真时翻转（ISO/IEC 18004 表 10）。</summary>
    static bool Flips(int mask, int x, int y) => mask switch
    {
        0 => (x + y) % 2 == 0,
        1 => y % 2 == 0,
        2 => x % 3 == 0,
        3 => (x + y) % 3 == 0,
        4 => (x / 3 + y / 2) % 2 == 0,
        5 => x * y % 2 + x * y % 3 == 0,
        6 => (x * y % 2 + x * y % 3) % 2 == 0,
        _ => ((x + y) % 2 + x * y % 3) % 2 == 0,
    };

    /// <summary>版本 version 里放数据与纠错码字的模块数（去掉功能图形）。</summary>
    public static int RawDataModules(int version)
    {
        var result = (16 * version + 128) * version + 64;
        if (version < 2) return result;
        var alignments = version / 7 + 2;
        result -= (25 * alignments - 10) * alignments - 55;
        if (version >= 7) result -= 36;
        return result;
    }

    /// <summary>版本 version、纠错等级 ecc 能放的数据码字数。</summary>
    public static int DataCodewords(int version, QrErrorCorrection ecc) =>
        RawDataModules(version) / 8 - EccCodewordsPerBlock[(int)ecc][version] * EccBlocks[(int)ecc][version];

    /// <summary>校正图形中心的行列坐标（版本 1 没有）。</summary>
    public static int[] AlignmentPositions(int version)
    {
        if (version == 1) return [];
        var count = version / 7 + 2;
        var step = (version * 8 + count * 3 + 5) / (count * 4 - 4) * 2;
        var result = new List<int> { 6 };
        for (var position = version * 4 + 10; result.Count < count; position -= step) result.Insert(1, position);
        return [.. result];
    }

    /// <summary>GF(2^8) 上的乘法，本原多项式 x^8 + x^4 + x^3 + x^2 + 1（0x11D）。</summary>
    static int GfMultiply(int x, int y)
    {
        var z = 0;
        for (var i = 7; i >= 0; i--)
        {
            z = (z << 1) ^ ((z >> 7) * 0x11D);
            z ^= ((y >> i) & 1) * x;
        }
        return z;
    }

    /// <summary>Reed–Solomon 生成多项式（degree 次，最高次项系数 1 省略，从高到低）。</summary>
    static int[] ReedSolomonDivisor(int degree)
    {
        var result = new int[degree];
        result[degree - 1] = 1;
        var root = 1;
        for (var i = 0; i < degree; i++)
        {
            for (var j = 0; j < result.Length; j++)
            {
                result[j] = GfMultiply(result[j], root);
                if (j + 1 < result.Length) result[j] ^= result[j + 1];
            }
            root = GfMultiply(root, 0x02);
        }
        return result;
    }

    /// <summary>data 的纠错码字：data 乘 x^n 除以生成多项式的余数。</summary>
    public static int[] ReedSolomon(IReadOnlyList<int> data, int eccLength)
    {
        var divisor = ReedSolomonDivisor(eccLength);
        var result = new List<int>(new int[eccLength]);
        foreach (var value in data)
        {
            var factor = value ^ result[0];
            result.RemoveAt(0);
            result.Add(0);
            for (var i = 0; i < divisor.Length; i++) result[i] ^= GfMultiply(divisor[i], factor);
        }
        return [.. result];
    }

    /// <summary>15 位格式信息：纠错等级与掩码，BCH(15,5) 校验后与 0x5412 异或。</summary>
    public static int FormatBits(QrErrorCorrection ecc, int mask)
    {
        var data = (EccFormatBits(ecc) << 3) | mask;
        var remainder = data;
        for (var i = 0; i < 10; i++) remainder = (remainder << 1) ^ ((remainder >> 9) * 0x537);
        return ((data << 10) | remainder) ^ 0x5412;
    }

    /// <summary>18 位版本信息（版本 7 起）：BCH(18,6)。</summary>
    public static int VersionBits(int version)
    {
        var remainder = version;
        for (var i = 0; i < 12; i++) remainder = (remainder << 1) ^ ((remainder >> 11) * 0x1F25);
        return (version << 12) | remainder;
    }

    static bool Bit(int value, int index) => ((value >> index) & 1) != 0;

    /// <summary>
    /// 深色模块的 SVG 路径（同 TS 的 modulesPath）：一行里连续的深色模块合成一个矩形，坐标是模块数（含 <paramref name="margin"/> 个模块的静区）。
    /// 导出成 SVG 文件时配 <c>viewBox="0 0 边长 边长"</c>，边长是 Size + 2 × margin。
    /// </summary>
    public static string SvgPath(QrMatrix qr, int margin)
    {
        var path = new StringBuilder();
        for (var y = 0; y < qr.Size; y++)
            for (var x = 0; x < qr.Size; x++)
            {
                if (!qr.IsDark(x, y)) continue;
                var run = 1;
                while (qr.IsDark(x + run, y)) run++;
                path.Append($"M{x + margin} {y + margin}h{run}v1h-{run}z");
                x += run;
            }
        return path.ToString();
    }

    /// <summary>完整的 SVG 文档：底色一块矩形、深色模块一条路径（颜色写 CSS 颜色，如 #1c2024）。</summary>
    public static string Svg(QrMatrix qr, int margin, string ink, string paper)
    {
        var side = qr.Size + 2 * margin;
        return $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {side} {side}\" shape-rendering=\"crispEdges\"><rect width=\"100%\" height=\"100%\" fill=\"{paper}\"/><path d=\"{SvgPath(qr, margin)}\" fill=\"{ink}\"/></svg>";
    }

    /// <summary>把文字（UTF-8）编码成二维码。</summary>
    /// <param name="text">文字。</param>
    /// <param name="ecc">纠错等级。</param>
    /// <param name="minVersion">最小版本（1–40）：想让不同长度的内容一样大时调高。</param>
    /// <param name="mask">指定掩码（0–7）；null 时按罚分自动选。</param>
    public static QrMatrix Encode(string text, QrErrorCorrection ecc = QrErrorCorrection.M, int minVersion = 1, int? mask = null) =>
        Encode(Encoding.UTF8.GetBytes(text), ecc, minVersion, mask);

    /// <summary>把字节编码成二维码。</summary>
    public static QrMatrix Encode(byte[] bytes, QrErrorCorrection ecc = QrErrorCorrection.M, int minVersion = 1, int? mask = null)
    {
        var version = Math.Clamp(minVersion, 1, 40);
        bool Fits(int v) => 4 + (v <= 9 ? 8 : 16) + bytes.Length * 8 <= DataCodewords(v, ecc) * 8 && bytes.Length < (v <= 9 ? 256 : 65536);
        while (version <= 40 && !Fits(version)) version++;
        if (version > 40) throw new ArgumentException($"二维码放不下 {bytes.Length} 字节（纠错等级 {ecc} 最多 {DataCodewords(40, ecc) - 3} 字节）", nameof(bytes));

        var grid = new Grid(version);
        grid.DrawFunctionPatterns(ecc);
        grid.DrawCodewords(Interleave(DataCodewordsOf(bytes, version, ecc), version, ecc));
        var chosen = mask ?? Enumerable.Range(0, 8).MinBy(candidate =>
        {
            grid.ApplyMask(candidate);
            grid.DrawFormat(ecc, candidate);
            var score = grid.Penalty();
            grid.ApplyMask(candidate);
            return score;
        });
        grid.ApplyMask(chosen);
        grid.DrawFormat(ecc, chosen);
        return new QrMatrix(version, ecc, chosen, grid.Dark);
    }

    /// <summary>数据码字：模式 0100、字符数、字节，终止符与补齐（0xEC、0x11 交替）。</summary>
    static List<int> DataCodewordsOf(byte[] bytes, int version, QrErrorCorrection ecc)
    {
        var bits = new List<int>();
        void Append(int value, int length)
        {
            for (var i = length - 1; i >= 0; i--) bits.Add((value >> i) & 1);
        }
        Append(0b0100, 4);
        Append(bytes.Length, version <= 9 ? 8 : 16);
        foreach (var value in bytes) Append(value, 8);
        var capacity = DataCodewords(version, ecc) * 8;
        Append(0, Math.Min(4, capacity - bits.Count));
        Append(0, (8 - bits.Count % 8) % 8);
        var result = new List<int>();
        for (var i = 0; i < bits.Count; i += 8) result.Add(bits.Skip(i).Take(8).Aggregate(0, (value, b) => (value << 1) | b));
        for (var pad = 0xEC; result.Count < capacity / 8; pad ^= 0xEC ^ 0x11) result.Add(pad);
        return result;
    }

    /// <summary>分块、各块算纠错码字，再按列交织成最终的码字序列。</summary>
    static List<int> Interleave(List<int> data, int version, QrErrorCorrection ecc)
    {
        var blockCount = EccBlocks[(int)ecc][version];
        var eccLength = EccCodewordsPerBlock[(int)ecc][version];
        var total = RawDataModules(version) / 8;
        var shortBlocks = blockCount - total % blockCount;
        var shortLength = total / blockCount;
        var blocks = new List<List<int>>();
        for (int i = 0, offset = 0; i < blockCount; i++)
        {
            var length = shortLength - eccLength + (i < shortBlocks ? 0 : 1);
            var block = data.GetRange(offset, length);
            offset += length;
            var parity = ReedSolomon(block, eccLength);
            // 短块在数据末尾占一个空位，交织时跳过，块长一致好按列取
            if (i < shortBlocks) block.Add(0);
            block.AddRange(parity);
            blocks.Add(block);
        }
        var result = new List<int>();
        for (var column = 0; column < blocks[0].Count; column++)
            for (var index = 0; index < blocks.Count; index++)
                if (column != shortLength - eccLength || index >= shortBlocks) result.Add(blocks[index][column]);
        return result;
    }

    /// <summary>按版本、纠错等级画出全部模块（掩码选定前的状态与功能图形标记）。</summary>
    sealed class Grid(int version)
    {
        readonly int size = version * 4 + 17;
        readonly bool[] fixedModules = new bool[(version * 4 + 17) * (version * 4 + 17)];

        public bool[] Dark { get; } = new bool[(version * 4 + 17) * (version * 4 + 17)];

        void Set(int x, int y, bool dark)
        {
            Dark[y * size + x] = dark;
            fixedModules[y * size + x] = true;
        }

        /// <summary>定位图形（含白边分隔）、定时图形、校正图形、版本信息、格式信息的位置。</summary>
        public void DrawFunctionPatterns(QrErrorCorrection ecc)
        {
            for (var i = 0; i < size; i++)
            {
                Set(6, i, i % 2 == 0);
                Set(i, 6, i % 2 == 0);
            }
            foreach (var (cx, cy) in new[] { (3, 3), (size - 4, 3), (3, size - 4) })
                for (var dy = -4; dy <= 4; dy++)
                    for (var dx = -4; dx <= 4; dx++)
                    {
                        var (x, y) = (cx + dx, cy + dy);
                        if (x < 0 || x >= size || y < 0 || y >= size) continue;
                        var distance = Math.Max(Math.Abs(dx), Math.Abs(dy));
                        Set(x, y, distance != 2 && distance != 4);
                    }
            var positions = AlignmentPositions(version);
            var last = positions.Length - 1;
            for (var row = 0; row < positions.Length; row++)
                for (var column = 0; column < positions.Length; column++)
                {
                    // 与三个定位图形重叠的角上不画
                    if ((row == 0 && column == 0) || (row == 0 && column == last) || (row == last && column == 0)) continue;
                    for (var dy = -2; dy <= 2; dy++)
                        for (var dx = -2; dx <= 2; dx++)
                            Set(positions[column] + dx, positions[row] + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
                }
            DrawFormat(ecc, 0);
            if (version < 7) return;
            var bits = VersionBits(version);
            for (var i = 0; i < 18; i++)
            {
                var (a, b) = (size - 11 + i % 3, i / 3);
                Set(a, b, Bit(bits, i));
                Set(b, a, Bit(bits, i));
            }
        }

        /// <summary>两份格式信息与固定的深色模块。</summary>
        public void DrawFormat(QrErrorCorrection ecc, int mask)
        {
            var bits = FormatBits(ecc, mask);
            for (var i = 0; i <= 5; i++) Set(8, i, Bit(bits, i));
            Set(8, 7, Bit(bits, 6));
            Set(8, 8, Bit(bits, 7));
            Set(7, 8, Bit(bits, 8));
            for (var i = 9; i < 15; i++) Set(14 - i, 8, Bit(bits, i));
            for (var i = 0; i < 8; i++) Set(size - 1 - i, 8, Bit(bits, i));
            for (var i = 8; i < 15; i++) Set(8, size - 15 + i, Bit(bits, i));
            Set(8, size - 8, true);
        }

        /// <summary>码字按之字形放进非功能模块：从右下角起，两列一组上下来回，跳过第 6 列（竖的定时图形）。</summary>
        public void DrawCodewords(List<int> codewords)
        {
            var index = 0;
            for (var right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;
                var upward = ((right + 1) & 2) == 0;
                for (var vertical = 0; vertical < size; vertical++)
                {
                    var y = upward ? size - 1 - vertical : vertical;
                    for (var j = 0; j < 2; j++)
                    {
                        var x = right - j;
                        if (fixedModules[y * size + x]) continue;
                        // 码字之外剩下的几个模块（余位）留作浅色
                        if (index < codewords.Count * 8) Dark[y * size + x] = Bit(codewords[index >> 3], 7 - (index & 7));
                        index++;
                    }
                }
            }
        }

        /// <summary>按掩码翻转数据模块（再调用一次就还原）。</summary>
        public void ApplyMask(int mask)
        {
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                    if (!fixedModules[y * size + x] && Flips(mask, x, y)) Dark[y * size + x] = !Dark[y * size + x];
        }

        /// <summary>罚分（ISO/IEC 18004 7.8.3）：连续同色、2×2 同色块、类似定位图形的 1:1:3:1:1、深浅比例。</summary>
        public int Penalty()
        {
            int At(int x, int y) => Dark[y * size + x] ? 1 : 0;
            var score = 0;
            for (var line = 0; line < size; line++)
                foreach (var horizontal in new[] { true, false })
                {
                    int Read(int i) => horizontal ? At(i, line) : At(line, i);
                    var run = 1;
                    for (var i = 1; i <= size; i++)
                    {
                        if (i < size && Read(i) == Read(i - 1))
                        {
                            run++;
                            continue;
                        }
                        if (run >= 5) score += 3 + run - 5;
                        run = 1;
                    }
                    // 最近 11 个模块滚动成一个整数，比对 1011101 前后带四个浅色的两种排列
                    var window = 0;
                    for (var i = 0; i < size; i++)
                    {
                        window = ((window << 1) & 0x7FF) | Read(i);
                        if (i >= 10 && window is 0b10111010000 or 0b00001011101) score += 40;
                    }
                }
            var darkCount = 0;
            for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    darkCount += At(x, y);
                    if (x + 1 < size && y + 1 < size && At(x, y) == At(x + 1, y) && At(x, y) == At(x, y + 1) && At(x, y) == At(x + 1, y + 1)) score += 3;
                }
            var total = size * size;
            score += ((int)Math.Ceiling(Math.Abs(darkCount * 20 - total * 10) / (double)total) - 1) * 10;
            return score;
        }
    }
}
