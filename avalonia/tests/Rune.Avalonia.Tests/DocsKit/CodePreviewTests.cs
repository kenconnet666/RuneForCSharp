using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>CodePreview：效果区是一组；源码默认收起，按钮切换；收起时不高亮，工具栏上的意图或展开时才取源码、只取一次；actions 在工具栏最前面。</summary>
public class CodePreviewTests
{
    static Border Preview(CodePreview preview) => preview.GetVisualDescendants().OfType<Border>().Single(border => border.Name == CodePreviewParts.Preview);

    static Border Toolbar(CodePreview preview) => preview.GetVisualDescendants().OfType<Border>().Single(border => border.Name == CodePreviewParts.Toolbar);

    [Fact]
    [Upstream("CodePreview", "效果区是一组；源码默认收起，按钮切换并带 aria-expanded / aria-controls")]
    public Task The_preview_is_a_group_and_the_code_toggles() => HeadlessUi.Run(() =>
    {
        var preview = new CodePreview { Code = "<Button>保存</Button>", Content = new Button { Content = "保存" } };
        HeadlessUi.Show(preview);

        var group = Preview(preview).Peer();
        Assert.Equal((AutomationControlType.Group, "预览"), (group.GetAutomationControlType(), group.GetName()));
        var toggle = (IExpandCollapseProvider)preview.Toggle.Peer();
        Assert.Equal(("显示代码", global::Avalonia.Automation.ExpandCollapseState.Collapsed), (preview.Toggle.Content, toggle.ExpandCollapseState));
        Assert.Equal("<Button>保存</Button>", preview.CodeBlock.Code);
        var layout = preview.GetVisualDescendants().OfType<Flex>().First(flex => flex.Name == CodePreviewParts.Layout);
        Assert.Equal(new Thickness(1.5 * preview.GetValue(global::Avalonia.Controls.Documents.TextElement.FontSizeProperty)), layout.Margin); // 效果的排布四周 space.xl

        ((IInvokeProvider)preview.Toggle.Peer()).Invoke();
        Assert.True(preview.IsCodeShown);
        Assert.Equal(("隐藏代码", global::Avalonia.Automation.ExpandCollapseState.Expanded), (preview.Toggle.Content, toggle.ExpandCollapseState));
        // 复制按钮在工具栏上，源码里不再重复一个
        Assert.Single(preview.GetVisualDescendants().OfType<CopyButton>(), button => button.IsVisible);
    });

    [Fact]
    [Upstream("CodePreview", "收起的源码不高亮：指针移到或焦点进入工具栏时才交给高亮器")]
    public Task Collapsed_code_is_highlighted_only_on_intent() => HeadlessUi.Run(() =>
    {
        var preview = new CodePreview { Code = "var a = 1;", Content = "x" };
        var window = HeadlessUi.Show(preview);

        Assert.False(preview.CodeBlock.Highlight);
        var toolbar = Toolbar(preview);
        window.MouseMove(toolbar.TranslatePoint(new global::Avalonia.Point(4, 4), window)!.Value);
        HeadlessUi.Flush(window);
        Assert.True(preview.CodeBlock.Highlight);
    });

    [Fact]
    [Upstream("CodePreview", "按需加载的源码：打开时不加载，工具栏上的意图或展开时才加载一次；复制拿到的是加载的源码")]
    public Task Lazy_code_loads_once_on_intent_or_expand() => HeadlessUi.Run(() =>
    {
        var loads = 0;
        var preview = new CodePreview { CodeFactory = () => { loads++; return Task.FromResult("var lazy = true;"); }, Content = "x" };
        HeadlessUi.Show(preview);

        Assert.Equal(0, loads);
        preview.Toggle.Focus(); // 焦点进入工具栏
        preview.IsCodeShown = true;
        HeadlessUi.Until(() => preview.CodeBlock.Code == "var lazy = true;");
        Assert.Equal(1, loads);
    });

    [Fact]
    [Upstream("CodePreview", "actions 排在工具栏最前面")]
    public Task Actions_come_first_in_the_toolbar() => HeadlessUi.Run(() =>
    {
        var edit = new Button { Content = "编辑" };
        var preview = new CodePreview { Code = "x", Content = "x", Actions = edit };
        HeadlessUi.Show(preview);

        var buttons = Toolbar(preview).GetVisualDescendants().OfType<global::Avalonia.Controls.Button>().ToList();
        Assert.Same(edit, buttons[0]);
        Assert.Same(preview.Toggle, buttons[1]);
    });
}
