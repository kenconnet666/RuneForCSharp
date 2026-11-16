using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace Rune.Avalonia;

/// <summary>列表里一个文件的状态。</summary>
public enum UploadStatus
{
    /// <summary>选了、还没上传。</summary>
    Selected,

    /// <summary>上传中。</summary>
    Uploading,

    /// <summary>完成（已有文件也是）。</summary>
    Done,

    /// <summary>失败或没通过检查。</summary>
    Error,
}

/// <summary>Upload 列表里的一个文件（vue-tsx-pro 的 UploadFile）。</summary>
/// <param name="Id">组件给的 id，同一个列表里唯一。</param>
/// <param name="Name">文件名（选文件夹时是相对路径）。</param>
/// <param name="Status">状态。</param>
public sealed record UploadFile(string Id, string Name, UploadStatus Status)
{
    static int nextId;

    /// <summary>大小（字节）；已有文件不知道时为 null。</summary>
    public long? Size { get; init; }

    /// <summary>选的文件；已有文件没有。</summary>
    public IStorageFile? File { get; init; }

    /// <summary>已有文件的地址：文件名链到它。</summary>
    public Uri? Url { get; init; }

    /// <summary>上传进度 0–1；上传函数没报告进度时为 null（不确定进度）。</summary>
    public double? Progress { get; init; }

    /// <summary>错误信息。</summary>
    public string? Error { get; init; }

    /// <summary>没通过大小或类型检查：不会上传，也不能重试。</summary>
    public bool Rejected { get; init; }

    /// <summary>上传函数的返回值（如文件地址）。</summary>
    public object? Response { get; init; }

    internal static string NextId() => $"upload-{Interlocked.Increment(ref nextId)}";

    /// <summary>已有文件（之前上传过、从后端读回来的，vue-tsx-pro 的 existingUploadFile）：状态是完成，没有 File。</summary>
    public static UploadFile Existing(string name, long? size = null, Uri? url = null, string? id = null) =>
        new(id ?? NextId(), name, UploadStatus.Done) { Size = size, Url = url, Response = url };
}

/// <summary>上传一个文件：用 <paramref name="progress"/> 报告进度（0–1），响应取消；成功时返回结果（记在 Response 上），失败时抛异常（消息显示在列表里）。</summary>
/// <param name="file">文件。</param>
/// <param name="progress">进度。</param>
/// <param name="cancellation">移除、重试、离开界面时取消。</param>
public delegate Task<object?> UploadHandler(IStorageFile file, IProgress<double> progress, CancellationToken cancellation);

/// <summary>列表的样子。</summary>
public enum UploadListType
{
    /// <summary>一行一个文件：图标、文件名、大小或进度（默认）。</summary>
    Text,

    /// <summary>图片墙：方形缩略图，点开看大图。</summary>
    Picture,
}

/// <summary>
/// 上传（vue-tsx-pro 的 Upload）：<see cref="FileDrop"/> 选文件，下面列出文件、上传进度、失败重试与移除。只负责界面与状态，怎样上传由 <see cref="Uploader"/> 决定。
/// <code>
/// &lt;rune:Upload Files="{Binding Attachments}" Multiple="True" Accept="image/*" MaxSize="5000000" Uploader="{Binding UploadAsync}" /&gt;
/// </code>
/// <list type="bullet">
/// <item><see cref="Files"/> 是 UploadFile 的列表（双向绑定）。给了 Uploader 时选到文件就开始上传，否则停在 Selected（等提交时一起上传）；移除正在上传的文件会取消。
/// 上游的 action（经应用配置的请求客户端 POST）桌面上没有对应的客户端，用 Uploader 自己发。</item>
/// <item>超过 <see cref="MaxSize"/>、不在 <see cref="Accept"/> 里的文件也列出来，带错误、不上传；单选（不写 Multiple）时新文件替换旧的；已有文件用 <see cref="UploadFile.Existing"/> 放进来。</item>
/// <item>图片墙（<see cref="ListType"/> = Picture）：方形缩略图（本地文件直接显示），点开看大图（整面墙是一个 ImageGroup）；上传中盖一层进度，失败标红；选文件的区域是最后一格。
/// <see cref="Sortable"/> 时拖动把手（或聚焦后用键盘）调整顺序；<see cref="Pasteable"/> 时焦点在组件里按 Ctrl + V 把剪贴板里的图片、文件加进来（截图存成临时的 PNG）。</item>
/// </list>
/// 部件：<see cref="UploadParts"/>。
/// </summary>
public class Upload : Control, IFieldControl
{
    /// <summary>文件列表（TS 的 _value，双向绑定）。</summary>
    public static readonly StyledProperty<IReadOnlyList<UploadFile>> FilesProperty =
        AvaloniaProperty.Register<Upload, IReadOnlyList<UploadFile>>(nameof(Files), [], defaultBindingMode: BindingMode.TwoWay);

    /// <summary>怎样上传一个文件（TS 的 upload）；不写时只选文件、不上传。</summary>
    public static readonly StyledProperty<UploadHandler?> UploaderProperty = AvaloniaProperty.Register<Upload, UploadHandler?>(nameof(Uploader));

    /// <summary>接受的文件类型（同 FileDrop）。</summary>
    public static readonly StyledProperty<string?> AcceptProperty = FileDrop.AcceptProperty.AddOwner<Upload>();

    /// <summary>可以选多个；不写时新文件替换旧的。</summary>
    public static readonly StyledProperty<bool> MultipleProperty = FileDrop.MultipleProperty.AddOwner<Upload>();

    /// <summary>选文件夹：里面的文件都加进来，列表里显示相对路径；隐含 Multiple。</summary>
    public static readonly StyledProperty<bool> DirectoryProperty = FileDrop.DirectoryProperty.AddOwner<Upload>();

    /// <summary>单个文件的大小上限（字节）。</summary>
    public static readonly StyledProperty<long?> MaxSizeProperty = AvaloniaProperty.Register<Upload, long?>(nameof(MaxSize));

    /// <summary>最多几个文件，超出的不加进来。</summary>
    public static readonly StyledProperty<int?> MaxFilesProperty = AvaloniaProperty.Register<Upload, int?>(nameof(MaxFiles));

    /// <summary>选到的文件加进列表之前先过一遍（裁剪、压缩、改名）；返回 null 不加这个文件。类型不对的文件不经过它。</summary>
    public static readonly StyledProperty<Func<IStorageFile, Task<IStorageFile?>>?> TransformProperty =
        AvaloniaProperty.Register<Upload, Func<IStorageFile, Task<IStorageFile?>>?>(nameof(Transform));

    /// <summary>选择区域里提示文字下面的说明（大小、类型限制）。</summary>
    public static readonly StyledProperty<string?> DescriptionProperty = FileDrop.DescriptionProperty.AddOwner<Upload>();

    /// <summary>只读：只列出文件，不能再选、不能移除或重试；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsReadOnlyProperty = FileDrop.IsReadOnlyProperty.AddOwner<Upload>();

    /// <summary>校验未通过；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsInvalidProperty = FileDrop.IsInvalidProperty.AddOwner<Upload>();

    /// <summary>必填；不写时跟着所在的 Field。</summary>
    public static readonly StyledProperty<bool?> IsRequiredProperty = FileDrop.IsRequiredProperty.AddOwner<Upload>();

    /// <summary>列表的样子，默认 Text。</summary>
    public static readonly StyledProperty<UploadListType> ListTypeProperty = AvaloniaProperty.Register<Upload, UploadListType>(nameof(ListType));

    /// <summary>图片墙里缩略图的边长（像素），默认 104。</summary>
    public static readonly StyledProperty<double> ThumbnailSizeProperty = AvaloniaProperty.Register<Upload, double>(nameof(ThumbnailSize), 104);

    /// <summary>拖动把手（或聚焦后用键盘）调整文件的顺序（图片墙）。</summary>
    public static readonly StyledProperty<bool> SortableProperty = AvaloniaProperty.Register<Upload, bool>(nameof(Sortable));

    /// <summary>焦点在组件里时 Ctrl + V 把剪贴板里的图片、文件加进来。</summary>
    public static readonly StyledProperty<bool> PasteableProperty = AvaloniaProperty.Register<Upload, bool>(nameof(Pasteable));

    readonly Flex root = new() { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch };
    readonly FileDrop drop = new() { Name = UploadParts.Drop };
    readonly Flex list = new() { Name = UploadParts.List, Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch };
    readonly ImageGroup wall = new() { Name = UploadParts.Wall };
    readonly Text hint = new() { Name = UploadParts.Hint, Size = TextSize.Sm, Tone = TextTone.Muted };
    readonly LiveRegion announcer = new();
    // 每个正在上传的文件一个：移除、重试、离开界面时取消
    readonly Dictionary<string, CancellationTokenSource> uploads = [];
    readonly SortableModel<UploadFile> sorting;
    // 这一次拖放、选择里已经有合格的文件（FileDrop 先报告选到的，同一次里紧跟着报告拒绝的）
    bool acceptedInBatch;

    /// <summary>新建：选择区域与列表（或图片墙）。</summary>
    public Upload()
    {
        sorting = new SortableModel<UploadFile>(() => Files, item => item.Id, () => Provider.GetLang(this))
        {
            GetLabel = item => item.Name,
            Reordered = (next, _) => SetCurrentValue(FilesProperty, next),
        };
        sorting.Changed += () =>
        {
            announcer.Announce(sorting.Announcement);
            Render();
        };
        drop.FilesPicked += (_, e) =>
        {
            e.Handled = true;
            acceptedInBatch = true;
            Dispatcher.UIThread.Post(() => acceptedInBatch = false);
            _ = Receive([.. e.Files]);
        };
        drop.Rejected += (_, e) =>
        {
            e.Handled = true;
            // 只收一个文件、这一批里已经收下了合格的：类型不对的不再顶掉它
            if (!Many && acceptedInBatch) return;
            _ = Add([.. e.Files], rejectedType: true);
        };
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        LogicalChildren.Add(announcer);
        VisualChildren.Add(announcer);
        Focusable = false;
    }

    /// <summary>文件列表。</summary>
    public IReadOnlyList<UploadFile> Files { get => GetValue(FilesProperty); set => SetValue(FilesProperty, value); }

    /// <summary>怎样上传一个文件。</summary>
    public UploadHandler? Uploader { get => GetValue(UploaderProperty); set => SetValue(UploaderProperty, value); }

    /// <summary>接受的文件类型。</summary>
    public string? Accept { get => GetValue(AcceptProperty); set => SetValue(AcceptProperty, value); }

    /// <summary>可以选多个。</summary>
    public bool Multiple { get => GetValue(MultipleProperty); set => SetValue(MultipleProperty, value); }

    /// <summary>选文件夹。</summary>
    public bool Directory { get => GetValue(DirectoryProperty); set => SetValue(DirectoryProperty, value); }

    /// <summary>单个文件的大小上限。</summary>
    public long? MaxSize { get => GetValue(MaxSizeProperty); set => SetValue(MaxSizeProperty, value); }

    /// <summary>最多几个文件。</summary>
    public int? MaxFiles { get => GetValue(MaxFilesProperty); set => SetValue(MaxFilesProperty, value); }

    /// <summary>加进列表之前先过一遍。</summary>
    public Func<IStorageFile, Task<IStorageFile?>>? Transform { get => GetValue(TransformProperty); set => SetValue(TransformProperty, value); }

    /// <summary>选择区域的说明。</summary>
    public string? Description { get => GetValue(DescriptionProperty); set => SetValue(DescriptionProperty, value); }

    /// <summary>只读。</summary>
    public bool? IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    /// <summary>校验未通过。</summary>
    public bool? IsInvalid { get => GetValue(IsInvalidProperty); set => SetValue(IsInvalidProperty, value); }

    /// <summary>必填。</summary>
    public bool? IsRequired { get => GetValue(IsRequiredProperty); set => SetValue(IsRequiredProperty, value); }

    /// <summary>列表的样子。</summary>
    public UploadListType ListType { get => GetValue(ListTypeProperty); set => SetValue(ListTypeProperty, value); }

    /// <summary>缩略图的边长。</summary>
    public double ThumbnailSize { get => GetValue(ThumbnailSizeProperty); set => SetValue(ThumbnailSizeProperty, value); }

    /// <summary>可以调整顺序。</summary>
    public bool Sortable { get => GetValue(SortableProperty); set => SetValue(SortableProperty, value); }

    /// <summary>可以粘贴。</summary>
    public bool Pasteable { get => GetValue(PasteableProperty); set => SetValue(PasteableProperty, value); }

    /// <summary>选择区域（测试用）。</summary>
    internal FileDrop DropPart => drop;

    bool Many => Multiple || Directory;

    bool ReadOnly => IsReadOnly ?? Field.ReadOnlyOf(this);

    bool Invalid => IsInvalid ?? Field.Of(this)?.Invalid ?? false;

    bool Required => IsRequired ?? Field.Of(this)?.Required ?? false;

    void IFieldControl.FieldChanged() => Render();

    void Patch(string id, Func<UploadFile, UploadFile> change) =>
        SetCurrentValue(FilesProperty, Files.Select(item => item.Id == id ? change(item) : item).ToList());

    /// <summary>开始（或重新）上传一个文件：进度、完成后记下结果，失败时记下原因（有文案的错误码之外取异常的消息，再没有是"上传失败"）。</summary>
    internal async Task Start(UploadFile item)
    {
        if (Uploader is not { } send || item.File is not { } file) return;
        if (uploads.Remove(item.Id, out var previous)) previous.Cancel();
        var cancel = new CancellationTokenSource();
        uploads[item.Id] = cancel;
        Patch(item.Id, current => current with { Status = UploadStatus.Uploading, Progress = null, Error = null });
        var progress = new Progress<double>(fraction =>
        {
            if (!cancel.IsCancellationRequested) Patch(item.Id, current => current with { Progress = fraction });
        });
        try
        {
            var response = await send(file, progress, cancel.Token);
            if (!cancel.IsCancellationRequested) Patch(item.Id, current => current with { Status = UploadStatus.Done, Progress = 1, Response = response });
        }
        catch (Exception error)
        {
            if (cancel.IsCancellationRequested) return;
            Trace.TraceWarning($"上传失败：{error}");
            var message = string.IsNullOrEmpty(error.Message) ? Provider.GetLang(this).UploadFailed : error.Message;
            Patch(item.Id, current => current with { Status = UploadStatus.Error, Error = message });
        }
        finally
        {
            if (uploads.TryGetValue(item.Id, out var current) && current == cancel) uploads.Remove(item.Id);
        }
    }

    /// <summary>开始上传还停在 Selected、带文件、没在上传的。</summary>
    void StartPending()
    {
        if (Uploader is null) return;
        foreach (var item in Files.Where(item => item.Status == UploadStatus.Selected && item.File is not null).ToList())
            if (!uploads.ContainsKey(item.Id)) _ = Start(item);
    }

    /// <summary>移除一个文件：正在上传的取消。</summary>
    internal void Remove(string id)
    {
        if (uploads.Remove(id, out var cancel)) cancel.Cancel();
        SetCurrentValue(FilesProperty, Files.Where(item => item.Id != id).ToList());
    }

    /// <summary>加进列表：类型不对、超过大小的带错误；单选时替换旧的；最多 MaxFiles 个。</summary>
    async Task Add(IReadOnlyList<PickedFile> files, bool rejectedType = false)
    {
        var lang = Provider.GetLang(this);
        var entries = new List<UploadFile>();
        foreach (var picked in files)
        {
            var size = await SizeOf(picked.File);
            var entry = new UploadFile(UploadFile.NextId(), picked.RelativePath, UploadStatus.Selected) { Size = size, File = picked.File };
            if (rejectedType) entry = entry with { Status = UploadStatus.Error, Error = lang.FileTypeRejected, Rejected = true };
            else if (MaxSize is { } max && size > max) entry = entry with { Status = UploadStatus.Error, Error = lang.FileTooLarge(FormatSize(max)), Rejected = true };
            entries.Add(entry);
        }
        if (!Many)
            foreach (var cancel in uploads.Values) cancel.Cancel();
        var kept = Many ? Files : [];
        var room = MaxFiles is { } maxFiles ? Math.Max(0, maxFiles - kept.Count) : entries.Count;
        var added = (Many ? entries : entries.TakeLast(1)).Take(room).ToList();
        // 合格的文件由 Files 变了时统一开始上传（同父级放进来的待上传文件），这里不再调一次
        SetCurrentValue(FilesProperty, (IReadOnlyList<UploadFile>)[.. kept, .. added]);
    }

    static async Task<long?> SizeOf(IStorageFile file)
    {
        try
        {
            return (await file.GetBasicPropertiesAsync()).Size is { } size ? (long)size : null;
        }
        catch (Exception)
        {
            // 拿不到属性的文件（测试里的假文件、没有权限）：不知道大小
            return null;
        }
    }

    string FormatSize(long bytes) => TableColumns.FileSize(bytes, Provider.GetLang(this).Code, "USD");

    /// <summary>选到的（类型对的）文件：先经过 Transform，再加进列表。</summary>
    async Task Receive(IReadOnlyList<PickedFile> files)
    {
        if (Transform is not { } transform)
        {
            await Add(files);
            return;
        }
        var results = new List<PickedFile>();
        foreach (var picked in files)
            if (await transform(picked.File) is { } next) results.Add(new PickedFile(next, next.Name));
        if (results.Count > 0) await Add(results);
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == FilesProperty || change.Property == UploaderProperty)
        {
            // 选到的、父级放进来的待上传文件：给了 Uploader 时开始上传。排到改完之后（同上游的 watch）：Start 自己也改 Files，在这里直接调会重入
            if (Uploader is not null) Dispatcher.UIThread.Post(StartPending);
            Render();
        }
        else if (change.Property == AcceptProperty || change.Property == MultipleProperty || change.Property == DirectoryProperty || change.Property == DescriptionProperty
                 || change.Property == IsReadOnlyProperty || change.Property == IsInvalidProperty || change.Property == IsRequiredProperty || change.Property == IsEnabledProperty
                 || change.Property == ListTypeProperty || change.Property == ThumbnailSizeProperty || change.Property == SortableProperty || change.Property == PasteableProperty
                 || change.Property == MaxFilesProperty || change.Property == Field.OwnerProperty || change.Property == Provider.LangProperty)
            Render();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Render();
    }

    /// <inheritdoc />
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        foreach (var cancel in uploads.Values) cancel.Cancel();
        uploads.Clear();
    }

    /// <summary>按列表、状态重建：列表模式选择区域在上、列表在下；图片墙模式选择区域是最后一格。</summary>
    void Render()
    {
        var lang = Provider.GetLang(this);
        var picture = ListType == UploadListType.Picture;
        var editable = !ReadOnly;
        var full = MaxFiles is { } max && Many && Files.Count >= max;
        drop.Accept = Accept;
        drop.Multiple = Multiple;
        drop.Directory = Directory;
        drop.IsEnabled = IsEffectivelyEnabled;
        drop.IsInvalid = Invalid;
        // 选好的文件不在选择框里（它选完就清空）：已有文件时不再必填
        drop.IsRequired = Required && Files.Count == 0;
        drop.IsVisible = editable && !(picture && full);
        root.Children.Clear();
        if (picture)
        {
            drop.Description = null;
            drop.Content = new Flex
            {
                Direction = FlexDirection.Column, Align = FlexAlign.Center, Gap = Space.Xs,
                Children = { new Icon { Data = Lucide.ImagePlus }, new TextBlock { Text = lang.UploadAddImage, TextAlignment = TextAlignment.Center, TextWrapping = TextWrapping.Wrap } },
            };
            drop.Width = drop.Height = ThumbnailSize;
            // "添加图片"那一格：四周 xs、sm 字号，同缩略图一样大
            drop.Compact = true;
            drop.Bind(TextElement.FontSizeProperty, this.GetResourceObservable(ThemeKeys.FontSize.Sm));
            wall.Children.Clear();
            foreach (var item in Files) wall.Children.Add(Card(item));
            if (drop.IsVisible) wall.Children.Add(drop);
            AutomationProperties.SetLiveSetting(wall, AutomationLiveSetting.Polite);
            root.Children.Add(wall);
            hint.IsVisible = Pasteable && editable;
            hint.Text = WithPasteHint(lang);
            root.Children.Add(hint);
            return;
        }
        drop.Content = null;
        drop.Width = drop.Height = double.NaN;
        drop.Compact = false;
        drop.ClearValue(TextElement.FontSizeProperty);
        drop.Description = Pasteable ? WithPasteHint(lang) : Description;
        root.Children.Add(drop);
        list.Children.Clear();
        foreach (var item in Files) list.Children.Add(Row(item));
        list.IsVisible = Files.Count > 0;
        // 状态变化（失败、完成）由读屏播报
        AutomationProperties.SetLiveSetting(list, AutomationLiveSetting.Polite);
        root.Children.Add(list);
    }

    /// <summary>说明后面接上粘贴的提示。</summary>
    string WithPasteHint(Lang lang) => Description is { Length: > 0 } description ? $"{description} · {lang.UploadPasteHint}" : lang.UploadPasteHint;

    double Em => TextElement.GetFontSize(this);

    double Token(string key) => this.Resource<double>(key) * Em;

    /// <summary>列表模式的一项：图标（按状态是文件、对勾或警告）、文件名（有地址时是链接）与下一行（大小、错误或进度）、重试、移除。</summary>
    Control Row(UploadFile item)
    {
        var lang = Provider.GetLang(this);
        var failed = item.Status == UploadStatus.Error;
        var icon = new Icon
        {
            Name = UploadParts.Icon,
            Data = failed ? Lucide.CircleExclamationPoint : item.Status == UploadStatus.Done ? Lucide.CircleCheck : Lucide.File,
            Foreground = this.Resource<IBrush>(failed ? ThemeKeys.Brush.DangerText : item.Status == UploadStatus.Done ? ThemeKeys.Brush.SuccessText : ThemeKeys.Brush.TextMuted),
        };
        Control name = item.Url is { } url
            ? new Link { Name = UploadParts.Link, Href = url, Underline = LinkUnderline.Hover, Content = item.Name, HorizontalAlignment = HorizontalAlignment.Left }
            : new Text { Name = UploadParts.Name, Text = item.Name, Truncate = true };
        Control meta = item.Status == UploadStatus.Uploading
            ? Bar(item)
            : new Text
            {
                Name = UploadParts.Meta,
                Size = TextSize.Sm,
                Text = failed ? item.Error : item.Size is { } size ? FormatSize(size) : null,
                Tone = failed ? TextTone.Danger : TextTone.Muted,
            };
        // 没有大小也没有错误（已有文件不知道大小）：只剩文件名一行
        meta.IsVisible = meta is not Text { Text: null or "" };
        if (item.Status == UploadStatus.Done) AutomationProperties.SetHelpText(meta, lang.UploadDone);
        var body = new Flex { Name = UploadParts.Body, Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch, Children = { name, meta } };
        Flex.SetGrow(body, true);
        var row = new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { icon, body } };
        foreach (var button in Buttons(item, TextSize.Sm, onThumbnail: false)) row.Children.Add(button);
        return new Border
        {
            Name = UploadParts.Item,
            MinHeight = Math.Max(ControlHeight.Of(Em, this.Resource<double>(ThemeKeys.Size.Control)), this.Resource<double>(ThemeKeys.Size.MinTarget)),
            Padding = new Thickness(Token(ThemeKeys.Space.Sm), Token(ThemeKeys.Space.Xs)),
            BorderThickness = new Thickness(this.Resource<double>(ThemeKeys.BorderWidth.Xs)),
            BorderBrush = this.Resource<IBrush>(ThemeKeys.Brush.BorderMuted),
            CornerRadius = new CornerRadius(Token(ThemeKeys.Radius.Md)),
            Child = row,
            Tag = item,
        };
    }

    /// <summary>上传中的进度条（xs）：没有进度时是不确定的；名称是文件名。</summary>
    static Progress Bar(UploadFile item)
    {
        var bar = new Progress { Name = UploadParts.Progress, Size = TextSize.Xs, Value = item.Progress is { } fraction ? Math.Round((decimal)fraction * 100) : null };
        AutomationProperties.SetName(bar, item.Name);
        return bar;
    }

    /// <summary>重试（失败、有上传函数、不是没通过检查的）与移除；只读时都没有。图片墙上的按钮是 xs、半透明的表面色底。</summary>
    IEnumerable<Control> Buttons(UploadFile item, TextSize size, bool onThumbnail)
    {
        if (ReadOnly) yield break;
        var lang = Provider.GetLang(this);
        if (item.Status == UploadStatus.Error && Uploader is not null && !item.Rejected)
        {
            var retry = new IconButton { Name = UploadParts.Retry, Icon = Lucide.RotateCw, Variant = ButtonVariant.Ghost, Size = size, IsEnabled = IsEffectivelyEnabled };
            AutomationProperties.SetName(retry, lang.UploadRetry(item.Name));
            retry.Click += (_, _) => _ = Start(Files.First(file => file.Id == item.Id));
            yield return Translucent(retry, onThumbnail);
        }
        var remove = new IconButton { Name = UploadParts.Remove, Icon = Lucide.X, Variant = ButtonVariant.Ghost, Size = size, IsEnabled = IsEffectivelyEnabled };
        AutomationProperties.SetName(remove, lang.UploadRemove(item.Name));
        remove.Click += (_, _) => Remove(item.Id);
        yield return Translucent(remove, onThumbnail);
    }

    /// <summary>图片墙上的小按钮：半透明表面色底（白底配主题文字色在深色主题里读不清），文字跟着主题。</summary>
    Control Translucent(Control button, bool onThumbnail)
    {
        if (!onThumbnail) return button;
        var surface = this.Resource<IBrush>(ThemeKeys.Brush.Surface) is ISolidColorBrush solid ? solid.Color : Colors.White;
        return new Border { Background = new SolidColorBrush(surface, 0.85), CornerRadius = new CornerRadius(Token(ThemeKeys.Radius.Sm)), Child = button };
    }

    /// <summary>图片墙的一格：缩略图（点开看大图）或图标加文件名、上传进度或错误、把手、重试与移除。</summary>
    Control Card(UploadFile item)
    {
        var size = ThumbnailSize;
        var failed = item.Status == UploadStatus.Error;
        var source = item.Url ?? (item.File is { } file && IsImage(file.Name) ? file.Path : null);
        var card = new Panel { Name = UploadParts.Card, Width = size, Height = size, Tag = item };
        var frame = new Border
        {
            BorderThickness = new Thickness(this.Resource<double>(ThemeKeys.BorderWidth.Xs)),
            BorderBrush = this.Resource<IBrush>(failed ? ThemeKeys.Brush.Danger : ThemeKeys.Brush.BorderMuted),
            CornerRadius = new CornerRadius(Token(ThemeKeys.Radius.Md)),
            Background = this.Resource<IBrush>(ThemeKeys.Brush.Fill),
            ClipToBounds = true,
        };
        var content = new Panel();
        if (source is not null && !item.Rejected)
            content.Children.Add(new Image { Name = UploadParts.Image, Source = source, Alt = item.Name, Width = size - 2, Height = size - 2, Radius = Radius.None, Preview = true, Caption = item.Name, DownloadName = item.Name });
        else
        {
            var placeholder = new Flex
            {
                Name = UploadParts.Placeholder, Direction = FlexDirection.Column, Align = FlexAlign.Center, Justify = FlexJustify.Center, Gap = Space.Xs,
                Margin = new Thickness(Token(ThemeKeys.Space.Xs)),
                Children =
                {
                    new Icon { Data = failed ? Lucide.CircleExclamationPoint : Lucide.File, Foreground = this.Resource<IBrush>(failed ? ThemeKeys.Brush.DangerText : ThemeKeys.Brush.TextMuted) },
                    new Small { Size = TextSize.Xs, Tone = TextTone.Muted, Text = item.Name, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center },
                },
            };
            content.Children.Add(placeholder);
        }
        if (item.Status == UploadStatus.Uploading || failed)
        {
            // 上传中、失败：盖在缩略图下部
            var overlay = new Border
            {
                Name = UploadParts.Overlay, VerticalAlignment = VerticalAlignment.Bottom, Padding = new Thickness(Token(ThemeKeys.Space.Xs)),
                Background = this.Resource<IBrush>(ThemeKeys.Brush.Scrim),
                Child = failed ? new TextBlock { Text = item.Error, FontSize = this.Resource<double>(ThemeKeys.FontSize.Xs), Foreground = this.Resource<IBrush>(ThemeKeys.Brush.OnScrim), TextTrimming = TextTrimming.CharacterEllipsis } : Bar(item),
            };
            if (failed) ToolTip.SetTip(overlay, item.Error);
            content.Children.Add(overlay);
        }
        frame.Child = content;
        card.Children.Add(frame);
        if (item.Status == UploadStatus.Done) AutomationProperties.SetHelpText(card, Provider.GetLang(this).UploadDone);
        var actions = new Flex { Name = UploadParts.Actions, Gap = Space.Xxs, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(Token(ThemeKeys.Space.Xs)) };
        if (Sortable && !ReadOnly) actions.Children.Add(Handle(item));
        foreach (var button in Buttons(item, TextSize.Xs, onThumbnail: true)) actions.Children.Add(button);
        card.Children.Add(actions);
        if (sorting.Active == item.Id) card.Opacity = 0.5;
        return card;
    }

    static bool IsImage(string name) => FileAccept.Matches(name, "image/*");

    /// <summary>拖动把手：键盘空格拿起、←→ 移动、空格放下、Esc 取消；指针按下拖动，越过别的格就换位置。</summary>
    Control Handle(UploadFile item)
    {
        var handle = new IconButton { Name = UploadParts.Handle, Icon = Lucide.GripVertical, Variant = ButtonVariant.Ghost, Size = TextSize.Xs, IsEnabled = IsEffectivelyEnabled, Cursor = new Cursor(StandardCursorType.SizeAll) };
        AutomationProperties.SetName(handle, sorting.HandleName(item.Id));
        AutomationProperties.SetHelpText(handle, sorting.Instructions);
        handle.AddHandler(KeyDownEvent, (_, e) =>
        {
            var key = e.Key switch
            {
                Key.Space or Key.Enter => SortableKey.Toggle,
                Key.Escape => SortableKey.Escape,
                Key.Left => SortableKey.Left,
                Key.Right => SortableKey.Right,
                Key.Up => SortableKey.Up,
                Key.Down => SortableKey.Down,
                Key.Home => SortableKey.Home,
                Key.End => SortableKey.End,
                _ => (SortableKey?)null,
            };
            if (key is { } pressed && sorting.Press(item.Id, pressed, horizontal: true, FlowDirection == FlowDirection.RightToLeft)) e.Handled = true;
        }, RoutingStrategies.Tunnel);
        handle.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed) return;
            sorting.Grab(item.Id);
            e.Pointer.Capture(handle);
            e.Handled = true;
        }, RoutingStrategies.Tunnel);
        handle.AddHandler(PointerMovedEvent, (_, e) =>
        {
            if (sorting.Active != item.Id) return;
            var point = e.GetPosition(wall);
            var cards = wall.Children.OfType<Panel>().Where(card => card.Tag is UploadFile).ToList();
            var over = cards.FindIndex(card => card.Bounds.Contains(point));
            if (over >= 0) sorting.DragOver(item.Id, null, over);
        }, RoutingStrategies.Tunnel);
        handle.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (sorting.Active != item.Id) return;
            e.Pointer.Capture(null);
            sorting.Drop(item.Id);
        }, RoutingStrategies.Tunnel);
        handle.LostFocus += (_, _) => sorting.Blur(item.Id);
        return handle;
    }

    /// <summary>Ctrl + V：剪贴板里的文件按 Accept 分开（不符合的照常列出、带错误）；截图（位图）存成临时的 PNG 再加进来。</summary>
    protected override async void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || !Pasteable || ReadOnly || !IsEffectivelyEnabled || e.Key != Key.V || !e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        if (TopLevel.GetTopLevel(this) is not { Clipboard: { } clipboard, StorageProvider: { } storage }) return;
        e.Handled = true;
        await Paste(await PastedFiles(clipboard, storage));
    }

    /// <summary>粘贴进来的文件：类型对的经过 Transform 加进来，不对的带错误列出。</summary>
    internal async Task Paste(IReadOnlyList<IStorageFile> files)
    {
        if (files.Count == 0) return;
        var accepted = files.Where(file => FileAccept.Matches(file.Name, Accept)).Select(file => new PickedFile(file, file.Name)).ToList();
        var rejected = files.Where(file => !FileAccept.Matches(file.Name, Accept)).Select(file => new PickedFile(file, file.Name)).ToList();
        if (accepted.Count > 0) await Receive(accepted);
        if (rejected.Count > 0) await Add(rejected, rejectedType: true);
    }

    static async Task<IReadOnlyList<IStorageFile>> PastedFiles(IClipboard clipboard, IStorageProvider storage)
    {
        if (await clipboard.TryGetFilesAsync() is { Length: > 0 } items) return [.. items.OfType<IStorageFile>()];
        if (await clipboard.TryGetBitmapAsync() is not { } bitmap) return [];
        // 截图在剪贴板里是位图：存成临时的 PNG（浏览器里粘贴截图得到的也是 image.png）
        var path = Path.Combine(Path.GetTempPath(), $"image-{Guid.NewGuid():N}.png");
        bitmap.Save(path, PngBitmapEncoderOptions.Default);
        return await storage.TryGetFileFromPathAsync(path) is { } saved ? [saved] : [];
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        root.Measure(availableSize);
        announcer.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
        announcer.Arrange(default);
        return finalSize;
    }
}

/// <summary>Upload 的部件名（TS 的 uploadRecipe 的部件）。</summary>
public static class UploadParts
{
    /// <summary>选择区域（FileDrop）。</summary>
    public const string Drop = "PART_Drop";

    /// <summary>粘贴的提示（图片墙）。</summary>
    public const string Hint = "PART_Hint";

    /// <summary>列表（列表模式）。</summary>
    public const string List = "PART_List";

    /// <summary>每一项（列表模式）。</summary>
    public const string Item = "PART_Item";

    /// <summary>文件图标。</summary>
    public const string Icon = "PART_Icon";

    /// <summary>文件名与下一行的外层。</summary>
    public const string Body = "PART_Body";

    /// <summary>文件名。</summary>
    public const string Name = "PART_Name";

    /// <summary>有地址时的文件名（链接）。</summary>
    public const string Link = "PART_Link";

    /// <summary>上传中的进度条。</summary>
    public const string Progress = "PART_Progress";

    /// <summary>大小或错误。</summary>
    public const string Meta = "PART_Meta";

    /// <summary>重试按钮。</summary>
    public const string Retry = "PART_Retry";

    /// <summary>移除按钮。</summary>
    public const string Remove = "PART_Remove";

    /// <summary>图片墙（ImageGroup）。</summary>
    public const string Wall = "PART_Wall";

    /// <summary>图片墙的一格。</summary>
    public const string Card = "PART_Card";

    /// <summary>缩略图。</summary>
    public const string Image = "PART_Image";

    /// <summary>不是图片时的图标与文件名。</summary>
    public const string Placeholder = "PART_Placeholder";

    /// <summary>上传中、失败时盖在缩略图上的一层。</summary>
    public const string Overlay = "PART_Overlay";

    /// <summary>右上角的把手、重试、移除。</summary>
    public const string Actions = "PART_Actions";

    /// <summary>拖动排序的把手。</summary>
    public const string Handle = "PART_Handle";
}
