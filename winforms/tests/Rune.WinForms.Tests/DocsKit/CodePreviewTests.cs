namespace Rune.WinForms.Tests;

/// <summary>CodePreviewControl：效果区是一组；源码默认收起，按钮切换；收起时不高亮，工具栏上的意图或展开时才取源码、只取一次；actions 在工具栏最前面。</summary>
public class CodePreviewTests
{
    [Fact]
    [Upstream("CodePreview", "效果区是一组；源码默认收起，按钮切换并带 aria-expanded / aria-controls")]
    public Task The_preview_is_a_group_and_the_code_toggles() => WinFormsUi.Run(() =>
    {
        var preview = new CodePreviewControl { Code = "<Button>保存</Button>", Content = new ButtonControl { Text = "保存" }, Dock = DockStyle.Top };
        using var form = WinFormsUi.Show(preview);

        Assert.Equal((AccessibleRole.Grouping, "预览"), (preview.Preview.AccessibilityObject.Role, preview.Preview.AccessibilityObject.Name));
        Assert.Equal("显示代码", preview.Toggle.Text);
        Assert.True(preview.Toggle.AccessibilityObject.State.HasFlag(AccessibleStates.Collapsed));
        Assert.Equal("<Button>保存</Button>", preview.CodeBlock.Code);

        preview.Toggle.PerformClick();
        Assert.True(preview.IsCodeShown);
        Assert.Equal("隐藏代码", preview.Toggle.Text);
        Assert.True(preview.Toggle.AccessibilityObject.State.HasFlag(AccessibleStates.Expanded));
        Assert.False(preview.CodeBlock.Copyable); // 复制按钮在工具栏上，源码里不再重复一个
    });

    [Fact]
    [Upstream("CodePreview", "收起的源码不高亮：指针移到或焦点进入工具栏时才交给高亮器")]
    public Task Collapsed_code_is_highlighted_only_on_intent() => WinFormsUi.Run(() =>
    {
        var preview = new CodePreviewControl { Code = "var a = 1;", Content = new TextControl { Text = "x" }, Dock = DockStyle.Top };
        // 窗体显示时焦点给第一个能聚焦的控件：先放一个输入框接住，不然一显示焦点就进了工具栏
        using var form = WinFormsUi.Show(new TextBox(), preview);

        Assert.False(preview.CodeBlock.Highlight);
        preview.Toggle.Focus(); // 焦点进入工具栏
        WinFormsUi.Pump();
        Assert.True(preview.CodeBlock.Highlight);
    });

    [Fact]
    [Upstream("CodePreview", "按需加载的源码：打开时不加载，工具栏上的意图或展开时才加载一次；复制拿到的是加载的源码")]
    public Task Lazy_code_loads_once_on_intent_or_expand() => WinFormsUi.Run(() =>
    {
        var loads = 0;
        var preview = new CodePreviewControl { CodeFactory = () => { loads++; return Task.FromResult("var lazy = true;"); }, Content = new TextControl { Text = "x" }, Dock = DockStyle.Top };
        using var form = WinFormsUi.Show(new TextBox(), preview);

        Assert.Equal(0, loads);
        preview.Toggle.Focus();
        preview.IsCodeShown = true;
        WinFormsUi.Until(() => preview.CodeBlock.Code == "var lazy = true;");
        Assert.Equal(1, loads);
    });

    [Fact]
    [Upstream("CodePreview", "actions 排在工具栏最前面")]
    public Task Actions_come_first_in_the_toolbar() => WinFormsUi.Run(() =>
    {
        var edit = new ButtonControl { Text = "编辑" };
        var preview = new CodePreviewControl { Code = "x", Content = new TextControl { Text = "x" }, Actions = edit, Dock = DockStyle.Top };
        using var form = WinFormsUi.Show(preview);

        Assert.Same(edit, preview.Toolbar.Controls[0]);
        Assert.Same(preview.Toggle, preview.Toolbar.Controls[1]);
    });
}
