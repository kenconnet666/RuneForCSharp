using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 图片（vue-tsx-pro 的 Image）：按地址读图，读的时候是扫光的占位，读不了时显示占位与 alt；写了 <see cref="Preview"/> 时点击（或 Enter、空格）
/// 看大图（<see cref="LightboxControl"/>）。同一个 <see cref="ImageGroupControl"/> 里的图共用一个预览，大图里左右切换。
/// <code>
/// var photo = new ImageControl { Source = new Uri("photos/1-thumb.jpg", UriKind.Relative), Alt = "支架正面", Size = new Size(160, 120), Preview = true };
/// </code>
/// 地址是 <see cref="Uri"/>：file://、http(s)://、base64 的 data:，相对地址按程序所在目录（要有读的状态与失败的占位，所以不收已经解码的 Image）。
/// 框的大小是控件的 Size（打开 AutoSize 时按图片，图片的一个像素是 1/96 英寸），图片在框里怎么放用 <see cref="Fit"/>（默认 cover），圆角档位 <see cref="Radius"/>（默认 md），底色 fill。
/// 可以预览时是按钮：名称“查看大图：alt”，Tab 能到，悬停时略微压暗。读屏：不能预览时是一张图片、名称是 alt；读不了时说明“图片加载失败”。
/// </summary>
[DefaultEvent(nameof(StatusChanged))]
[Description("图片：按地址读图，读的时候占位、读不了时显示 alt；Preview 时点击看大图。")]
public class ImageControl : Control
{
    readonly SkeletonControl shimmer = new() { Shape = SkeletonShape.Image, Dock = DockStyle.Fill };
    readonly Icon brokenIcon = new() { Data = Lucide.ImageOff };
    readonly Text brokenText = new() { Align = TextAlign.Center };
    Bitmap? picture;
    LightboxControl? lightbox;
    int loading;
    bool hovered;
    bool pressed;

    /// <summary>新建：读之前是占位。</summary>
    public ImageControl()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                 | ControlStyles.SupportsTransparentBackColor, true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Color.Transparent;
        TabStop = false;
        // 占位不接鼠标：点在占位上也是点图片
        shimmer.Enabled = false;
        Controls.Add(shimmer);
    }

    /// <summary>图片地址。</summary>
    [Category("Appearance")]
    [Description("图片地址：file://、http(s)://、data:，相对地址按程序所在目录。")]
    [DefaultValue(null)]
    public Uri? Source
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            if (IsHandleCreated) _ = Load();
        }
    }

    /// <summary>图片的说明：读屏念它，读不了时也显示。</summary>
    [Category("Accessibility")]
    [Description("图片的说明：读屏念它，读不了时也显示。")]
    [DefaultValue(null)]
    public string? Alt { get; set { field = value; brokenText.Content = value ?? ""; UpdateRole(); Invalidate(); } }

    /// <summary>图片在框里怎么放，默认 cover。</summary>
    [Category("Appearance")]
    [Description("图片在框里怎么放：cover（默认）、contain、fill、none、scale-down。")]
    [DefaultValue(typeof(ImageFit), "cover")]
    public ImageFit Fit { get => field.Value is null ? ImageFit.Cover : field; set { field = value; Invalidate(); } }

    /// <summary>圆角档位，默认 md。</summary>
    [Category("Appearance")]
    [Description("圆角档位，默认 md。")]
    [DefaultValue(typeof(Radius), "md")]
    public Radius Radius { get => field.Value is null ? Radius.Md : field; set { field = value; Invalidate(); } }

    /// <summary>点击看大图。</summary>
    [Category("Behavior")]
    [Description("点击（或 Enter、空格）看大图。")]
    [DefaultValue(false)]
    public bool Preview { get; set { field = value; UpdateRole(); } }

    /// <summary>大图的地址；不写时用 <see cref="Source"/>。</summary>
    [Category("Behavior")]
    [Description("大图的地址；不写时用 Source。")]
    [DefaultValue(null)]
    public Uri? PreviewSource { get; set; }

    /// <summary>大图下方的说明。</summary>
    [Category("Behavior")]
    [Description("大图下方的说明。")]
    [DefaultValue(null)]
    public string? Caption { get; set; }

    /// <summary>大图下载时建议的文件名；不写时取地址里的文件名。</summary>
    [Category("Behavior")]
    [Description("大图下载时建议的文件名；不写时取地址里的文件名。")]
    [DefaultValue(null)]
    public string? DownloadName { get; set; }

    /// <summary>大图里显示下载按钮，默认 true。</summary>
    [Category("Behavior")]
    [Description("大图里显示下载按钮。")]
    [DefaultValue(true)]
    public bool Downloadable { get; set; } = true;

    /// <summary>读不了时显示的文字；默认是 alt（图标在上面）。</summary>
    [Category("Appearance")]
    [Description("读不了时显示的文字；默认是 alt。")]
    [DefaultValue(null)]
    public string? Fallback { get; set { field = value; Invalidate(); } }

    /// <summary>加载状态。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public ImageStatus Status
    {
        get;
        private set
        {
            if (field == value) return;
            field = value;
            shimmer.Visible = value == ImageStatus.Loading;
            UpdateRole();
            Invalidate();
            StatusChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>加载状态变了（读完、读失败、换了地址重新读）。</summary>
    public event EventHandler? StatusChanged;

    /// <summary>可以点开看大图（写了 Preview、没有读失败）。</summary>
    internal bool Previewable => Preview && Status != ImageStatus.Error;

    /// <summary>自己的预览（不在 ImageGroupControl 里时；测试用）。</summary>
    internal LightboxControl? OwnLightbox => lightbox;

    /// <summary>大图里的这一张。</summary>
    internal LightboxItem Item => new(PreviewSource ?? Source ?? new Uri("about:blank"), Alt ?? "")
    {
        Caption = Caption,
        DownloadName = DownloadName,
        Downloadable = Downloadable,
    };

    /// <summary>打开大图：在 ImageGroupControl 里时交给组（从这一张开始），否则用自己的 Lightbox。</summary>
    public void OpenPreview()
    {
        if (!Previewable) return;
        for (var node = Parent; node is not null; node = node.Parent)
            if (node is ImageGroupControl group)
            {
                group.Open(this);
                return;
            }
        lightbox ??= new LightboxControl();
        if (lightbox.IsOpen) return;
        lightbox.Items = [Item];
        lightbox.Index = 0;
        _ = lightbox.ShowAsync(this);
    }

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (Status != ImageStatus.Loaded) _ = Load();
    }

    /// <summary>读图：读的时候占位扫光；换了地址时只认最后一次。</summary>
    async Task Load()
    {
        var version = ++loading;
        Status = ImageStatus.Loading;
        if (Source is not { } source)
        {
            Status = ImageStatus.Error;
            return;
        }
        try
        {
            var bitmap = await ImageLoader.LoadAsync(source);
            if (version != loading || IsDisposed) return;
            picture = bitmap;
            Status = ImageStatus.Loaded;
            if (AutoSize) Parent?.PerformLayout();
        }
        catch (Exception error)
        {
            if (version != loading || IsDisposed) return;
            System.Diagnostics.Trace.TraceWarning($"ImageControl 读不了 {source}：{error.Message}");
            picture = null;
            Status = ImageStatus.Error;
        }
    }

    /// <summary>打开 AutoSize 时按图片的大小（图片的一个像素是 1/96 英寸，跟着 DPI 放大）。</summary>
    public override Size GetPreferredSize(Size proposedSize) =>
        picture is { } image ? new Size(LogicalToDeviceUnits(image.Width), LogicalToDeviceUnits(image.Height)) : Size.Empty;

    void UpdateRole()
    {
        SetStyle(ControlStyles.Selectable, Previewable);
        TabStop = Previewable;
        Cursor = Previewable ? Cursors.Hand : Cursors.Default;
        AccessibilityNotifyClients(AccessibleEvents.NameChange, -1);
        Invalidate();
    }

    /// <summary>图片按 <see cref="Fit"/> 放进框里的矩形（框的坐标）。</summary>
    RectangleF Placement(Bitmap image, RectangleF box)
    {
        var (width, height) = (LogicalToDeviceUnits(image.Width), LogicalToDeviceUnits(image.Height));
        var scale = Fit.Value switch
        {
            "contain" => Math.Min(box.Width / width, box.Height / height),
            "scale-down" => Math.Min(1, Math.Min(box.Width / width, box.Height / height)),
            "none" => 1,
            "fill" => float.NaN,
            _ => Math.Max(box.Width / width, box.Height / height),
        };
        if (float.IsNaN(scale)) return box;
        var (w, h) = (width * scale, height * scale);
        return new RectangleF(box.X + (box.Width - w) / 2, box.Y + (box.Height - h) / 2, w, h);
    }

    /// <summary>底色 fill（圆角）；读完画图片（按圆角裁），读不了画图标与 alt；能预览时悬停压暗 10%，键盘聚焦时画焦点环。</summary>
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var context = ElementContext.Of(this);
        var colors = context.Theme.Colors;
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var box = new RectangleF(0, 0, Width, Height);
        var radius = Radius.Value == "full" ? Math.Min(box.Width, box.Height) / 2 : (Radius.In(context.Theme.Radii) ?? 0) * context.Em;
        using var shape = Drawing.RoundedRectangle(box, radius);
        using (var fill = new SolidBrush(colors.Fill)) graphics.FillPath(fill, shape);
        if (Status == ImageStatus.Loaded && picture is { } image)
        {
            var state = graphics.Save();
            graphics.SetClip(shape);
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.DrawImage(image, Placement(image, box));
            graphics.Restore(state);
        }
        else if (Status == ImageStatus.Error) PaintBroken(graphics, context);
        if (hovered && Previewable)
            using (var dim = new SolidBrush(Color.FromArgb(26, 0, 0, 0))) graphics.FillPath(dim, shape);
        if (Focused && ShowFocusCues && Previewable)
        {
            var width = context.Scale(2);
            using var ring = Drawing.RoundedRectangle(RectangleF.Inflate(box, -width / 2, -width / 2), Math.Max(0, radius - width / 2));
            using var pen = new Pen(colors.FocusRing, width);
            graphics.DrawPath(pen, ring);
        }
    }

    /// <summary>读不了：图标在上、alt（或 Fallback）在下，xs 字号、textMuted，整块居中，四周留 sm。</summary>
    void PaintBroken(Graphics graphics, ElementContext context)
    {
        var small = context.Sized(TextSize.Xs) with { ForeColor = context.Theme.Colors.TextMuted };
        brokenText.Content = Fallback ?? Alt ?? "";
        var pad = (int)MathF.Round(Tokens.Space.Sm * context.Em);
        var gap = (int)MathF.Round(Tokens.Space.Xs * small.Em);
        var room = new Size(Math.Max(0, Width - 2 * pad), Math.Max(0, Height - 2 * pad));
        var icon = brokenIcon.Measure(small, room);
        var text = brokenText.Measure(small, room);
        var top = (Height - icon.Height - gap - text.Height) / 2;
        brokenIcon.Bounds = new Rectangle((Width - icon.Width) / 2, top, icon.Width, icon.Height);
        brokenText.Bounds = new Rectangle((Width - text.Width) / 2, top + icon.Height + gap, text.Width, text.Height);
        brokenIcon.Paint(graphics, small);
        brokenText.Paint(graphics, small);
    }

    /// <inheritdoc />
    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        hovered = true;
        if (Previewable) Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hovered = false;
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || !Previewable) return;
        pressed = true;
        Focus();
    }

    /// <summary>在图片上按下又抬起才打开（同按钮：按下后移出去再抬起不算）。</summary>
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!pressed) return;
        pressed = false;
        if (ClientRectangle.Contains(e.Location)) OpenPreview();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !Previewable || e.KeyCode is not (Keys.Enter or Keys.Space)) return;
        OpenPreview();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override bool IsInputKey(Keys keyData) => keyData is Keys.Enter || base.IsInputKey(keyData);

    /// <inheritdoc />
    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        // 图片在缓存里共用，不在这里释放
        if (disposing) lightbox?.Dispose();
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new ImageAccessibleObject(this);

    /// <summary>能预览时是按钮（名称“查看大图：alt”，默认动作是打开），否则是一张图片（名称是 alt）；读不了时说明“图片加载失败”。</summary>
    sealed class ImageAccessibleObject(ImageControl owner) : ControlAccessibleObject(owner)
    {
        public override AccessibleRole Role => owner.Previewable ? AccessibleRole.PushButton : AccessibleRole.Graphic;

        public override string? Name => owner.AccessibleName is { Length: > 0 } name ? name
            : owner.Previewable ? ProviderControl.LangOf(owner).ImagePreview(owner.Alt ?? "") : owner.Alt;

        public override string? Description => owner.Status == ImageStatus.Error ? ProviderControl.LangOf(owner).ImageError : base.Description;

        public override string? DefaultAction => owner.Previewable ? "Press" : null;

        public override void DoDefaultAction()
        {
            if (owner.Previewable) owner.OpenPreview();
        }
    }
}

/// <summary>
/// 一组图片（vue-tsx-pro 的 ImageGroup）：换行排列（间距 sm），里面能预览的 <see cref="ImageControl"/> 共用一个 <see cref="LightboxControl"/>，从点的那张开始，大图里左右切换。
/// <code>
/// var group = new ImageGroupControl { Loop = true };
/// group.Controls.AddRange([.. photos.Select(photo => new ImageControl { Source = photo.Thumb, Alt = photo.Title, Preview = true, PreviewSource = photo.Url })]);
/// </code>
/// 图片可以夹在别的控件里（图片加名称的一格）：打开时按控件的先后顺序收集组里能预览的图。共用的预览是 <see cref="Lightbox"/>（TS 的 propsLightbox）。
/// </summary>
[Description("一组图片：换行排列，能预览的图片共用一个大图预览、左右切换。")]
public class ImageGroupControl : FlexControl
{
    /// <summary>新建：换行排列、间距 sm。</summary>
    public ImageGroupControl()
    {
        Wrap = true;
        Gap = Space.Sm;
    }

    /// <summary>到头之后从另一头继续，默认 false。</summary>
    [Category("Behavior")]
    [Description("大图里到头之后从另一头继续。")]
    [DefaultValue(false)]
    public bool Loop { get; set; }

    /// <summary>共用的预览。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public LightboxControl Lightbox { get; } = new();

    /// <summary>从 <paramref name="image"/> 那张开始预览组里能预览的图。</summary>
    internal void Open(ImageControl image)
    {
        if (Lightbox.IsOpen) return;
        var images = Descendants(this).Where(item => item.Preview).ToList();
        Lightbox.Items = [.. images.Select(item => item.Item)];
        Lightbox.Index = Math.Max(0, images.IndexOf(image));
        Lightbox.Loop = Loop;
        _ = Lightbox.ShowAsync(image);
    }

    /// <summary>按控件的先后顺序（先序）找组里的图片。</summary>
    static IEnumerable<ImageControl> Descendants(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is ImageControl image) yield return image;
            else foreach (var nested in Descendants(child)) yield return nested;
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) Lightbox.Dispose();
        base.Dispose(disposing);
    }
}
