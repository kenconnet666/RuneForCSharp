namespace Rune.WinForms;

/// <summary>
/// 按地址读图片（ImageControl、LightboxControl 共用）：file://、http(s)://、data:（base64），相对地址按程序所在目录。
/// 解码在后台线程（复制成独立的 Bitmap，不再依赖读它的流），之后只在界面线程上画；最近用过的几十张留在内存里，
/// 缩略图点开大图、Lightbox 预先加载的前后两张不用再读一遍。
/// </summary>
static class ImageLoader
{
    const int Capacity = 32;
    static readonly HttpClient Http = new();
    static readonly Dictionary<Uri, Task<Bitmap>> Cache = [];
    static readonly Queue<Uri> Order = [];

    /// <summary>读出并解码：同一个地址只读一次（失败的不留，下次重新读）。</summary>
    public static Task<Bitmap> LoadAsync(Uri source)
    {
        lock (Cache)
        {
            if (Cache.TryGetValue(source, out var cached)) return cached;
            var task = Decode(source);
            Cache[source] = task;
            Order.Enqueue(source);
            while (Order.Count > Capacity) Cache.Remove(Order.Dequeue());
            return task;
        }
    }

    static async Task<Bitmap> Decode(Uri source)
    {
        try
        {
            var bytes = await ReadAsync(source).ConfigureAwait(false);
            using var stream = new MemoryStream(bytes);
            using var decoded = System.Drawing.Image.FromStream(stream);
            return new Bitmap(decoded);
        }
        catch
        {
            lock (Cache) Cache.Remove(source);
            throw;
        }
    }

    /// <summary>读出原始字节（下载按钮存的就是它）。</summary>
    public static async Task<byte[]> ReadAsync(Uri source)
    {
        if (!source.IsAbsoluteUri) return await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, source.OriginalString)).ConfigureAwait(false);
        switch (source.Scheme)
        {
            case "file":
                return await File.ReadAllBytesAsync(source.LocalPath).ConfigureAwait(false);
            case "http" or "https":
                return await Http.GetByteArrayAsync(source).ConfigureAwait(false);
            case "data":
            {
                var text = source.OriginalString;
                var comma = text.IndexOf(',');
                if (comma < 0 || !text[..comma].EndsWith(";base64", StringComparison.OrdinalIgnoreCase))
                    throw new NotSupportedException($"只支持 base64 的 data 地址：{text[..Math.Min(text.Length, 40)]}…");
                return Convert.FromBase64String(text[(comma + 1)..]);
            }
            default:
                throw new NotSupportedException($"不支持的图片地址：{source}");
        }
    }
}
