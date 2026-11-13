using System.Collections;
using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>执行了命令面板里一项的事件参数。</summary>
/// <param name="item">ItemsSource 里的那一项。</param>
public sealed class CommandPaletteItemEventArgs(object? item) : EventArgs
{
    /// <summary>执行的那一项。</summary>
    public object? Item { get; } = item;
}

/// <summary>
/// 命令面板（vue-tsx-pro 的 CommandPalette）：快捷键（默认 Mod+K，Windows 上是 Ctrl+K）或点触发按钮打开的搜索对话框，边输入边过滤，Enter 执行。
/// <code>
/// var palette = new CommandPaletteControl { ItemsSource = pages, ToOption = PageOption };
/// palette.ItemInvoked += (_, e) => Go((Page)e.Item!);
/// </code>
/// 结构是 sm 宽、停在窗体上部的 <see cref="DialogControl"/>（标题只给读屏，是面板的名称），里面一行搜索框（lg 字号、下边分隔线）与不可聚焦的
/// <see cref="ListboxControl"/>（最高 min(24rem, 窗体高度一半)）：焦点一直在搜索框，↑↓、Page Up / Down 移动高亮，Enter 执行；数据层同 Select（<see cref="ListState"/>）。
/// 面板不记选择：执行就是选中，<see cref="ItemInvoked"/> 报告那一项，面板随即关闭。默认按选项文字与说明过滤（忽略大小写与重音），<see cref="Filter"/> 自定义
/// （远程搜索：读 <see cref="InputText"/> 取数据，Filter 一律返回 true，取数据时给 <see cref="IsLoading"/>）。每次打开清空输入；输入、结果变了高亮第一个。
/// 触发按钮默认画成输入框外壳（放大镜、占位文字、快捷键的 Kbd），<see cref="Trigger"/> 换成自己的控件（点它即 Click 打开），设为 null 时只用快捷键。
/// 快捷键由应用的消息过滤器接（焦点在所在窗体或面板里时有效）。选项是 ListboxControl 画出来的，没有自定义内容；没有匹配时的说明是 <see cref="EmptyText"/>。
/// </summary>
[DefaultEvent(nameof(ItemInvoked))]
public class CommandPaletteControl : Control
{
    readonly DialogControl dialog = new() { DialogSize = DialogSize.Sm, Closable = false, HeaderHidden = true, TopFraction = 0.12f, BodyFlush = true };
    readonly PaletteBody body;
    readonly TextFieldControl input = new() { Icon = Lucide.Search, Bare = true };
    readonly ListboxControl listbox = new() { Embedded = true };
    readonly ButtonControl defaultTrigger = new() { Variant = ButtonVariant.Surface, Tone = StatusTone.Neutral, Icon = Lucide.Search };
    readonly Kbd triggerKbd = new();
    readonly HotkeyFilter filter;
    IReadOnlyList<object?> all = [];
    bool open;
    bool syncingInput;

    /// <summary>新建：默认触发按钮、对话框（搜索行与列表）。</summary>
    public CommandPaletteControl()
    {
        SetStyle(ControlStyles.Selectable, false);
        SetStyle(ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
        AutoSize = true;
        body = new PaletteBody(this);
        dialog.Content = body;
        dialog.OpenChanged += (_, e) => OnDialogChanged(e);
        defaultTrigger.Element.Trailing = triggerKbd;
        defaultTrigger.Element.MutedForeground = true;
        input.TextChanged += (_, _) =>
        {
            if (!syncingInput) InputText = input.Text;
        };
        input.Input.KeyDown += OnInputKeyDown;
        input.Input.PreviewKeyDown += (_, e) => e.IsInputKey |= e.KeyCode is Keys.Up or Keys.Down;
        listbox.Picked += (_, e) => Pick(e.Value);
        filter = new HotkeyFilter(this);
        Controls.Add(dialog);
        Trigger = defaultTrigger;
        Sync();
    }

    /// <summary>开着；快捷键、触发按钮、执行、Esc 都会改它。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsOpen { get => open; set => SetOpen(value, null); }

    /// <summary>数据：ListboxOption、字符串，或任意数据加 <see cref="ToOption"/>。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public IEnumerable? ItemsSource
    {
        get;
        set
        {
            field = value;
            all = value?.Cast<object?>().ToList() ?? [];
            UpdateItems();
        }
    }

    /// <summary>数据 → 选项。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object?, ListboxOption>? ToOption { get; set { field = value; UpdateItems(); } }

    /// <summary>怎样算匹配（数据、输入）；不写时选项文字或说明包含输入（忽略大小写与重音）。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Func<object?, string, bool>? Filter { get; set { field = value; UpdateItems(); } }

    /// <summary>输入框里的文字（远程搜索时据此取数据，变了发 <see cref="InputTextChanged"/>）；每次打开清空。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string InputText
    {
        get;
        set
        {
            value ??= "";
            if (field == value) return;
            field = value;
            syncingInput = true;
            input.Text = value;
            syncingInput = false;
            UpdateItems();
            InputTextChanged?.Invoke(this, EventArgs.Empty);
        }
    } = "";

    /// <summary>正在取结果：列表换成骨架行。</summary>
    [Category("Behavior")]
    [DefaultValue(false)]
    public bool IsLoading
    {
        get => listbox.IsLoading;
        set
        {
            listbox.IsLoading = value;
            dialog.Relayout();
        }
    }

    /// <summary>打开面板的快捷键，默认 Mod+K；null 时不注册。</summary>
    [Category("Behavior")]
    [DefaultValue("Mod+K")]
    public string? Hotkey { get; set { field = value; Sync(); } } = "Mod+K";

    /// <summary>搜索框（与默认触发按钮）的占位文字；不写时取语言包（搜索文档）。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? Placeholder { get; set { field = value; Sync(); } }

    /// <summary>面板的名称（读屏念的）；不写时取语言包（搜索）。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? Label { get; set { field = value; Sync(); } }

    /// <summary>没有匹配时列表里的说明；不写时取语言包（没有找到相关内容）。</summary>
    [Category("Appearance")]
    [DefaultValue(null)]
    public string? EmptyText { get; set { field = value; Sync(); } }

    /// <summary>触发控件（点它即 Click 打开）；默认画成输入框外壳的按钮，null 时只用快捷键。控件的大小就是它的大小。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Trigger
    {
        get;
        set
        {
            if (field == value) return;
            if (field is not null)
            {
                field.Click -= OnTriggerClick;
                Controls.Remove(field);
            }
            field = value;
            if (value is not null)
            {
                value.Click += OnTriggerClick;
                Controls.Add(value);
            }
            PerformLayout();
        }
    }

    /// <summary>执行了一项（Enter 或点击）：面板随即关闭。</summary>
    public event EventHandler<CommandPaletteItemEventArgs>? ItemInvoked;

    /// <summary>用户开、关了（快捷键、触发按钮、执行、Esc、点遮罩）；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged;

    /// <summary>输入框里的文字变了（远程搜索据此取数据）。</summary>
    public event EventHandler? InputTextChanged;

    /// <summary>对话框（测试用）。</summary>
    internal DialogControl Dialog => dialog;

    /// <summary>搜索框（测试用）。</summary>
    internal TextFieldControl Input => input;

    /// <summary>列表（测试用）。</summary>
    internal ListboxControl List => listbox;

    /// <summary>默认的触发按钮（测试用）。</summary>
    internal ButtonControl DefaultTrigger => defaultTrigger;

    float Em => Font.SizeInPoints * DeviceDpi / 72f;

    /// <summary>名称、占位文字、快捷键的显示与读屏写法。</summary>
    void Sync()
    {
        var lang = ProviderControl.LangOf(this);
        var placeholder = Placeholder ?? lang.SearchPlaceholder;
        var label = Label ?? lang.SearchLabel;
        dialog.Title = label;
        input.PlaceholderText = placeholder;
        input.AccessibleName = label;
        defaultTrigger.Text = placeholder;
        var hotkey = Hotkey is { } combo ? global::Rune.WinForms.Hotkey.Parse(combo) : null;
        triggerKbd.Content = hotkey?.Text ?? "";
        defaultTrigger.Element.Trailing = hotkey is null ? null : triggerKbd;
        // 名称来自看得见的文字，快捷键由读屏的快捷键属性念（显示它的 Kbd 不算进名称）
        defaultTrigger.AccessibleName = placeholder;
        defaultTrigger.KeyboardShortcut = hotkey?.AccessKey;
        listbox.EmptyText = EmptyText ?? lang.SearchEmpty;
        defaultTrigger.MinimumSize = new Size((int)MathF.Round(ElementContext.Of(this).Scale(12 * 16)), 0);
    }

    /// <summary>列表里的数据：输入不空时按过滤留下匹配的；高亮第一个；面板跟着内容变高变矮。</summary>
    void UpdateItems()
    {
        listbox.ToOption = ToOption;
        var text = InputText;
        listbox.ItemsSource = text.Length == 0 ? all : all.Where(item => Matches(item, text)).ToList();
        listbox.Value = null;
        listbox.State.MoveToEnd(last: false);
        body.PerformLayout();
        dialog.Relayout();
    }

    bool Matches(object? item, string text)
    {
        if (Filter is { } match) return match(item, text);
        var option = ListboxOption.From(item, ToOption);
        return SelectSearch.Matches(option.Label, text) || SelectSearch.Matches(option.Description ?? "", text);
    }

    /// <summary>开关：每次打开清空输入、高亮第一个（对话框把焦点给搜索框）。</summary>
    void SetOpen(bool next, OpenChangeReason? reason)
    {
        if (next == open) return;
        open = next;
        if (next)
        {
            var host = FindForm();
            listbox.ScrollMaxHeight = (int)Math.Min(ElementContext.Of(this).Scale(24 * 16), (host?.ClientSize.Height ?? 800) / 2);
            InputText = "";
            UpdateItems();
        }
        dialog.IsOpen = next;
        if (reason is { } known) OpenChanged?.Invoke(this, new OpenChangedEventArgs(next, known));
    }

    /// <summary>对话框自己关了（Esc、点遮罩）：报告它的原因。</summary>
    void OnDialogChanged(OpenChangedEventArgs e)
    {
        if (e.IsOpen || !open) return;
        open = false;
        OpenChanged?.Invoke(this, e);
    }

    void OnTriggerClick(object? sender, EventArgs e) => SetOpen(true, OpenChangeReason.Trigger);

    /// <summary>执行一项：不可用的不执行；关上面板，报告那一项。</summary>
    void Pick(string value)
    {
        var index = listbox.State.IndexOf(value);
        if (index < 0) return;
        var entry = listbox.State.Options[index];
        listbox.Value = null;
        if (entry.Option.IsDisabled) return;
        SetOpen(false, OpenChangeReason.Select);
        ItemInvoked?.Invoke(this, new CommandPaletteItemEventArgs(entry.Item));
    }

    /// <summary>搜索框里：↑↓、Page Up / Down 移动高亮，Enter 执行（Home / End、空格留给输入）。</summary>
    void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode is Keys.Home or Keys.End or Keys.Space) return;
        if (ListboxControl.ToListKey(e.KeyData) is { } key && listbox.State.Handle(key, space: false, picked: Pick)) e.Handled = e.SuppressKeyPress = true;
    }

    /// <summary>按了键：是快捷键时开关面板（测试直接调）。</summary>
    /// <param name="keyData">按键与修饰键。</param>
    /// <param name="win">按着 Win（Keys 里没有它）。</param>
    /// <returns>是快捷键。</returns>
    internal bool OnHotkey(Keys keyData, bool win = false)
    {
        if (Hotkey is not { } combo || !global::Rune.WinForms.Hotkey.Parse(combo).Matches(keyData, win)) return false;
        SetOpen(!open, OpenChangeReason.Imperative);
        return true;
    }

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => Trigger?.GetPreferredSize(proposedSize) ?? Size.Empty;

    /// <inheritdoc />
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        if (Trigger is not { } trigger) return;
        // 触发控件自己变了大小（文字变了）：跟着变，再铺满
        if (levent.AffectedControl == trigger && AutoSize) Size = GetPreferredSize(Size.Empty);
        trigger.Bounds = ClientRectangle;
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        Sync();
    }

    /// <inheritdoc />
    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        Application.AddMessageFilter(filter);
    }

    /// <inheritdoc />
    protected override void OnHandleDestroyed(EventArgs e)
    {
        Application.RemoveMessageFilter(filter);
        base.OnHandleDestroyed(e);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) Application.RemoveMessageFilter(filter);
        base.Dispose(disposing);
    }

    /// <summary>应用里按下的键：焦点在所在窗体或面板里时看是不是快捷键（输入框里也有效）。</summary>
    sealed class HotkeyFilter(CommandPaletteControl owner) : IMessageFilter
    {
        const int WmKeyDown = 0x100, WmSysKeyDown = 0x104, VkLWin = 0x5B, VkRWin = 0x5C;

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg is not (WmKeyDown or WmSysKeyDown) || owner.Hotkey is null) return false;
            var form = Control.FromChildHandle(m.HWnd)?.FindForm();
            if (form is null || (form != owner.FindForm() && form != owner.dialog.Surface.Form)) return false;
            var win = GetKeyState(VkLWin) < 0 || GetKeyState(VkRWin) < 0;
            return owner.OnHotkey((Keys)(int)m.WParam | ModifierKeys, win);
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern short GetKeyState(int virtualKey);
    }

    /// <summary>对话框的正文：搜索行（上下 space.sm、左右 space.md、lg 字号，下边一条 border 分隔线）与列表（按内容，最高见 ScrollMaxHeight）。</summary>
    sealed class PaletteBody : Control
    {
        readonly CommandPaletteControl owner;

        public PaletteBody(CommandPaletteControl owner)
        {
            this.owner = owner;
            SetStyle(ControlStyles.Selectable, false);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Controls.AddRange([owner.input, owner.listbox]);
        }

        float Em => Font.SizeInPoints * DeviceDpi / 72f;

        int Line => Math.Max(1, (int)MathF.Round(ElementContext.Of(this).Scale(Tokens.BorderWidth.Xs)));

        (int Inline, int Block) SearchPadding => ((int)MathF.Round(Tokens.Space.Md * Em), (int)MathF.Round(Tokens.Space.Sm * Em));

        /// <summary>搜索行的高度（含分隔线）。</summary>
        int SearchHeight(int width)
        {
            var (inline, block) = SearchPadding;
            return owner.input.GetPreferredSize(new Size(Math.Max(0, width - 2 * inline), 0)).Height + 2 * block + Line;
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var width = proposedSize.Width > 0 ? proposedSize.Width : Width;
            return new Size(width, SearchHeight(width) + owner.listbox.GetPreferredSize(new Size(width, 0)).Height);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            // 搜索行是 lg 字号（Tokens 的 lg 是 px，换成磅）
            owner.input.Font = new Font(Font.FontFamily, Tokens.FontSize.Lg * 72 / 96, Font.Style);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            var width = ClientSize.Width;
            var (inline, block) = SearchPadding;
            var search = SearchHeight(width);
            owner.input.Bounds = new Rectangle(inline, block, Math.Max(0, width - 2 * inline), search - 2 * block - Line);
            owner.listbox.Bounds = new Rectangle(0, search, width, Math.Max(0, ClientSize.Height - search));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var brush = new SolidBrush(ProviderControl.ThemeOf(this).Colors.Border);
            var search = SearchHeight(ClientSize.Width);
            e.Graphics.FillRectangle(brush, 0, search - Line, ClientSize.Width, Line);
        }
    }
}
