using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;

namespace Rune.Avalonia.Tests;

/// <summary>Mentions：拆出提及（取最长、邮箱不算）；输入 @ 弹出、按名字过滤、Enter 写成"@名字 "；Esc 后这个 @ 不再弹出；已有的提及算进 Mentioned；MentionText 高亮。</summary>
public class MentionsTests
{
    static readonly ListboxOption[] People = [new("U01", "林小满"), new("U02", "林小"), new("U03", "陆川"), new("U04", "周宁")];

    static (Mentions Mentions, Window Window) Render(string initial = "")
    {
        var mentions = new Mentions { ItemsSource = People, Text = initial };
        var window = HeadlessUi.Show(mentions);
        mentions.Focus();
        HeadlessUi.Flush(window);
        return (mentions, window);
    }

    // 不排空调度队列：headless 里弹出层打开后会抢走焦点（桌面上点外面才关的弹出层不抢），失焦就关上了；同 ComboboxTests
    static void Type(Mentions mentions, string text)
    {
        mentions.Text = text;
        mentions.CaretIndex = text.Length;
        mentions.Detect();
    }

    static void Press(Mentions mentions, Key key) =>
        mentions.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = mentions });

    static List<string> Options(Mentions mentions) => [.. mentions.List.State.Options.Select(entry => entry.Option.Label)];

    [Fact]
    [Upstream("Mentions", "按显示文字拆出提及，有多个能匹配时取最长的；邮箱里的 @ 不算")]
    public void Segments_take_the_longest_label_and_skip_emails()
    {
        var segments = MentionParsing.Segments("请@林小满 看下，抄送 a@陆川.com 与 @周宁", People);
        Assert.Equal([("请", (string?)null), ("@林小满", "U01"), (" 看下，抄送 a@陆川.com 与 ", null), ("@周宁", "U04")], segments.Select(segment => (segment.Text, segment.Mention?.Value)));
        Assert.Equal(["U04", "U01"], MentionParsing.MentionedValues("@周宁 @林小满 @周宁", People));
    }

    [Fact]
    [Upstream("Mentions", "输入 @ 弹出全部，接着输入按名字过滤；Enter 选中写成“@名字 ”，mentions 跟着更新")]
    public Task Typing_at_opens_filters_and_enter_picks() => HeadlessUi.Run(() =>
    {
        var (mentions, _) = Render();
        Type(mentions, "请 @");
        Assert.True(mentions.IsOpen);
        Assert.Equal(["林小满", "林小", "陆川", "周宁"], Options(mentions));
        Type(mentions, "请 @林");
        Assert.Equal(["林小满", "林小"], Options(mentions));
        Assert.NotNull(mentions.List.State.Active);
        Press(mentions, Key.Enter);
        Assert.Equal("请 @林小满 ", mentions.Text);
        Assert.Equal(["U01"], mentions.Mentioned);
        Assert.False(mentions.IsOpen);

        // 删掉"@林小满"就不再算提及
        Type(mentions, "请 ");
        Assert.Empty(mentions.Mentioned);
    });

    [Fact]
    [Upstream("Mentions", "Esc 关掉后这个 @ 不再弹出，换一个 @ 照常；@ 后面有空白或前面是字母数字时不弹出")]
    public Task Escape_dismisses_this_at_only() => HeadlessUi.Run(() =>
    {
        var (mentions, _) = Render();
        Type(mentions, "@陆");
        Assert.True(mentions.IsOpen);
        Press(mentions, Key.Escape);
        Assert.False(mentions.IsOpen);
        Type(mentions, "@陆川");
        Assert.False(mentions.IsOpen);
        Type(mentions, "@陆川 @");
        Assert.True(mentions.IsOpen);
        Type(mentions, "@陆川 @周 ");
        Assert.False(mentions.IsOpen);
        Type(mentions, "lin@");
        Assert.False(mentions.IsOpen);
    });

    [Fact]
    [Upstream("Mentions", "父级给的文字里已有的提及也算进 mentions")]
    public Task Existing_mentions_are_counted() => HeadlessUi.Run(() =>
    {
        var (mentions, _) = Render("@周宁 请确认");
        Assert.Equal(["U04"], mentions.Mentioned);
    });

    [Fact]
    [Upstream("MentionText", "提及高亮成单独的 span，其余文字原样")]
    public Task Mention_text_highlights_mentions() => HeadlessUi.Run(() =>
    {
        var text = new MentionText { Value = "@陆川 收到", Options = People };
        HeadlessUi.Show(text);
        var runs = text.Inlines!.OfType<Run>().ToList();
        Assert.Equal(["@陆川", " 收到"], runs.Select(run => run.Text));
        Assert.NotNull(runs[0].Background);
        Assert.Null(runs[1].Background);
    });

    [Fact]
    [Upstream("Mentions", "Mentions：列表开着时组合中的 Enter 不提及")]
    public Task Composing_enter_does_not_pick() => HeadlessUi.Run(() =>
    {
        var mentions = new Mentions { ItemsSource = new ListboxOption[] { new("U1", "周宁"), new("U2", "陆川") } };
        HeadlessUi.Show(mentions);
        mentions.Focus();
        Type(mentions, "@");
        Assert.NotEmpty(mentions.List.State.Options);
        // 输入法组合中的 Enter：平台报 ImeProcessed，交给输入法
        Press(mentions, Key.ImeProcessed);
        Assert.Equal("@", mentions.Text);
        Press(mentions, Key.Enter);
        Assert.Equal("@周宁 ", mentions.Text);
    });
}
