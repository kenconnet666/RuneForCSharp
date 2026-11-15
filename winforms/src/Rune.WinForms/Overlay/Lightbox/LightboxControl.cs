using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace Rune.WinForms;

/// <summary>
/// 图片预览（vue-tsx-pro 的 Lightbox）：铺满窗体的深色模态层上看大图——缩放、拖动、旋转、还原、下载，几张图时左右切换。
/// <see cref="ImageControl"/> 的 Preview 打开它（同一个 <see cref="ImageGroupControl"/> 里的图共用一个），也可以单独用（附件列表里的“预览”）。
/// <code>
/// var viewer = new LightboxControl { Items = attachments.Select(file => new LightboxItem(file.Url, file.Name)).ToList() };
/// preview.Click += (_, _) => { viewer.Index = list.SelectedIndex; viewer.IsOpen = true; };
/// </code>
/// 内核同 <see cref="DialogControl"/>（模态、Esc、关上后还回焦点、<see cref="DialogControl.BeforeClose"/>、命令式 ShowAsync），面板是平的、铺满窗体：工具栏（计数、缩小、比例、放大、
/// 向左 / 向右旋转、还原、下载、关闭）、图片区、说明，两侧是上一张、下一张。背景总是 scrimStrong、文字 onScrim（看图片本身的颜色，不跟主题变）。
/// 平移与缩放建在 <see cref="PanZoom"/> 上（0.05–8 倍）：拖动平移，滚轮以指针为中心缩放，双击放大 / 还原；打开、换一张、旋转、还原之后整张图放进视口
/// （四周留 24，不超过原大）。键盘：← / → 上一张、下一张（从右到左排版时反过来），+ / − 缩放，0 原大，↑ / ↓ 平移，R 向右旋转，Esc 关闭。
/// 打开时预先读前后两张。下载按钮弹出保存对话框，存原始文件。读屏：对话框名称是“图片预览”，图片区是一张图片、名称是 alt。
/// </summary>
[Description("图片预览：铺满窗体看大图，缩放、拖动、旋转、下载，几张图时左右切换。")]
public class LightboxControl : DialogControl
{
    /// <summary>打开、换一张、旋转之后整张放进视口时四周留的空白（设备无关像素）。</summary>
    const float FitPadding = 24;

    readonly LightboxView view;
    readonly PanZoom panZoom;
    Bitmap? picture;
    int rotation;
    int loading;
    Uri? shown;
    bool fitPending;

    /// <summary>新建：面板换成工具栏、图片区、说明与两侧的切换按钮。</summary>
    public LightboxControl()
    {
        view = new LightboxView(this) { Dock = DockStyle.Fill };
        panZoom = new PanZoom(() => (view.Stage.Width, view.Stage.Height))
        {
            Min = 0.05,
            Max = 8,
            Bounds = () => picture is null ? null : Box,
        };
        panZoom.Changed += () =>
        {
            view.Zoom.Text = $"{Math.Round(panZoom.State.Scale * 100)}%";
            view.Stage.Invalidate();
        };
        ContentLayout.Visible = false;
        PanelForm.Controls.Add(view);
        HeaderHidden = true;
    }

    /// <summary>图片。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IReadOnlyList<LightboxItem>? Items { get; set { field = value; ShowCurrent(); } }

    /// <summary>当前第几张，从 0 开始。</summary>
    [Category("Behavior")]
    [Description("当前第几张，从 0 开始。")]
    [DefaultValue(0)]
    public int Index
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            ShowCurrent();
            IndexChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>到头之后从另一头继续，默认 false。</summary>
    [Category("Behavior")]
    [Description("到头之后从另一头继续。")]
    [DefaultValue(false)]
    public bool Loop { get; set { field = value; ShowCurrent(); } }

    /// <summary>用户换了一张（← / →、两侧按钮）。</summary>
    public event EventHandler? IndexChanged;

    /// <summary>平移与缩放（测试用）。</summary>
    internal PanZoom View => panZoom;

    /// <summary>旋转的角度：0、90、180、270（测试用）。</summary>
    internal int Rotation => rotation;

    /// <summary>界面（测试用）。</summary>
    internal LightboxView Parts => view;

    /// <summary>当前这一张（序号夹在范围里）。</summary>
    LightboxItem? Current => Items is { Count: > 0 } items ? items[Math.Clamp(Index, 0, items.Count - 1)] : null;

    /// <summary>旋转之后图片占的框（图片的一个像素是 1/96 英寸，跟着 DPI 放大）。</summary>
    (double Width, double Height) Box
    {
        get
        {
            if (picture is null) return (0, 0);
            var (width, height) = (view.Stage.LogicalToDeviceUnits(picture.Width), view.Stage.LogicalToDeviceUnits(picture.Height));
            return rotation % 180 == 0 ? (width, height) : (height, width);
        }
    }

    /// <inheritdoc />
    private protected override Control? InitialFocus => view.Stage;

    /// <inheritdoc />
    private protected override bool Flat => true;

    /// <inheritdoc />
    private protected override bool SquarePanel => true;

    /// <summary>铺满宿主窗体的客户区。</summary>
    private protected override Rectangle PanelBounds(Rectangle area, ElementContext context, int border) => area;

    /// <summary>面板显示之后：按语言包写名称，读当前这一张。</summary>
    private protected override void ArrangeExtras()
    {
        if (Title is null) Title = ProviderControl.LangOf(this).LightboxLabel;
        view.Localize(ProviderControl.LangOf(this));
        ShowCurrent();
    }

    /// <summary>换一张（或刚打开）：方向回正，读原图；计数、说明、两侧按钮、下载按钮跟着；预先读前后两张。</summary>
    void ShowCurrent()
    {
        if (!IsOpen) return;
        var item = Current;
        var total = Items?.Count ?? 0;
        view.Counter.Text = total > 1 ? ProviderControl.LangOf(this).SlideCounter(Index + 1, total) : "";
        view.Previous.Visible = total > 1 && (Loop || Index > 0);
        view.Next.Visible = total > 1 && (Loop || Index < total - 1);
        view.Download.Visible = item is { Downloadable: true };
        view.Caption.Text = item?.Caption?.ToString() ?? "";
        view.Caption.Visible = item?.Caption is not null;
        view.Stage.AccessibleName = item?.Alt;
        view.PerformLayout();
        if (shown != item?.Source) _ = Load(item?.Source);
        if (Items is { } items)
            foreach (var step in new[] { -1, 1 })
                if (Index + step >= 0 && Index + step < items.Count) ImageLoader.LoadAsync(items[Index + step].Source).ContinueWith(task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
    }

    /// <summary>读一张图：读到之前不显示；读完量原大，整张放进视口。换得快时只认最后一次。</summary>
    async Task Load(Uri? source)
    {
        var version = ++loading;
        shown = source;
        picture = null;
        rotation = 0;
        view.Stage.Invalidate();
        if (source is null) return;
        try
        {
            var bitmap = await ImageLoader.LoadAsync(source);
            if (version != loading || IsDisposed) return;
            picture = bitmap;
            Fit();
        }
        catch (Exception error)
        {
            if (version == loading) System.Diagnostics.Trace.TraceWarning($"LightboxControl 读不了图片 {source}：{error.Message}");
        }
    }

    /// <summary>整张放进视口（四周留 24，不超过原大）；图片区还没有大小时等它排好（<see cref="LightboxStage"/> 变了大小时再做）。</summary>
    void Fit()
    {
        fitPending = picture is not null && (view.Stage.Width <= 0 || view.Stage.Height <= 0);
        if (picture is null || fitPending) return;
        panZoom.Fit(Box, view.Stage.LogicalToDeviceUnits((int)FitPadding));
        view.Zoom.Text = $"{Math.Round(panZoom.State.Scale * 100)}%";
        view.Stage.Invalidate();
    }

    /// <summary>上一张、下一张：到头时 Loop 才从另一头继续。</summary>
    internal void Go(int step)
    {
        var total = Items?.Count ?? 0;
        if (total < 2) return;
        var target = Index + step;
        if (target < 0 || target >= total)
        {
            if (!Loop) return;
            target = (target + total) % total;
        }
        Index = target;
    }

    /// <summary>以 90° 为一档旋转，转完整张放进视口。</summary>
    internal void Rotate(int step)
    {
        rotation = (rotation + step * 90 + 360) % 360;
        Fit();
    }

    /// <summary>还原：方向回正、整张放进视口。</summary>
    internal void ResetView()
    {
        rotation = 0;
        Fit();
    }

    /// <summary>下载：弹出保存对话框（建议的文件名取 DownloadName 或地址里的名字），存原始文件。</summary>
    internal async Task Download()
    {
        if (Current is not { } item) return;
        using var dialog = new SaveFileDialog { FileName = item.SuggestedFileName };
        if (dialog.ShowDialog(PanelForm) != DialogResult.OK) return;
        try
        {
            await File.WriteAllBytesAsync(dialog.FileName, await ImageLoader.ReadAsync(item.Source));
        }
        catch (Exception error)
        {
            System.Diagnostics.Trace.TraceWarning($"LightboxControl 下载 {item.Source} 失败：{error.Message}");
        }
    }

    /// <summary>
    /// 按键（焦点在面板里任何地方）：← / → 上一张、下一张（从右到左排版时反过来），R 向右旋转；焦点在图片区时还有 + / = 放大、− 缩小、0 原大、↑ / ↓ 平移。
    /// 带 Ctrl、Alt 的不管。返回是否处理了。
    /// </summary>
    internal bool Press(Keys keyData)
    {
        if ((keyData & (Keys.Control | Keys.Alt)) != 0) return false;
        var key = keyData & Keys.KeyCode;
        if (key is Keys.Left or Keys.Right)
        {
            var rtl = RightToLeft == RightToLeft.Yes;
            Go((key == Keys.Right) != rtl ? 1 : -1);
            return true;
        }
        if (key == Keys.R)
        {
            Rotate(1);
            return true;
        }
        if (!view.Stage.Focused) return false;
        PanZoomKey? action = key switch
        {
            Keys.Oemplus or Keys.Add => PanZoomKey.ZoomIn,
            Keys.OemMinus or Keys.Subtract => PanZoomKey.ZoomOut,
            Keys.D0 or Keys.NumPad0 => PanZoomKey.Reset,
            Keys.Up => PanZoomKey.Up,
            Keys.Down => PanZoomKey.Down,
            _ => null,
        };
        if (action is not { } pressed) return false;
        panZoom.Press(pressed);
        return true;
    }

    /// <summary>画图片区：先缩放、再平移，图片在旋转后的框里居中旋转；拖动时用快一点的插值。</summary>
    internal void PaintStage(Graphics graphics, bool dragging)
    {
        if (picture is not { } image) return;
        var state = panZoom.State;
        var (boxWidth, boxHeight) = Box;
        var (width, height) = (view.Stage.LogicalToDeviceUnits(image.Width), view.Stage.LogicalToDeviceUnits(image.Height));
        graphics.InterpolationMode = dragging ? InterpolationMode.Bilinear : InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.TranslateTransform((float)state.X, (float)state.Y);
        graphics.ScaleTransform((float)state.Scale, (float)state.Scale);
        graphics.TranslateTransform((float)boxWidth / 2, (float)boxHeight / 2);
        graphics.RotateTransform(rotation);
        graphics.DrawImage(image, -width / 2f, -height / 2f, width, height);
        graphics.ResetTransform();
    }

    /// <summary>
    /// 面板里的界面：工具栏在上、说明在下、图片区在中间，上一张、下一张贴着两侧、上下居中。底色是 scrimStrong 叠在主题背景上
    /// （面板是不透明的窗口，透不出下面的窗体），文字与图标 onScrim。
    /// </summary>
    internal sealed class LightboxView : Control
    {
        readonly LightboxControl owner;

        public LightboxView(LightboxControl owner)
        {
            this.owner = owner;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Stage = new LightboxStage(owner);
            Toolbar.Controls.AddRange([Counter, ZoomOut, Zoom, ZoomIn, RotateLeft, RotateRight, Reset, Download, Close]);
            Toolbar.SetGrow(Counter, true);
            Controls.AddRange([Previous, Next, Toolbar, Caption, Stage]);
            ZoomOut.Click += (_, _) => owner.panZoom.ZoomOut();
            ZoomIn.Click += (_, _) => owner.panZoom.ZoomIn();
            RotateLeft.Click += (_, _) => owner.Rotate(-1);
            RotateRight.Click += (_, _) => owner.Rotate(1);
            Reset.Click += (_, _) => owner.ResetView();
            Download.Click += (_, _) => _ = owner.Download();
            Close.Click += (_, _) => _ = owner.RequestClose(OpenChangeReason.Close);
            Previous.Click += (_, _) => owner.Go(-1);
            Next.Click += (_, _) => owner.Go(1);
        }

        public FlexControl Toolbar { get; } = new() { Align = FlexAlign.Center, Justify = FlexJustify.End, Gap = Space.Xs, AutoSize = false };

        public SmallControl Counter { get; } = new() { Numeric = true };

        public SmallControl Zoom { get; } = new() { Numeric = true, Align = TextAlign.Center, AutoSize = false };

        public IconButtonControl ZoomOut { get; } = Tool(Lucide.ZoomOut);

        public IconButtonControl ZoomIn { get; } = Tool(Lucide.ZoomIn);

        public IconButtonControl RotateLeft { get; } = Tool(Lucide.RotateCcw);

        public IconButtonControl RotateRight { get; } = Tool(Lucide.RotateCw);

        public IconButtonControl Reset { get; } = Tool(Lucide.Undo2);

        public IconButtonControl Download { get; } = Tool(Lucide.Download);

        public IconButtonControl Close { get; } = Tool(Lucide.X);

        public IconButtonControl Previous { get; } = Tool(Lucide.ChevronLeft, Radius.Full);

        public IconButtonControl Next { get; } = Tool(Lucide.ChevronRight, Radius.Full);

        public SmallControl Caption { get; } = new() { Align = TextAlign.Center, Visible = false };

        public LightboxStage Stage { get; }

        static IconButtonControl Tool(IconData icon, Radius radius = default) => new() { Icon = icon, Radius = radius };

        /// <summary>按钮的读屏名称取语言包。</summary>
        public void Localize(Lang lang)
        {
            (ZoomOut.AccessibleName, ZoomIn.AccessibleName) = (lang.ZoomOut, lang.ZoomIn);
            (RotateLeft.AccessibleName, RotateRight.AccessibleName) = (lang.LightboxRotateLeft, lang.LightboxRotateRight);
            (Reset.AccessibleName, Download.AccessibleName, Close.AccessibleName) = (lang.ResetView, lang.LightboxDownload, lang.Close);
            (Previous.AccessibleName, Next.AccessibleName) = (lang.LightboxPrevious, lang.LightboxNext);
            Stage.AccessibleDescription = null;
        }

        /// <summary>颜色：底色 scrimStrong 叠在主题背景上，文字 onScrim；按钮的文字同色、悬停时 12% 的底。</summary>
        void ApplyColors()
        {
            var colors = ElementContext.Of(this).Theme.Colors;
            BackColor = Drawing.Mix(colors.Background, Color.FromArgb(255, colors.ScrimStrong), colors.ScrimStrong.A / 255d);
            ForeColor = colors.OnScrim;
            foreach (var button in new[] { ZoomOut, ZoomIn, RotateLeft, RotateRight, Reset, Download, Close, Previous, Next }) Hover(button, false);
        }

        void Hover(IconButtonControl button, bool hovered)
        {
            var fore = ForeColor;
            var sx = new ButtonSx { Foreground = fore, Background = hovered ? Color.FromArgb((int)MathF.Round(fore.A * 0.12f), fore) : null };
            if (button.Sx != sx) button.Sx = sx;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyColors();
            foreach (var button in new[] { ZoomOut, ZoomIn, RotateLeft, RotateRight, Reset, Download, Close, Previous, Next })
            {
                button.MouseEnter += (_, _) => Hover(button, true);
                button.MouseLeave += (_, _) => Hover(button, false);
            }
        }

        /// <summary>工具栏上下 sm、左右 md；说明上下 md、左右 xl；两侧按钮离边 md。</summary>
        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            var em = ElementContext.Of(this).Em;
            int Space(float factor) => (int)MathF.Round(factor * em);
            var (sm, md, xl) = (Space(Tokens.Space.Sm), Space(Tokens.Space.Md), Space(Tokens.Space.Xl));
            Zoom.Width = (int)MathF.Ceiling(3.5f * Zoom.Font.EmPixels(DeviceDpi) * Tokens.FontSize.Sm / Tokens.FontSize.Md);
            Zoom.Height = Zoom.GetPreferredSize(new Size(Zoom.Width, 0)).Height;
            var toolbarHeight = Toolbar.GetPreferredSize(new Size(Width - 2 * md, 0)).Height;
            Toolbar.SetBounds(md, sm, Math.Max(0, Width - 2 * md), toolbarHeight);
            var top = toolbarHeight + 2 * sm;
            var bottom = Height;
            if (Caption.Visible)
            {
                var height = Caption.GetPreferredSize(new Size(Math.Max(0, Width - 2 * xl), 0)).Height;
                Caption.SetBounds(xl, Height - md - height, Math.Max(0, Width - 2 * xl), height);
                bottom = Caption.Top - md;
            }
            Stage.SetBounds(0, top, Width, Math.Max(0, bottom - top));
            foreach (var (button, left) in new[] { (Previous, true), (Next, false) })
            {
                var size = button.GetPreferredSize(Size.Empty);
                button.SetBounds(left ? md : Width - md - size.Width, (Height - size.Height) / 2, size.Width, size.Height);
                button.BringToFront();
            }
        }

        /// <summary>← / → 、R 在面板里哪里按都算；图片区还有缩放、平移的键（方向键不让窗体拿去移动焦点）。</summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData) => owner.Press(keyData) || base.ProcessCmdKey(ref msg, keyData);
    }

    /// <summary>图片区：拖动平移（挪开 3 以内不算拖）、滚轮以指针为中心缩放、双击放大 / 还原；读屏是一张图片（名称是 alt）。</summary>
    internal sealed class LightboxStage : Control
    {
        readonly LightboxControl owner;
        Point? pressedAt;
        Point lastDrag;
        bool dragging;

        public LightboxStage(LightboxControl owner)
        {
            this.owner = owner;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw
                     | ControlStyles.Selectable | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.Graphic;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SetClip(ClientRectangle);
            owner.PaintStage(e.Graphics, dragging);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (e.Button != MouseButtons.Left) return;
            pressedAt = lastDrag = e.Location;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (pressedAt is not { } from) return;
            if (!dragging)
            {
                var (dx, dy) = (e.X - from.X, e.Y - from.Y);
                if (dx * dx + dy * dy < LogicalToDeviceUnits(3) * LogicalToDeviceUnits(3)) return;
                dragging = true;
                Cursor = Cursors.SizeAll;
            }
            owner.panZoom.PanBy(e.X - lastDrag.X, e.Y - lastDrag.Y);
            lastDrag = e.Location;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            pressedAt = null;
            dragging = false;
            Cursor = Cursors.Hand;
            Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left) owner.panZoom.DoubleTap((e.X, e.Y));
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            owner.panZoom.Wheel(-e.Delta / 120d, (e.X, e.Y));
            if (e is HandledMouseEventArgs handled) handled.Handled = true;
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (owner.fitPending) owner.Fit();
        }
    }
}
