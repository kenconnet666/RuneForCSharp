using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Rune.Avalonia;

/// <summary>留言的一个附件。</summary>
/// <param name="Name">文件名。</param>
/// <param name="Url">地址：文件名链到它（预览、下载）。</param>
/// <param name="Size">大小（字节）。</param>
public sealed record CommentAttachment(string Name, Uri? Url = null, long? Size = null);

/// <summary>一条留言（vue-tsx-pro 的 CommentItem）。</summary>
/// <param name="Id">标识。</param>
/// <param name="Author">作者。</param>
/// <param name="Content">内容：文字，"@名字"按 People 高亮。</param>
/// <param name="Time">发表的时间。</param>
public sealed record CommentItem(string Id, string Author, string Content, DateTimeOffset Time)
{
    /// <summary>作者的头像；没有时显示名字的首字母。</summary>
    public IImage? Avatar { get; init; }

    /// <summary>作者的身份（岗位、"供应商"标记），在名字后面。</summary>
    public object? Role { get; init; }

    /// <summary>改过。</summary>
    public bool Edited { get; init; }

    /// <summary>附件。</summary>
    public IReadOnlyList<CommentAttachment>? Attachments { get; init; }

    /// <summary>内容下面的补充（逐条回应的"采纳 / 不采纳"、引用的字段对比）。</summary>
    public object? Extra { get; init; }

    /// <summary>回复（只分一层，回复里的 Replies 不显示）。</summary>
    public IReadOnlyList<CommentItem>? Replies { get; init; }
}

/// <summary>写好的一条留言：Send 收到它。</summary>
/// <param name="Content">内容（去掉首尾空白）。</param>
/// <param name="Mentions">内容里 @ 到的人（People 的 Value）。</param>
/// <param name="Attachments">上传完成的附件。</param>
/// <param name="ReplyTo">回复哪一串（那一串第一条留言的 id）；新的一串为 null。</param>
public sealed record CommentDraft(string Content, IReadOnlyList<string> Mentions, IReadOnlyList<UploadFile> Attachments, string? ReplyTo = null);

/// <summary>写留言的人：输入框前面的头像。</summary>
/// <param name="Name">名字：没有头像时显示首字母。</param>
/// <param name="Avatar">头像。</param>
public sealed record CommentAuthor(string Name, IImage? Avatar = null);

/// <summary>
/// 写留言的输入框（vue-tsx-pro 的 CommentComposer）：提及（<see cref="Mentions"/>）、附件（<see cref="Upload"/>）、发送。单独用在协商回复、审批意见里，CommentThread 里也是它。
/// <code>
/// &lt;rune:CommentComposer People="{Binding People}" Send="{Binding SendAsync}" /&gt;
/// </code>
/// 发送：按钮或 Ctrl + Enter；Send 返回的任务进行中按钮转圈，成功后清空，失败（抛异常）时保留内容并提示。附件还在上传时不能发送。
/// 写了 <see cref="Cancel"/> 时有取消按钮，按 Esc 也是取消（提及的列表开着时 Esc 先关列表）。部件：<see cref="CommentComposerParts"/>。
/// </summary>
public class CommentComposer : Control
{
    /// <summary>发送：任务完成后清空；失败（抛异常）时保留内容并提示。</summary>
    public static readonly StyledProperty<Func<CommentDraft, Task>?> SendProperty = AvaloniaProperty.Register<CommentComposer, Func<CommentDraft, Task>?>(nameof(Send));

    /// <summary>取消（回复时）：有它时显示取消按钮，按 Esc 也是取消。</summary>
    public static readonly StyledProperty<Action?> CancelProperty = AvaloniaProperty.Register<CommentComposer, Action?>(nameof(Cancel));

    /// <summary>可以 @ 的人。</summary>
    public static readonly StyledProperty<IReadOnlyList<ListboxOption>?> PeopleProperty = AvaloniaProperty.Register<CommentComposer, IReadOnlyList<ListboxOption>?>(nameof(People));

    /// <summary>上传附件；不给时不能带附件。</summary>
    public static readonly StyledProperty<UploadHandler?> UploaderProperty = AvaloniaProperty.Register<CommentComposer, UploadHandler?>(nameof(Uploader));

    /// <summary>写留言的人：输入框前面的头像。</summary>
    public static readonly StyledProperty<CommentAuthor?> AuthorProperty = AvaloniaProperty.Register<CommentComposer, CommentAuthor?>(nameof(Author));

    /// <summary>一开始的内容（回复时的"@作者 "）。</summary>
    public static readonly StyledProperty<string> InitialTextProperty = AvaloniaProperty.Register<CommentComposer, string>(nameof(InitialText), "");

    /// <summary>放进界面后聚焦输入框（点了回复）。</summary>
    public static readonly StyledProperty<bool> AutofocusProperty = AvaloniaProperty.Register<CommentComposer, bool>(nameof(Autofocus));

    /// <summary>输入框的提示；不写时取文案。</summary>
    public static readonly StyledProperty<string?> PlaceholderProperty = AvaloniaProperty.Register<CommentComposer, string?>(nameof(Placeholder));

    readonly global::Avalonia.Controls.Grid root = new() { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
    readonly Avatar avatar = new() { Name = CommentComposerParts.Avatar, Size = TextSize.Sm, Decorative = true, VerticalAlignment = VerticalAlignment.Top };
    readonly Flex main = new() { Name = CommentComposerParts.Main, Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch };
    readonly Mentions input = new() { Name = CommentComposerParts.Mentions, Rows = 2, MaxRows = 10 };
    readonly Upload upload = new() { Name = CommentComposerParts.Upload, Multiple = true, IsVisible = false };
    readonly Text error = new() { Name = CommentComposerParts.Error, Size = TextSize.Sm, Tone = TextTone.Danger, IsVisible = false };
    readonly Button attach = new() { Name = CommentComposerParts.Attach, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Size = TextSize.Sm, Icon = Lucide.Paperclip };
    readonly Text hint = new() { Name = CommentComposerParts.Hint, Size = TextSize.Xs, Tone = TextTone.Muted };
    readonly Button cancel = new() { Name = CommentComposerParts.Cancel, Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Size = TextSize.Sm };
    readonly Button send = new() { Name = CommentComposerParts.Send, Size = TextSize.Sm };
    readonly LiveRegion announcer = new();
    bool sending;

    /// <summary>新建：头像、输入框、附件、底部的按钮。</summary>
    public CommentComposer()
    {
        input.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.TextProperty) Sync();
        };
        upload.PropertyChanged += (_, e) =>
        {
            if (e.Property == Upload.FilesProperty) Sync();
        };
        // 隧道阶段先拿到：多行输入框自己会吃掉 Enter（换行）；提及的列表开着时这里不管，Esc、Enter 交给列表
        input.AddHandler(KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
        attach.Click += (_, _) =>
        {
            upload.IsVisible = !upload.IsVisible;
            Sync();
        };
        cancel.Click += (_, _) => Cancel?.Invoke();
        send.Click += (_, _) => _ = SendDraft();
        var left = new Flex { Name = CommentComposerParts.Tools, Align = FlexAlign.Center, Gap = Space.Sm, Children = { attach, hint } };
        var right = new Flex { Name = CommentComposerParts.Tools, Align = FlexAlign.Center, Gap = Space.Sm, Children = { cancel, send } };
        var footer = new Flex { Name = CommentComposerParts.Footer, Align = FlexAlign.Center, Justify = FlexJustify.Between, Wrap = FlexWrap.Wrap, Gap = Space.Xs, Children = { left, right } };
        main.Children.AddRange([input, upload, error, footer]);
        global::Avalonia.Controls.Grid.SetColumn(main, 1);
        root.Children.AddRange([avatar, main]);
        LogicalChildren.AddRange([root, announcer]);
        VisualChildren.AddRange([root, announcer]);
    }

    /// <summary>发送。</summary>
    public Func<CommentDraft, Task>? Send { get => GetValue(SendProperty); set => SetValue(SendProperty, value); }

    /// <summary>取消。</summary>
    public Action? Cancel { get => GetValue(CancelProperty); set => SetValue(CancelProperty, value); }

    /// <summary>可以 @ 的人。</summary>
    public IReadOnlyList<ListboxOption>? People { get => GetValue(PeopleProperty); set => SetValue(PeopleProperty, value); }

    /// <summary>上传附件。</summary>
    public UploadHandler? Uploader { get => GetValue(UploaderProperty); set => SetValue(UploaderProperty, value); }

    /// <summary>写留言的人。</summary>
    public CommentAuthor? Author { get => GetValue(AuthorProperty); set => SetValue(AuthorProperty, value); }

    /// <summary>一开始的内容。</summary>
    public string InitialText { get => GetValue(InitialTextProperty); set => SetValue(InitialTextProperty, value); }

    /// <summary>放进界面后聚焦输入框。</summary>
    public bool Autofocus { get => GetValue(AutofocusProperty); set => SetValue(AutofocusProperty, value); }

    /// <summary>输入框的提示。</summary>
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }

    /// <summary>输入框（测试用；读屏名称在 CommentThread 里换成"回复 某人"）。</summary>
    internal Mentions Input => input;

    /// <summary>发送按钮（测试用）。</summary>
    internal Button SendButton => send;

    /// <summary>发送失败的提示（测试用）。</summary>
    internal Text ErrorPart => error;

    /// <summary>附件还在上传：等它完成才能发送。</summary>
    bool Uploading => upload.Files.Any(file => file.Status is UploadStatus.Uploading or UploadStatus.Selected);

    bool Sendable => (input.Text ?? "").Trim().Length > 0 && !Uploading && !sending;

    /// <summary>发送：成功后清空内容与附件、收起附件；失败时保留并提示（读屏播报）。</summary>
    internal async Task SendDraft()
    {
        if (!Sendable || Send is not { } handler) return;
        sending = true;
        error.IsVisible = false;
        Sync();
        try
        {
            await handler(new CommentDraft((input.Text ?? "").Trim(), input.Mentioned, [.. upload.Files.Where(file => file.Status == UploadStatus.Done)]));
            input.Text = "";
            upload.Files = [];
            upload.IsVisible = false;
        }
        catch (Exception)
        {
            error.IsVisible = true;
            announcer.Announce(error.Text ?? "");
        }
        finally
        {
            sending = false;
            Sync();
        }
    }

    void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled || input.IsOpen) return;
        if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            e.Handled = true;
            _ = SendDraft();
        }
        else if (e.Key == Key.Escape && Cancel is { } close)
        {
            e.Handled = true;
            close();
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == InitialTextProperty) input.Text = InitialText;
        else if (change.Property == PeopleProperty) input.ItemsSource = People;
        else if (change.Property == UploaderProperty) upload.Uploader = Uploader;
        else if (change.Property == SendProperty || change.Property == CancelProperty || change.Property == AuthorProperty || change.Property == PlaceholderProperty
                 || change.Property == Provider.LangProperty) Sync();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Sync();
        if (!Autofocus) return;
        Dispatcher.UIThread.Post(() =>
        {
            input.Focus();
            input.CaretIndex = (input.Text ?? "").Length;
        }, DispatcherPriority.Loaded);
    }

    /// <summary>按状态：头像、提示文字、附件按钮（有附件时带个数）、取消、发送（不能发送时不可用，发送中转圈）。</summary>
    void Sync()
    {
        var messages = PatternsMessages.For(Provider.GetLang(this));
        avatar.IsVisible = Author is not null;
        avatar.DisplayName = Author?.Name;
        avatar.Source = Author?.Avatar;
        root.ColumnDefinitions[0].Width = Author is null ? new GridLength(0) : GridLength.Auto;
        root.ColumnSpacing = Author is null ? 0 : this.Resource<double>(ThemeKeys.Space.Sm) * TextElement.GetFontSize(this);
        input.PlaceholderText = Placeholder ?? messages.CommentPlaceholder;
        if (AutomationProperties.GetName(input) is null or "") AutomationProperties.SetName(input, messages.Comments);
        attach.IsVisible = Uploader is not null;
        attach.Content = upload.Files.Count > 0 ? $"{messages.CommentAttach} {upload.Files.Count}" : messages.CommentAttach;
        AutomationProperties.SetItemStatus(attach, upload.IsVisible ? "pressed" : null);
        hint.Text = messages.CommentSendHint;
        cancel.IsVisible = Cancel is not null;
        cancel.Content = messages.CommentCancel;
        send.Content = messages.CommentSend;
        send.IsLoading = sending;
        send.IsEnabled = Sendable;
        error.Text = messages.CommentSendFailed;
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

/// <summary>
/// 留言（vue-tsx-pro 的 CommentThread）：一串留言与回复（沟通记录、协商的逐条回应、审批时的讨论），下面是写留言的输入框（<see cref="CommentComposer"/>）。
/// <code>
/// &lt;rune:CommentThread Comments="{Binding Comments}" People="{Binding People}" Send="{Binding SendAsync}" CurrentUser="{Binding Me}" /&gt;
/// </code>
/// <list type="bullet">
/// <item>回复只分一层：回复一条回复时仍挂在那一串下面，开头写上"@作者 "，Send 收到的 ReplyTo 是那一串第一条的 id；发出后收起回复框。</item>
/// <item>留言存在哪、发出去以后怎样出现在 Comments 里由应用决定（乐观更新或重新取）。没有 Send 时只读（没有输入框与回复按钮）。</item>
/// <item>时间写成相对现在（3 分钟前，<see cref="Now"/> 可以固定"现在"）；<see cref="Actions"/> 给每条留言加操作（编辑、删除、标为已解决）。</item>
/// </list>
/// 部件：<see cref="CommentThreadParts"/>。
/// </summary>
public class CommentThread : Control
{
    /// <summary>留言，旧的在前；回复在各自的 Replies 里。</summary>
    public static readonly StyledProperty<IReadOnlyList<CommentItem>?> CommentsProperty = AvaloniaProperty.Register<CommentThread, IReadOnlyList<CommentItem>?>(nameof(Comments));

    /// <summary>可以 @ 的人：输入框的候选，也是留言里"@名字"高亮的依据。</summary>
    public static readonly StyledProperty<IReadOnlyList<ListboxOption>?> PeopleProperty = CommentComposer.PeopleProperty.AddOwner<CommentThread>();

    /// <summary>发送一条留言或回复；不给时只读。</summary>
    public static readonly StyledProperty<Func<CommentDraft, Task>?> SendProperty = CommentComposer.SendProperty.AddOwner<CommentThread>();

    /// <summary>上传附件；不给时不能带附件。</summary>
    public static readonly StyledProperty<UploadHandler?> UploaderProperty = CommentComposer.UploaderProperty.AddOwner<CommentThread>();

    /// <summary>当前用户：输入框前面的头像。</summary>
    public static readonly StyledProperty<CommentAuthor?> CurrentUserProperty = AvaloniaProperty.Register<CommentThread, CommentAuthor?>(nameof(CurrentUser));

    /// <summary>每条留言的操作（编辑、删除、标为已解决），在回复按钮后面。</summary>
    public static readonly StyledProperty<Func<CommentItem, Control?>?> ActionsProperty = AvaloniaProperty.Register<CommentThread, Func<CommentItem, Control?>?>(nameof(Actions));

    /// <summary>没有留言时显示的内容；不写时取文案。</summary>
    public static readonly StyledProperty<object?> EmptyProperty = AvaloniaProperty.Register<CommentThread, object?>(nameof(Empty));

    /// <summary>"现在"：相对时间按它算，不写时是当前时间（文档与测试里固定它）。</summary>
    public static readonly StyledProperty<DateTimeOffset?> NowProperty = AvaloniaProperty.Register<CommentThread, DateTimeOffset?>(nameof(Now));

    readonly Flex root = new() { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch };
    readonly CommentComposer composer = new() { Name = CommentThreadParts.Composer };
    // 正在回复：哪一串（第一条的 id）与它的回复框；Comments 变了重建列表时搬过去，写了一半的内容不丢
    (string Thread, CommentComposer Box)? replying;

    /// <summary>新建。</summary>
    public CommentThread()
    {
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
    }

    /// <summary>留言。</summary>
    public IReadOnlyList<CommentItem>? Comments { get => GetValue(CommentsProperty); set => SetValue(CommentsProperty, value); }

    /// <summary>可以 @ 的人。</summary>
    public IReadOnlyList<ListboxOption>? People { get => GetValue(PeopleProperty); set => SetValue(PeopleProperty, value); }

    /// <summary>发送。</summary>
    public Func<CommentDraft, Task>? Send { get => GetValue(SendProperty); set => SetValue(SendProperty, value); }

    /// <summary>上传附件。</summary>
    public UploadHandler? Uploader { get => GetValue(UploaderProperty); set => SetValue(UploaderProperty, value); }

    /// <summary>当前用户。</summary>
    public CommentAuthor? CurrentUser { get => GetValue(CurrentUserProperty); set => SetValue(CurrentUserProperty, value); }

    /// <summary>每条留言的操作。</summary>
    public Func<CommentItem, Control?>? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }

    /// <summary>没有留言时的内容。</summary>
    public object? Empty { get => GetValue(EmptyProperty); set => SetValue(EmptyProperty, value); }

    /// <summary>"现在"。</summary>
    public DateTimeOffset? Now { get => GetValue(NowProperty); set => SetValue(NowProperty, value); }

    /// <summary>下面写新留言的输入框（测试用）。</summary>
    internal CommentComposer Composer => composer;

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CommentsProperty || change.Property == PeopleProperty || change.Property == SendProperty || change.Property == UploaderProperty
            || change.Property == CurrentUserProperty || change.Property == ActionsProperty || change.Property == EmptyProperty || change.Property == NowProperty
            || change.Property == Provider.LangProperty) Render();
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Render();
    }

    double Token(string key) => this.Resource<double>(key) * TextElement.GetFontSize(this);

    void Render()
    {
        var messages = PatternsMessages.For(Provider.GetLang(this));
        root.Children.Clear();
        var comments = Comments ?? [];
        if (comments.Count == 0)
            root.Children.Add(Empty is Control empty ? Adopt(empty) : new Empty { Name = CommentThreadParts.Empty, Size = TextSize.Sm, Title = Empty as string ?? messages.CommentsEmpty });
        else
        {
            var list = new Flex { Name = CommentThreadParts.List, Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch };
            AutomationProperties.SetName(list, messages.Comments);
            foreach (var comment in comments) list.Children.Add(Item(comment, comment, messages));
            root.Children.Add(list);
        }
        if (Send is null) return;
        composer.People = People;
        composer.Uploader = Uploader;
        composer.Author = CurrentUser;
        composer.Send = Send;
        root.Children.Add(composer);
    }

    /// <summary>一条留言：头像一列、内容一列（作者、身份、时间、已编辑；内容；附件；补充；回复与操作；第一条下面是回复）。</summary>
    Control Item(CommentItem comment, CommentItem thread, PatternsMessages messages)
    {
        var item = new global::Avalonia.Controls.Grid { Name = CommentThreadParts.Item, ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = Token(ThemeKeys.Space.Sm) };
        item.Children.Add(new Avatar { Name = CommentThreadParts.Avatar, DisplayName = comment.Author, Source = comment.Avatar, Size = TextSize.Sm, Decorative = true, VerticalAlignment = VerticalAlignment.Top });
        var body = new Flex { Name = CommentThreadParts.Body, Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch };
        AutomationProperties.SetName(body, comment.Author);
        global::Avalonia.Controls.Grid.SetColumn(body, 1);
        var header = new Flex { Name = CommentThreadParts.Header, Align = FlexAlign.Center, Wrap = FlexWrap.Wrap, ColumnGap = Space.Sm, RowGap = Space.Xs };
        header.Children.Add(new Text { Name = CommentThreadParts.Author, Text = comment.Author, Weight = TextWeight.Medium });
        if (comment.Role is Control role) header.Children.Add(Adopt(role));
        else if (comment.Role is { } text) header.Children.Add(new Text { Text = text.ToString() });
        header.Children.Add(new RelativeTime { Name = CommentThreadParts.Meta, Value = comment.Time, Now = Now, Size = TextSize.Sm, Tone = TextTone.Muted });
        if (comment.Edited) header.Children.Add(new Text { Name = CommentThreadParts.Meta, Text = messages.CommentEdited, Size = TextSize.Sm, Tone = TextTone.Muted });
        body.Children.Add(header);
        body.Children.Add(new MentionText { Name = CommentThreadParts.Content, Value = comment.Content, Options = People });
        if (comment.Attachments is { Count: > 0 } attachments) body.Children.Add(Attachments(attachments, messages));
        if (comment.Extra is Control extra) body.Children.Add(Adopt(extra));
        if (Send is not null || Actions is not null)
        {
            var actions = new Flex { Name = CommentThreadParts.Actions, Align = FlexAlign.Center, Wrap = FlexWrap.Wrap, Gap = Space.Sm };
            if (Send is not null)
            {
                var reply = new Button { Name = CommentThreadParts.Reply, Variant = ButtonVariant.Link, Tone = StatusTone.Neutral, Size = TextSize.Sm, Content = messages.CommentReply };
                reply.Click += (_, _) => OpenReply(thread.Id, comment.Author, messages);
                actions.Children.Add(reply);
            }
            if (Actions?.Invoke(comment) is { } own) actions.Children.Add(own);
            body.Children.Add(actions);
        }
        if (comment == thread) Replies(thread, body, messages);
        item.Children.Add(body);
        return item;
    }

    /// <summary>附件：回形针、文件名（有地址时是链接）、大小，换行排（列间 md、行间 xs），sm 字号。</summary>
    Control Attachments(IReadOnlyList<CommentAttachment> attachments, PatternsMessages messages)
    {
        var list = new Flex { Name = CommentThreadParts.Attachments, Wrap = FlexWrap.Wrap, ColumnGap = Space.Md, RowGap = Space.Xs, Align = FlexAlign.Center };
        AutomationProperties.SetName(list, messages.CommentAttachments);
        list.Bind(TextElement.FontSizeProperty, this.GetResourceObservable(ThemeKeys.FontSize.Sm));
        foreach (var file in attachments)
        {
            var entry = new Flex { Name = CommentThreadParts.Attachment, Align = FlexAlign.Center, Gap = Space.Xxs };
            entry.Children.Add(new Icon { Data = Lucide.Paperclip });
            entry.Children.Add(file.Url is { } url ? new Link { Href = url, Underline = LinkUnderline.Hover, Content = file.Name } : new Text { Text = file.Name });
            if (file.Size is { } size) entry.Children.Add(new Text { Name = CommentThreadParts.Meta, Text = TableColumns.FileSize(size, Provider.GetLang(this).Code, "USD"), Tone = TextTone.Muted });
            list.Children.Add(entry);
        }
        return list;
    }

    /// <summary>一串的回复（缩进在内容列里、左边一条 borderMuted 的竖线）与回复的输入框。</summary>
    void Replies(CommentItem thread, Flex body, PatternsMessages messages)
    {
        var list = thread.Replies ?? [];
        if (list.Count > 0)
        {
            var replies = new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch };
            AutomationProperties.SetName(replies, messages.CommentReplies(list.Count));
            foreach (var reply in list) replies.Children.Add(Item(reply, thread, messages));
            body.Children.Add(new Border
            {
                Name = CommentThreadParts.Replies,
                Margin = new Thickness(0, Token(ThemeKeys.Space.Xs), 0, 0),
                Padding = new Thickness(Token(ThemeKeys.Space.Md), 0, 0, 0),
                BorderThickness = new Thickness(this.Resource<double>(ThemeKeys.BorderWidth.Sm), 0, 0, 0),
                Child = replies,
                [!Border.BorderBrushProperty] = this.GetResourceObservable(ThemeKeys.Brush.BorderMuted).ToBinding(),
            });
        }
        if (replying is { } open && open.Thread == thread.Id && Send is not null)
        {
            open.Box.People = People;
            open.Box.Uploader = Uploader;
            open.Box.Author = CurrentUser;
            body.Children.Add(Adopt(open.Box));
        }
    }

    /// <summary>打开回复框：开头写上"@作者 "，发送时带上这一串的 id，发出或取消后收起。</summary>
    void OpenReply(string thread, string to, PatternsMessages messages)
    {
        var box = new CommentComposer
        {
            InitialText = $"@{to} ", Autofocus = true,
            Cancel = CloseReply,
            Send = async draft =>
            {
                if (Send is { } send) await send(draft with { ReplyTo = thread });
                CloseReply();
            },
        };
        AutomationProperties.SetName(box.Input, messages.CommentReplyTo(to));
        replying = (thread, box);
        Render();
    }

    void CloseReply()
    {
        replying = null;
        Render();
    }

    /// <summary>重建列表时，使用方给的控件（Role、Extra、Empty）与回复框还挂在上一次的父级上：先拿下来再放进新的。</summary>
    static Control Adopt(Control control)
    {
        if (control.Parent is Panel panel) panel.Children.Remove(control);
        return control;
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        root.Measure(availableSize);
        return root.DesiredSize;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        root.Arrange(new Rect(finalSize));
        return finalSize;
    }
}

/// <summary>CommentComposer 的部件名。</summary>
public static class CommentComposerParts
{
    /// <summary>头像。</summary>
    public const string Avatar = "PART_Avatar";

    /// <summary>头像右边那一列（输入框、附件、按钮）。</summary>
    public const string Main = "PART_Main";

    /// <summary>输入框（Mentions）。</summary>
    public const string Mentions = "PART_Mentions";

    /// <summary>附件上传（Upload）。</summary>
    public const string Upload = "PART_Upload";

    /// <summary>发送失败的提示。</summary>
    public const string Error = "PART_Error";

    /// <summary>底部那一行。</summary>
    public const string Footer = "PART_Footer";

    /// <summary>底部两组按钮。</summary>
    public const string Tools = "PART_Tools";

    /// <summary>提示（Ctrl + Enter 发送）。</summary>
    public const string Hint = "PART_Hint";

    /// <summary>附件按钮。</summary>
    public const string Attach = "PART_Attach";

    /// <summary>取消按钮。</summary>
    public const string Cancel = "PART_Cancel";

    /// <summary>发送按钮。</summary>
    public const string Send = "PART_Send";
}

/// <summary>CommentThread 的部件名。</summary>
public static class CommentThreadParts
{
    /// <summary>留言列表。</summary>
    public const string List = "PART_List";

    /// <summary>每条留言。</summary>
    public const string Item = "PART_Item";

    /// <summary>头像。</summary>
    public const string Avatar = "PART_Avatar";

    /// <summary>头像右边的内容。</summary>
    public const string Body = "PART_Body";

    /// <summary>作者、身份、时间那一行。</summary>
    public const string Header = "PART_Header";

    /// <summary>作者名。</summary>
    public const string Author = "PART_Author";

    /// <summary>时间、已编辑、附件大小。</summary>
    public const string Meta = "PART_Meta";

    /// <summary>留言内容（MentionText）。</summary>
    public const string Content = "PART_Content";

    /// <summary>附件列表。</summary>
    public const string Attachments = "PART_Attachments";

    /// <summary>一个附件。</summary>
    public const string Attachment = "PART_Attachment";

    /// <summary>回复按钮与操作那一行。</summary>
    public const string Actions = "PART_Actions";

    /// <summary>回复按钮。</summary>
    public const string Reply = "PART_Reply";

    /// <summary>回复列表。</summary>
    public const string Replies = "PART_Replies";

    /// <summary>没有留言时的 Empty。</summary>
    public const string Empty = "PART_Empty";

    /// <summary>下面写新留言的输入框。</summary>
    public const string Composer = "PART_Composer";
}
