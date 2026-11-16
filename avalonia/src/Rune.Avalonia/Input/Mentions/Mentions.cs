using System.Collections;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Rune.Avalonia;

/// <summary>
/// 提及（vue-tsx-pro 的 Mentions）：多行文本框里输入 @ 弹出人员（或岗位、群组）列表，选中后写成"@名字 "。留言、审批意见、协商回复里通知相关的人。
/// <code>
/// &lt;rune:Field Label="留言"&gt;&lt;rune:Mentions Text="{Binding Comment}" ItemsSource="{Binding People}" Mentioned="{Binding Notified}" /&gt;&lt;/rune:Field&gt;
/// </code>
/// <list type="bullet">
/// <item>值仍是文字；<see cref="Mentioned"/> 是文字里提到的选项的值（工号、岗位编号），随文字增删——删掉"@名字"就不再算提及。显示提及过的文字用 <see cref="MentionText"/>。</item>
/// <item>弹出：@ 在开头或前面不是字母数字（邮箱里的 @ 不算），@ 与光标之间没有空白；列表贴在 @ 的位置，宽 16em。</item>
/// <item>过滤：本地按显示文字包含 @ 后面的文字（或 <see cref="Filter"/>，<see cref="LocalFilter"/> 为 false 时不过滤）；给 <see cref="Load"/> 时按它远程搜索（同 Combobox）。</item>
/// <item>键盘：↑↓ 移动、Enter 或 Tab 选中、Esc 关闭（这个 @ 不再弹出，直到重新输入）；焦点一直在文本框里。</item>
/// </list>
/// 外壳与自动增高同 <see cref="Textarea"/>。部件：弹出层 PART_Popover、列表 PART_Listbox（同 Combobox）。
/// </summary>
public class Mentions : Textarea
{
    /// <summary>可以提及的人：ListboxOption、字符串，或任意数据加 <see cref="ToOption"/>；有 Load 时是第一次结果之前的选项。</summary>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty = AvaloniaProperty.Register<Mentions, IEnumerable?>(nameof(ItemsSource));

    /// <summary>数据 → 选项。</summary>
    public static readonly StyledProperty<Func<object?, ListboxOption>?> ToOptionProperty = AvaloniaProperty.Register<Mentions, Func<object?, ListboxOption>?>(nameof(ToOption));

    /// <summary>远程搜索：按 @ 后面的文字调用，停下 300 毫秒后请求，只采用最后一次（同 Select 的 Load）。</summary>
    public static readonly StyledProperty<OptionsLoader?> LoadProperty = AvaloniaProperty.Register<Mentions, OptionsLoader?>(nameof(Load));

    /// <summary>文字里提到的选项的值（TS 的 _mentions，双向绑定），随文字更新。</summary>
    public static readonly StyledProperty<IReadOnlyList<string>> MentionedProperty =
        AvaloniaProperty.Register<Mentions, IReadOnlyList<string>>(nameof(Mentioned), [], defaultBindingMode: BindingMode.TwoWay);

    /// <summary>触发的字符，默认 @。</summary>
    public static readonly StyledProperty<string> PrefixProperty = AvaloniaProperty.Register<Mentions, string>(nameof(Prefix), "@");

    /// <summary>搜索时怎样算匹配（数据、@ 后面的文字）；不写时显示文字包含它（忽略大小写与重音）。</summary>
    public static readonly StyledProperty<Func<object?, string, bool>?> FilterProperty = AvaloniaProperty.Register<Mentions, Func<object?, string, bool>?>(nameof(Filter));

    /// <summary>在本地按 @ 后面的文字过滤，默认 true；false 时由调用方换 ItemsSource（TS 的 filter={false}）。</summary>
    public static readonly StyledProperty<bool> LocalFilterProperty = AvaloniaProperty.Register<Mentions, bool>(nameof(LocalFilter), true);

    /// <summary>列表开着（双向绑定）。</summary>
    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<Mentions, bool>(nameof(IsOpen), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>正在取选项。</summary>
    public static readonly StyledProperty<bool> IsLoadingProperty = AvaloniaProperty.Register<Mentions, bool>(nameof(IsLoading));

    /// <summary>选项的内容模板（头像、部门）。</summary>
    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty = AvaloniaProperty.Register<Mentions, IDataTemplate?>(nameof(ItemTemplate));

    /// <summary>没有匹配的人时列表里显示的内容；不写时取语言包。</summary>
    public static readonly StyledProperty<object?> EmptyProperty = AvaloniaProperty.Register<Mentions, object?>(nameof(Empty));

    /// <summary><see cref="OpenChanged"/> 的路由事件（直接）。</summary>
    public static readonly RoutedEvent<OpenChangedEventArgs> OpenChangedEvent = RoutedEvent.Register<Mentions, OpenChangedEventArgs>(nameof(OpenChanged), RoutingStrategies.Direct);

    /// <summary>@ 与光标之间最多几个字：再长就不算正在提及。</summary>
    const int MaxQuery = 30;

    readonly ListPopover popover;
    readonly Listbox listbox;
    readonly RemoteOptions remote;
    // 见过的选项（远程搜索换掉 ItemsSource 之后，已提及的人仍能认出来）
    readonly Dictionary<string, ListboxOption> known = [];
    TextPresenter? presenter;
    IReadOnlyList<object?> all = [];
    // 正在提及：@ 在文字里的位置，与 @ 到光标之间的文字（过滤词）
    int? trigger;
    string query = "";
    // 按 Esc 关掉的那个 @：光标还在它后面时不再弹出
    int? dismissed;
    OpenChangeReason? pending;
    bool writing;

    /// <summary>新建：文本框与贴着 @ 弹出的列表。</summary>
    public Mentions()
    {
        remote = new RemoteOptions(item => ListboxOption.From(item, ToOption).Value, OnRemoteChanged);
        popover = new ListPopover(this, LogicalChildren, () =>
        {
            if (IsOpen) SetOpen(false, OpenChangeReason.Outside);
        });
        listbox = popover.Listbox;
        listbox.IsRequired = true;
        listbox.Reached += last => remote.Reached(last);
        listbox.Picked += (_, e) => Pick(e.Value);
    }

    /// <summary>可以提及的人。</summary>
    public IEnumerable? ItemsSource { get => GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    /// <summary>数据 → 选项。</summary>
    public Func<object?, ListboxOption>? ToOption { get => GetValue(ToOptionProperty); set => SetValue(ToOptionProperty, value); }

    /// <summary>远程搜索。</summary>
    public OptionsLoader? Load { get => GetValue(LoadProperty); set => SetValue(LoadProperty, value); }

    /// <summary>文字里提到的选项的值。</summary>
    public IReadOnlyList<string> Mentioned { get => GetValue(MentionedProperty); set => SetValue(MentionedProperty, value); }

    /// <summary>触发的字符。</summary>
    public string Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }

    /// <summary>搜索的匹配。</summary>
    public Func<object?, string, bool>? Filter { get => GetValue(FilterProperty); set => SetValue(FilterProperty, value); }

    /// <summary>在本地过滤。</summary>
    public bool LocalFilter { get => GetValue(LocalFilterProperty); set => SetValue(LocalFilterProperty, value); }

    /// <summary>列表开着。</summary>
    public bool IsOpen { get => GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }

    /// <summary>正在取选项。</summary>
    public bool IsLoading { get => GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }

    /// <summary>选项的内容模板。</summary>
    public IDataTemplate? ItemTemplate { get => GetValue(ItemTemplateProperty); set => SetValue(ItemTemplateProperty, value); }

    /// <summary>没有匹配时的内容。</summary>
    public object? Empty { get => GetValue(EmptyProperty); set => SetValue(EmptyProperty, value); }

    /// <summary>用户开、关了列表（输入 @、选中、Esc、点外面、移走光标）；代码里改 IsOpen 时不发。</summary>
    public event EventHandler<OpenChangedEventArgs>? OpenChanged
    {
        add => AddHandler(OpenChangedEvent, value);
        remove => RemoveHandler(OpenChangedEvent, value);
    }

    /// <summary>弹出的列表（测试用）。</summary>
    internal Listbox List => listbox;

    /// <summary>当前的数据：远程请求到的，没有时是 ItemsSource。</summary>
    IReadOnlyList<object?> Source => remote.ItemsOr(all);

    /// <inheritdoc />
    protected override void OnTemplateParts(INameScope scope)
    {
        base.OnTemplateParts(scope);
        presenter = scope.Find<TextPresenter>("PART_TextPresenter");
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ItemsSourceProperty || change.Property == ToOptionProperty || change.Property == FilterProperty || change.Property == LocalFilterProperty)
        {
            all = ItemsSource?.Cast<object?>().ToList() ?? [];
            UpdateItems();
        }
        else if (change.Property == TextProperty)
        {
            UpdateMentioned();
            if (!writing && IsFocused) Detect();
        }
        else if (change.Property == PrefixProperty) UpdateMentioned();
        else if (change.Property == LoadProperty)
        {
            remote.Load = Load;
            OnRemoteChanged();
        }
        else if (change.Property == IsLoadingProperty) listbox.IsLoading = IsLoading || remote.Loading;
        else if (change.Property == ItemTemplateProperty) listbox.ItemTemplate = ItemTemplate;
        else if (change.Property == EmptyProperty || change.Property == Provider.LangProperty) UpdateEmpty();
        else if (change.Property == IsOpenProperty) Toggle();
        else if (change.Property == FontSizeProperty) popover.UpdateMetrics(FontSize);
    }

    /// <summary>列表里的选项：本地按 @ 后面的文字过滤；远程选项、关掉本地过滤时原样。见过的选项记下来认提及。</summary>
    void UpdateItems()
    {
        foreach (var item in Source)
        {
            var option = ListboxOption.From(item, ToOption);
            known[option.Value] = option;
        }
        listbox.ToOption = ToOption;
        listbox.ItemsSource = Load is not null || !LocalFilter || query.Length == 0
            ? Source
            : [.. Source.Where(item => Filter?.Invoke(item, query) ?? SelectSearch.Matches(ListboxOption.From(item, ToOption).Label, query))];
        UpdateEmpty();
        UpdateMentioned();
    }

    void UpdateEmpty()
    {
        var lang = Provider.GetLang(this);
        listbox.Empty = remote.Failed ? new Empty { Size = TextSize.Sm, Description = lang.ListLoadFailed } : Empty ?? new Empty { Size = TextSize.Sm, Description = lang.MentionsEmpty };
        AutomationProperties.SetName(listbox, lang.MentionsList);
    }

    void OnRemoteChanged()
    {
        listbox.IsLoading = IsLoading || remote.Loading;
        listbox.IsLoadingMore = remote.LoadingMore;
        UpdateItems();
        // 远程结果后到：打开着又没有高亮项时高亮第一项（Enter 才能直接选中）
        if (IsOpen && listbox.State.Active is null) listbox.State.MoveToEnd(last: false);
    }

    /// <summary>文字里提到的人：随文字与见过的选项更新。</summary>
    void UpdateMentioned()
    {
        var next = MentionParsing.MentionedValues(Text ?? "", known.Values, Prefix);
        if (!next.SequenceEqual(Mentioned)) SetCurrentValue(MentionedProperty, next);
    }

    /// <summary>看光标前面是不是正在写的提及：是就按 @ 后面的文字过滤并打开，不是就关闭。</summary>
    internal void Detect()
    {
        if (ReadOnly || !IsEffectivelyEnabled) return;
        var text = Text ?? "";
        var caret = Math.Min(CaretIndex, text.Length);
        var before = text[..caret];
        var at = before.LastIndexOf(Prefix, StringComparison.Ordinal);
        var typed = at < 0 ? "" : before[(at + Prefix.Length)..];
        var mentioning = at >= 0 && SelectionStart == SelectionEnd && (at == 0 || !MentionParsing.IsWordBefore(before[at - 1]))
                         && !typed.Any(char.IsWhiteSpace) && typed.Length <= MaxQuery;
        if (!mentioning || at == dismissed)
        {
            if (!mentioning) dismissed = null;
            trigger = null;
            if (IsOpen) SetOpen(false, OpenChangeReason.Focus);
            return;
        }
        var opening = trigger != at || !IsOpen;
        trigger = at;
        query = typed;
        remote.SetQuery(query);
        UpdateItems();
        if (opening) SetOpen(true, OpenChangeReason.Trigger);
        else ShowPopover();
        listbox.State.MoveToEnd(last: false);
    }

    /// <summary>选中一项：把"@过滤词"换成"@名字 "，光标放在后面。</summary>
    internal void Pick(string choice)
    {
        if (trigger is not { } start || listbox.State.Options.FirstOrDefault(entry => entry.Option.Value == choice)?.Option is not { } option) return;
        known[option.Value] = option;
        var text = Text ?? "";
        var caret = Math.Min(CaretIndex, text.Length);
        var inserted = $"{Prefix}{option.Label} ";
        writing = true;
        SetCurrentValue(TextProperty, text[..start] + inserted + text[caret..]);
        writing = false;
        CaretIndex = start + inserted.Length;
        trigger = null;
        SetOpen(false, OpenChangeReason.Select);
        Focus();
    }

    void SetOpen(bool open, OpenChangeReason reason)
    {
        pending = reason;
        SetCurrentValue(IsOpenProperty, open);
        pending = null;
    }

    void Toggle()
    {
        var reason = pending;
        remote.SetOpen(IsOpen);
        if (IsOpen) ShowPopover();
        else popover.Show(false);
        PseudoClasses.Set(":open", IsOpen);
        if (reason is { } known) RaiseEvent(new OpenChangedEventArgs(OpenChangedEvent, IsOpen, known));
    }

    /// <summary>列表贴在 @ 的位置（光标的矩形换到外壳坐标），宽 16em。</summary>
    void ShowPopover()
    {
        var anchor = new Rect(0, 0, 0, Bounds.Height);
        if (presenter?.TextLayout is { } layout && trigger is { } at && presenter.TranslatePoint(default, this) is { } origin)
        {
            var caret = layout.HitTestTextPosition(at);
            anchor = new Rect(origin.X + caret.X, origin.Y + caret.Y, 1, caret.Height);
        }
        popover.ShowAt(anchor, 16 * FontSize);
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (IsOpen && HandleListKey(e))
        {
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    /// <summary>列表开着时：Esc 关闭（这个 @ 不再弹出）、Tab 选中高亮项、↑↓ 与 Page Up / Down 移动、Enter 选中。</summary>
    bool HandleListKey(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            dismissed = trigger;
            SetOpen(false, OpenChangeReason.Escape);
            return true;
        }
        if (e.Key == Key.Tab && listbox.State.Active is { } active)
        {
            Pick(active);
            return true;
        }
        return Listbox.ToListKey(e.Key) is { } listKey and not (ListKey.Home or ListKey.End or ListKey.Space)
               && listbox.State.Handle(listKey, space: false, picked: Pick);
    }

    /// <summary>移动光标（←→、Home / End）后重新判断；列表关着时任何键之后都判断。</summary>
    protected override void OnKeyUp(KeyEventArgs e)
    {
        base.OnKeyUp(e);
        if (IsOpen && e.Key is Key.Up or Key.Down) return;
        if (e.Key is Key.Left or Key.Right or Key.Home or Key.End || !IsOpen) Detect();
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        Detect();
    }

    /// <inheritdoc />
    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        // 失焦是冒泡的路由事件：里面元素的不算
        if (e.Source != this) return;
        if (IsOpen) SetOpen(false, OpenChangeReason.Focus);
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        popover.UpdateMetrics(FontSize);
        UpdateEmpty();
    }
}

/// <summary>
/// 带提及的文字（vue-tsx-pro 的 MentionText）："@名字"高亮（accentText 字、accentSoft 底、medium），换行照常；行高同正文（<see cref="Text"/>）。
/// <code>
/// &lt;rune:MentionText Value="{Binding Comment}" Options="{Binding People}" /&gt;
/// </code>
/// 上游的提及是带 sm 圆角与左右 0.125em 的 span；桌面上是带底色的行内文字（跟着段落换行）。<see cref="RenderMention"/> 自己画一个提及（链接到个人页）。
/// </summary>
public class MentionText : Text
{
    /// <summary>文字（TS 的 text；Text 是 TextBlock 自己的）。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<MentionText, string?>(nameof(Value));

    /// <summary>能被提及的选项："@显示文字"高亮。</summary>
    public static readonly StyledProperty<IReadOnlyList<ListboxOption>?> OptionsProperty = AvaloniaProperty.Register<MentionText, IReadOnlyList<ListboxOption>?>(nameof(Options));

    /// <summary>触发的字符，默认 @。</summary>
    public static readonly StyledProperty<string> PrefixProperty = Mentions.PrefixProperty.AddOwner<MentionText>();

    /// <summary>自己画一个提及（选项、这一段的文字 → 行内元素）；不写时是高亮的文字。</summary>
    public static readonly StyledProperty<Func<ListboxOption, string, Inline>?> RenderMentionProperty =
        AvaloniaProperty.Register<MentionText, Func<ListboxOption, string, Inline>?>(nameof(RenderMention));

    static MentionText() => TextWrappingProperty.OverrideDefaultValue<MentionText>(TextWrapping.Wrap);

    /// <summary>文字。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>能被提及的选项。</summary>
    public IReadOnlyList<ListboxOption>? Options { get => GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }

    /// <summary>触发的字符。</summary>
    public string Prefix { get => GetValue(PrefixProperty); set => SetValue(PrefixProperty, value); }

    /// <summary>自己画一个提及。</summary>
    public Func<ListboxOption, string, Inline>? RenderMention { get => GetValue(RenderMentionProperty); set => SetValue(RenderMentionProperty, value); }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty || change.Property == OptionsProperty || change.Property == PrefixProperty || change.Property == RenderMentionProperty) Render();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Render();
    }

    void Render()
    {
        var inlines = new InlineCollection();
        foreach (var segment in MentionParsing.Segments(Value ?? "", Options ?? [], Prefix))
        {
            if (segment.Mention is not { } mention)
            {
                inlines.Add(new Run(segment.Text));
                continue;
            }
            if (RenderMention is { } render)
            {
                inlines.Add(render(mention, segment.Text));
                continue;
            }
            var run = new Run(segment.Text) { FontWeight = FontWeight.Medium };
            run.Bind(TextElement.ForegroundProperty, this.GetResourceObservable(ThemeKeys.Brush.AccentText));
            run.Bind(TextElement.BackgroundProperty, this.GetResourceObservable(ThemeKeys.Brush.AccentSoft));
            inlines.Add(run);
        }
        Inlines = inlines;
    }
}
