using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>CodePreview：效果区是一组；源码默认收起，按钮切换；收起时不高亮，工具栏上的意图或展开时才取源码、只取一次；actions 在工具栏最前面。</summary>
public class CodePreviewTests
{
    static void Show(CodePreview preview)
    {
        var host = new StackPanel { Width = 600, Children = { preview } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);
    }

    [Fact]
    [Upstream("CodePreview", "效果区是一组；源码默认收起，按钮切换并带 aria-expanded / aria-controls")]
    public Task The_preview_is_a_group_and_the_code_toggles() => WpfUi.Run(() =>
    {
        var preview = new CodePreview { Code = "<Button>保存</Button>", Content = new Button { Content = "保存" } };
        Show(preview);

        var group = UIElementAutomationPeer.CreatePeerForElement(preview.PreviewPart);
        Assert.Equal((AutomationControlType.Group, "预览"), (group.GetAutomationControlType(), group.GetName()));
        var toggle = (IExpandCollapseProvider)UIElementAutomationPeer.CreatePeerForElement(preview.Toggle).GetPattern(PatternInterface.ExpandCollapse)!;
        Assert.Equal(("显示代码", ExpandCollapseState.Collapsed), (preview.Toggle.Content, toggle.ExpandCollapseState));
        Assert.Equal("<Button>保存</Button>", preview.CodeBlock.Code);

        ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(preview.Toggle).GetPattern(PatternInterface.Invoke)!).Invoke();
        WpfUi.Flush();
        Assert.True(preview.IsCodeShown);
        Assert.Equal(("隐藏代码", ExpandCollapseState.Expanded), (preview.Toggle.Content, toggle.ExpandCollapseState));
        // 复制按钮在工具栏上，源码里不再重复一个
        Assert.Single(WpfUi.Descendants(preview).OfType<CopyButton>(), button => button.IsVisible);
    });

    [Fact]
    [Upstream("CodePreview", "收起的源码不高亮：指针移到或焦点进入工具栏时才交给高亮器")]
    public Task Collapsed_code_is_highlighted_only_on_intent() => WpfUi.Run(() =>
    {
        var preview = new CodePreview { Code = "var a = 1;", Content = "x" };
        Show(preview);

        Assert.False(preview.CodeBlock.Highlight);
        preview.ToolbarPart.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, 0) { RoutedEvent = Mouse.MouseEnterEvent });
        Assert.True(preview.CodeBlock.Highlight);
    });

    [Fact]
    [Upstream("CodePreview", "按需加载的源码：打开时不加载，工具栏上的意图或展开时才加载一次；复制拿到的是加载的源码")]
    public Task Lazy_code_loads_once_on_intent_or_expand() => WpfUi.Run(() =>
    {
        var loads = 0;
        var preview = new CodePreview { CodeFactory = () => { loads++; return Task.FromResult("var lazy = true;"); }, Content = "x" };
        Show(preview);

        Assert.Equal(0, loads);
        preview.Toggle.Focus(); // 焦点进入工具栏
        preview.IsCodeShown = true;
        WpfUi.Until(() => preview.CodeBlock.Code == "var lazy = true;");
        Assert.Equal(1, loads);
    });

    [Fact]
    [Upstream("CodePreview", "actions 排在工具栏最前面")]
    public Task Actions_come_first_in_the_toolbar() => WpfUi.Run(() =>
    {
        var edit = new Button { Content = "编辑" };
        var preview = new CodePreview { Code = "x", Content = "x", Actions = edit };
        Show(preview);

        var buttons = WpfUi.Descendants(preview.ToolbarPart).OfType<System.Windows.Controls.Primitives.ButtonBase>().ToList();
        Assert.Same(edit, buttons[0]);
        Assert.Same(preview.Toggle, buttons[1]);
    });
}
