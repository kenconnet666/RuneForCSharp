using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;

namespace Rune.Avalonia.Tests;

/// <summary>CommentThread：两层列表、提及高亮、附件、相对时间；回复带"@作者 "与 ReplyTo、Ctrl + Enter 发送；发送失败保留内容并提示；没有 Send 时只读。</summary>
public class CommentThreadTests
{
    static readonly ListboxOption[] People = [new("U01", "周宁"), new("U02", "陆川")];
    static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-24T10:00:00Z");

    static readonly CommentItem[] Comments =
    [
        new("c1", "周宁", "@陆川 报价单已更新，请看附件", DateTimeOffset.Parse("2026-10-24T09:57:00Z"))
        {
            Attachments = [new CommentAttachment("报价单.xlsx", new Uri("https://files.example.com/1"), 24_000)],
            Replies = [new CommentItem("c2", "陆川", "收到，下午确认", DateTimeOffset.Parse("2026-10-24T09:58:00Z")) { Edited = true }],
        },
    ];

    static IEnumerable<T> All<T>(Control owner) => owner.GetLogicalDescendants().OfType<T>();

    static IEnumerable<Control> Named(Control owner, string name) => All<Control>(owner).Where(part => part.Name == name);

    static void Press(Control target, Key key, KeyModifiers modifiers = KeyModifiers.None) =>
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers, Source = target });

    static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    [Fact]
    [Upstream("CommentThread", "留言与回复两层列表；提及高亮、附件链接、相对时间与“已编辑”")]
    public Task Lists_comments_and_replies() => HeadlessUi.Run(() =>
    {
        var thread = new CommentThread { Comments = Comments, People = People, Now = Now };
        HeadlessUi.Show(thread);
        var list = (Flex)Named(thread, CommentThreadParts.List).Single();
        Assert.Equal("留言", AutomationProperties.GetName(list));
        Assert.Single(list.Children);
        var replies = thread.Part<Border>(CommentThreadParts.Replies);
        Assert.Equal("1 条回复", AutomationProperties.GetName(replies.Child!));
        var contents = All<MentionText>(thread).Select(text => text.Value).ToList();
        Assert.Equal(["@陆川 报价单已更新，请看附件", "收到，下午确认"], contents);
        Assert.Contains(All<Text>(replies), text => text.Text == "已编辑");
        var link = All<Link>(thread).Single();
        Assert.Equal(new Uri("https://files.example.com/1"), link.Href);
        Assert.Equal("报价单.xlsx", link.Content);
        Assert.Equal("3分钟前", All<RelativeTime>(thread).First().Text);
        // 只读：没有输入框与按钮
        Assert.Empty(All<Mentions>(thread));
        Assert.Empty(All<Button>(thread));
    });

    [Fact]
    [Upstream("CommentThread", "回复：输入框开头是“@作者 ”，发送带 replyTo（那一串的第一条），成功后收起")]
    public Task Reply_sends_reply_to_and_closes() => HeadlessUi.Run(() =>
    {
        var sent = new List<CommentDraft>();
        var thread = new CommentThread
        {
            Comments = Comments, People = People, Now = Now,
            Send = draft =>
            {
                sent.Add(draft);
                return Task.CompletedTask;
            },
        };
        var window = HeadlessUi.Show(thread);
        var replies = All<Button>(thread).Where(button => button.Name == CommentThreadParts.Reply).ToList();
        Assert.Equal(2, replies.Count);
        Click(replies[1]);
        HeadlessUi.Flush(window);
        var input = All<Mentions>(thread).Single(box => AutomationProperties.GetName(box) == "回复 陆川");
        Assert.Equal("@陆川 ", input.Text);
        input.Text = "@陆川 好的";
        Press(input, Key.Enter, KeyModifiers.Control);
        HeadlessUi.Flush(window);
        var draft = Assert.Single(sent);
        Assert.Equal("@陆川 好的", draft.Content);
        Assert.Equal(["U02"], draft.Mentions);
        Assert.Empty(draft.Attachments);
        Assert.Equal("c1", draft.ReplyTo);
        Assert.DoesNotContain(All<Mentions>(thread), box => AutomationProperties.GetName(box) == "回复 陆川");
    });

    [Fact]
    [Upstream("CommentThread", "发送失败：内容保留，提示失败")]
    public Task Failed_send_keeps_content() => HeadlessUi.Run(() =>
    {
        var thread = new CommentThread { Comments = [], People = People, Send = _ => Task.FromException(new InvalidOperationException("网络错误")) };
        var window = HeadlessUi.Show(thread);
        Assert.Equal("还没有留言", thread.Part<Empty>(CommentThreadParts.Empty).Title);
        var composer = thread.Composer;
        Assert.Equal("留言", AutomationProperties.GetName(composer.Input));
        Assert.False(composer.SendButton.IsEnabled);
        composer.Input.Text = "请尽快确认";
        Assert.True(composer.SendButton.IsEnabled);
        Click(composer.SendButton);
        HeadlessUi.Flush(window);
        Assert.True(composer.ErrorPart.IsVisible);
        Assert.Contains("发送失败", composer.ErrorPart.Text);
        Assert.Equal("请尽快确认", composer.Input.Text);
    });

    [Fact]
    [Upstream("CommentThread", "部件：各层带 data-part")]
    public Task Parts_are_named() => HeadlessUi.Run(() =>
    {
        var thread = new CommentThread { Comments = Comments, People = People, Now = Now, Send = _ => Task.CompletedTask, CurrentUser = new CommentAuthor("我") };
        HeadlessUi.Show(thread);
        foreach (var part in new[]
                 {
                     CommentThreadParts.List, CommentThreadParts.Item, CommentThreadParts.Avatar, CommentThreadParts.Body, CommentThreadParts.Header,
                     CommentThreadParts.Author, CommentThreadParts.Meta, CommentThreadParts.Content, CommentThreadParts.Attachments, CommentThreadParts.Attachment,
                     CommentThreadParts.Actions, CommentThreadParts.Reply, CommentThreadParts.Replies, CommentThreadParts.Composer,
                 })
            Assert.NotEmpty(Named(thread, part));
        Assert.Equal(2, Named(thread, CommentThreadParts.Item).Count());
        foreach (var part in new[] { CommentComposerParts.Avatar, CommentComposerParts.Main, CommentComposerParts.Mentions, CommentComposerParts.Footer, CommentComposerParts.Hint, CommentComposerParts.Send })
            Assert.True(Named(thread.Composer, part).Single().IsVisible, part);
        // 没给 Uploader、Cancel：没有附件、取消按钮
        Assert.False(thread.Composer.Part<Button>(CommentComposerParts.Attach).IsVisible);
        Assert.False(thread.Composer.Part<Button>(CommentComposerParts.Cancel).IsVisible);
    });

    [Fact]
    [Upstream("CommentComposer", "单独用：Ctrl+Enter 发送去掉首尾空白的内容，成功后清空；Esc 交给 onCancel")]
    public Task Composer_sends_trimmed_and_cancels() => HeadlessUi.Run(() =>
    {
        var sent = new List<CommentDraft>();
        var cancelled = 0;
        var composer = new CommentComposer
        {
            People = People, Cancel = () => cancelled++,
            Send = draft =>
            {
                sent.Add(draft);
                return Task.CompletedTask;
            },
        };
        var window = HeadlessUi.Show(composer);
        composer.Input.Text = "  下午确认  ";
        Press(composer.Input, Key.Enter, KeyModifiers.Control);
        HeadlessUi.Flush(window);
        Assert.Equal(["下午确认"], sent.Select(draft => draft.Content));
        Assert.Equal("", composer.Input.Text);
        Press(composer.Input, Key.Escape);
        Assert.Equal(1, cancelled);
    });
}
