using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Metadata;
using Avalonia.Platform.Storage;

namespace Rune.Avalonia;

/// <summary>选到的一个文件：文件本身与相对路径（选文件夹时是"文件夹/子文件夹/文件名"，否则是文件名；上游的 webkitRelativePath）。</summary>
/// <param name="File">文件（读内容用 OpenReadAsync，本地路径是 Path.LocalPath）。</param>
/// <param name="RelativePath">相对路径。</param>
public sealed record PickedFile(IStorageFile File, string RelativePath)
{
    /// <summary>文件名。</summary>
    public string Name => File.Name;
}

/// <summary>选到文件、拖进来不合要求的文件（<see cref="FileDrop.FilesPicked"/>、<see cref="FileDrop.Rejected"/>）。</summary>
public sealed class FileDropEventArgs(RoutedEvent routedEvent, IReadOnlyList<PickedFile> files) : RoutedEventArgs(routedEvent)
{
    /// <summary>文件。</summary>
    public IReadOnlyList<PickedFile> Files { get; } = files;
}

/// <summary>
/// 选择文件的区域（vue-tsx-pro 的 FileDrop）：点击（或聚焦后按 Enter / 空格）打开系统的文件对话框，也可以把文件拖进来。
/// <code>
/// &lt;rune:FileDrop Accept="image/*" Multiple="True" FilesPicked="OnFiles" Description="最大 5 MB" /&gt;
/// </code>
/// 只负责选文件：选到的文件经 <see cref="FilesPicked"/> 交出去，不在 <see cref="Accept"/> 里的经 <see cref="Rejected"/> 交出去
/// （对话框按扩展名筛选，拖放不受限制，这里补上；不是多选时只取第一个）。<see cref="Directory"/> 时选整个文件夹，拖进来的文件夹递归读出里面的文件。
/// 外观：2px 虚线 borderStrong 边框（悬停、拖着文件经过时 accent，经过时 accentSoft 底）、md 圆角、surface 底、四周 lg、至少 8em 高，
/// 里面居中竖排上传图标、提示文字（语言包 FileDrop）与说明；出错 danger 边框，只读、不可用变淡、不能选。部件：<see cref="FileDropParts"/>。
/// </summary>
[PseudoClasses(":dragging", ":invalid", ":locked")]
public class FileDrop : Control, IFieldControl
{
    /// <summary>接受的文件类型：<c>image/*</c>、<c>.pdf</c>、<c>application/json</c> 这样的逗号分隔列表；不写时都接受。</summary>
    public static readonly StyledProperty<string?> AcceptProperty = AvaloniaProperty.Register<FileDrop, string?>(nameof(Accept));

    /// <summary>可以选多个。</summary>
    public static readonly StyledProperty<bool> MultipleProperty = AvaloniaProperty.Register<FileDrop, bool>(nameof(Multiple));

    /// <summary>选文件夹：对话框里选整个文件夹，拖进来的文件夹递归读出里面的文件。</summary>
    public static readonly StyledProperty<bool> DirectoryProperty = AvaloniaProperty.Register<FileDrop, bool>(nameof(Directory));

    /// <summary>图标，默认 Upload（上游的 propsIcon.icon）。</summary>
    public static readonly StyledProperty<IconData?> IconProperty = AvaloniaProperty.Register<FileDrop, IconData?>(nameof(Icon));

    /// <summary>提示文字下面的说明（大小、类型限制）。</summary>
    public static readonly StyledProperty<string?> DescriptionProperty = AvaloniaProperty.Register<FileDrop, string?>(nameof(Description));

    /// <summary>区域里的内容；不写时是图标、提示文字与说明。</summary>
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<FileDrop, object?>(nameof(Content));

    /// <summary>校验未通过；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsInvalidProperty = AvaloniaProperty.Register<FileDrop, bool?>(nameof(IsInvalid));

    /// <summary>必填；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsRequiredProperty = AvaloniaProperty.Register<FileDrop, bool?>(nameof(IsRequired));

    /// <summary>只读：不能选文件（同不可用）；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsReadOnlyProperty = AvaloniaProperty.Register<FileDrop, bool?>(nameof(IsReadOnly));

    /// <summary>底色（主题样式按状态写）。</summary>
    public static readonly StyledProperty<IBrush?> BackgroundProperty = Border.BackgroundProperty.AddOwner<FileDrop>();

    /// <summary>虚线边框的颜色（主题样式按状态写）。</summary>
    public static readonly StyledProperty<IBrush?> BorderBrushProperty = Border.BorderBrushProperty.AddOwner<FileDrop>();

    // 键盘聚焦时的焦点环颜色：主题资源 focusRing（换主题时跟着）
    static readonly StyledProperty<IBrush?> FocusRingProperty = AvaloniaProperty.Register<FileDrop, IBrush?>("FocusRing");

    /// <summary>选到了文件（已按 Accept 过滤）。</summary>
    public static readonly RoutedEvent<FileDropEventArgs> FilesPickedEvent = RoutedEvent.Register<FileDrop, FileDropEventArgs>(nameof(FilesPicked), RoutingStrategies.Bubble);

    /// <summary>拖进来的文件里不在 Accept 里的。</summary>
    public static readonly RoutedEvent<FileDropEventArgs> RejectedEvent = RoutedEvent.Register<FileDrop, FileDropEventArgs>(nameof(Rejected), RoutingStrategies.Bubble);

    readonly ContentPresenter presenter = new() { HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center };
    readonly Flex body = new() { Direction = FlexDirection.Column, Align = FlexAlign.Center, Gap = Space.Xs };
    readonly Icon icon = new() { Name = FileDropParts.Icon };
    readonly Text hint = new() { Name = FileDropParts.Hint, TextAlignment = TextAlignment.Center };
    readonly Text description = new() { Name = FileDropParts.Description, TextAlignment = TextAlignment.Center, IsVisible = false };

    static FileDrop()
    {
        FocusableProperty.OverrideDefaultValue<FileDrop>(true);
        AffectsRender<FileDrop>(BackgroundProperty, BorderBrushProperty, FocusRingProperty, IsFocusedProperty);
        DragDrop.AllowDropProperty.OverrideDefaultValue<FileDrop>(true);
    }

    /// <summary>新建：默认内容是图标、提示文字与说明；接上拖放。</summary>
    public FileDrop()
    {
        new TemplateResources(this).Bind(FocusRingProperty, ThemeKeys.Brush.FocusRing);
        body.Children.AddRange([icon, hint, description]);
        presenter.Content = body;
        LogicalChildren.Add(presenter);
        VisualChildren.Add(presenter);
        AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DragLeaveEvent, (_, _) => PseudoClasses.Set(":dragging", false));
        AddHandler(DragDrop.DropEvent, OnDrop);
        Sync();
    }

    /// <summary>接受的文件类型。</summary>
    public string? Accept { get => GetValue(AcceptProperty); set => SetValue(AcceptProperty, value); }

    /// <summary>可以选多个。</summary>
    public bool Multiple { get => GetValue(MultipleProperty); set => SetValue(MultipleProperty, value); }

    /// <summary>选文件夹。</summary>
    public bool Directory { get => GetValue(DirectoryProperty); set => SetValue(DirectoryProperty, value); }

    /// <summary>图标。</summary>
    public IconData? Icon { get => GetValue(IconProperty); set => SetValue(IconProperty, value); }

    /// <summary>说明。</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>区域里的内容。</summary>
    [Content]
    public object? Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }

    /// <summary>校验未通过。</summary>
    public bool? IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>必填。</summary>
    public bool? IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>只读。</summary>
    public bool? IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>底色。</summary>
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty, value); }

    /// <summary>边框颜色。</summary>
    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty, value); }

    /// <summary>选到了文件。</summary>
    public event EventHandler<FileDropEventArgs>? FilesPicked
    {
        add => AddHandler(FilesPickedEvent, value);
        remove => RemoveHandler(FilesPickedEvent, value);
    }

    /// <summary>拖进来的文件里不合要求的。</summary>
    public event EventHandler<FileDropEventArgs>? Rejected
    {
        add => AddHandler(RejectedEvent, value);
        remove => RemoveHandler(RejectedEvent, value);
    }

    /// <summary>不能选（不可用或只读）。</summary>
    bool Locked => !IsEffectivelyEnabled || (IsReadOnly ?? Field.ReadOnlyOf(this));

    double Em => global::Avalonia.Controls.Documents.TextElement.GetFontSize(this);

    void IFieldControl.FieldChanged() => Sync();

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ContentProperty || change.Property == IconProperty || change.Property == DescriptionProperty || change.Property == IsInvalidProperty || change.Property == IsReadOnlyProperty
            || change.Property == IsRequiredProperty || change.Property == IsEnabledProperty || change.Property == Provider.LangProperty
            || change.Property == Field.OwnerProperty || change.Property == Field.IsReadOnlyProperty
            || change.Property == global::Avalonia.Controls.Documents.TextElement.FontSizeProperty)
            Sync();
    }

    /// <summary>内容、文字、状态伪类、读屏名称；四周 lg、至少 8em 高按字号算。</summary>
    void Sync()
    {
        var lang = Provider.GetLang(this);
        icon.Data = Icon ?? Lucide.Upload;
        // 图标按字号画：1.5em
        icon.FontSize = 1.5 * Em;
        hint.Text = lang.FileDrop;
        description.Text = Description;
        description.IsVisible = !string.IsNullOrEmpty(Description);
        presenter.Content = Content ?? body;
        presenter.Padding = new Thickness(this.Resource<double>(ThemeKeys.Space.Lg) * Em);
        MinHeight = 8 * Em;
        var field = Field.Of(this) is { } owner && owner.Owns(this) ? owner : null;
        PseudoClasses.Set(":invalid", IsInvalid ?? field?.Invalid ?? false);
        PseudoClasses.Set(":locked", Locked);
        AutomationProperties.SetHelpText(this, Field.DescribedBy(this, Description));
        InvalidateVisual();
    }

    void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (Locked || !e.DataTransfer.Contains(DataFormat.File)) return;
        PseudoClasses.Set(":dragging", true);
    }

    void OnDragOver(object? sender, DragEventArgs e) =>
        e.DragEffects = !Locked && e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Copy : DragDropEffects.None;

    async void OnDrop(object? sender, DragEventArgs e)
    {
        PseudoClasses.Set(":dragging", false);
        if (Locked || e.DataTransfer.TryGetFiles() is not { } items) return;
        e.Handled = true;
        // 拖进来的条目要在 drop 里取出（之后数据就没了）；文件夹递归读出文件只在选文件夹时
        Deliver(await Collect(items, Directory));
    }

    /// <summary>条目里的文件：文件夹递归往下（选文件夹时），文件带上相对路径。</summary>
    /// <param name="items">拖进来或对话框选到的条目。</param>
    /// <param name="folders">把文件夹展开成里面的文件。</param>
    internal static async Task<List<PickedFile>> Collect(IEnumerable<IStorageItem> items, bool folders)
    {
        var files = new List<PickedFile>();
        async Task Visit(IStorageItem item, string prefix)
        {
            if (item is IStorageFile file) files.Add(new PickedFile(file, prefix + file.Name));
            else if (folders && item is IStorageFolder folder)
                await foreach (var child in folder.GetItemsAsync()) await Visit(child, $"{prefix}{folder.Name}/");
        }
        foreach (var item in items) await Visit(item, "");
        return files;
    }

    /// <summary>按 Accept 分成接受与拒绝两份；不是多选（也不是选文件夹）时只取第一个。</summary>
    internal void Deliver(IReadOnlyList<PickedFile> files)
    {
        var accepted = files.Where(file => FileAccept.Matches(file.Name, Accept)).ToList();
        var rejected = files.Where(file => !accepted.Contains(file)).ToList();
        var taken = Multiple || Directory ? accepted : accepted.Take(1).ToList();
        if (taken.Count > 0) RaiseEvent(new FileDropEventArgs(FilesPickedEvent, taken));
        if (rejected.Count > 0) RaiseEvent(new FileDropEventArgs(RejectedEvent, rejected));
    }

    /// <summary>打开系统的文件对话框（选文件夹时是文件夹对话框）。</summary>
    public async Task OpenAsync()
    {
        if (Locked || TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
        var lang = Provider.GetLang(this);
        if (Directory)
        {
            var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = Multiple, Title = lang.FileDrop });
            Deliver(await Collect(folders, folders: true));
            return;
        }
        var extensions = FileAccept.Extensions(Accept);
        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            AllowMultiple = Multiple,
            Title = lang.FileDrop,
            FileTypeFilter = extensions.Count > 0 ? [new FilePickerFileType(Accept) { Patterns = [.. extensions.Select(extension => "*" + extension)] }] : null,
        });
        Deliver(await Collect(picked, folders: false));
    }

    /// <inheritdoc />
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton != MouseButton.Left || e.Handled) return;
        e.Handled = true;
        _ = OpenAsync();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Key is not (Key.Enter or Key.Space)) return;
        e.Handled = true;
        _ = OpenAsync();
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        presenter.Measure(availableSize);
        return new Size(double.IsInfinity(availableSize.Width) ? presenter.DesiredSize.Width : availableSize.Width, presenter.DesiredSize.Height);
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        presenter.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>底色、2px 虚线边框（md 圆角）；键盘聚焦时外面一圈 2px 焦点环。</summary>
    public override void Render(DrawingContext context)
    {
        var radius = this.Resource<double>(ThemeKeys.Radius.Md) * Em;
        var outline = new RoundedRect(new Rect(Bounds.Size).Deflate(1), Math.Max(0, radius - 1));
        if (Background is { } background) context.DrawRectangle(background, null, new RoundedRect(new Rect(Bounds.Size), radius));
        if (BorderBrush is { } border) context.DrawRectangle(null, new Pen(border, 2, new DashStyle([3, 2], 0)), outline);
        if (Classes.Contains(":focus-visible") && GetValue(FocusRingProperty) is { } ring)
            context.DrawRectangle(null, new Pen(ring, 2), new RoundedRect(new Rect(Bounds.Size).Inflate(1), radius + 1));
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new DropPeer(this);

    /// <summary>读屏：按钮（同原生的文件输入框），名称是自己写的、所在字段的标签，否则是提示文字；按下打开对话框。</summary>
    sealed class DropPeer(FileDrop owner) : ControlAutomationPeer(owner), IInvokeProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

        protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name
            : Field.Of(owner) is { } field && field.Owns(owner) ? field.LabelText : Provider.GetLang(owner).FileDrop;

        protected override bool IsEnabledCore() => !owner.Locked && base.IsEnabledCore();

        public void Invoke() => _ = owner.OpenAsync();
    }
}

/// <summary>FileDrop 的部件名（TS 的部件）。外层（TS 的 root，原生 input 在里面）是区域自己。</summary>
public static class FileDropParts
{
    /// <summary>上传图标（TS 的 propsIcon）：Icon，1.5em、textMuted。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>提示文字（TS 的 propsHint）：Text，medium。</summary>
    public const string Hint = "PART_Hint";

    /// <summary>说明（TS 的 propsDescription）：Text，sm、textMuted。</summary>
    public const string Description = "PART_Description";
}
