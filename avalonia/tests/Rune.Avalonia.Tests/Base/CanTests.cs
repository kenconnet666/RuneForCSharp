using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>Can、Provider.Can：有权限才显示；几个权限点任一满足即可；没接入权限判断时一律放行；权限变了界面跟着变。</summary>
public class CanTests
{
    static IEnumerable<string> Buttons(Control root) => root.GetVisualDescendants().OfType<Button>().Select(button => button.Content as string ?? "");

    static Window Show(Control content)
    {
        var window = HeadlessUi.Show(content);
        HeadlessUi.Flush(window);
        return window;
    }

    [Fact]
    [Upstream("Can", "有权限渲染内容，没有时渲染 fallback")]
    public Task Shows_content_with_permission_and_fallback_without() => HeadlessUi.Run(() =>
    {
        var provider = new Provider
        {
            Permissions = permission => permission == "cost:add",
            Child = new StackPanel
            {
                Children =
                {
                    new Can { Permission = "cost:add", Content = new Button { Content = "新建" } },
                    new Can { Permission = "cost:remove", Content = new Button { Content = "删除" }, Fallback = "只读" },
                    new Can { Permission = "cost:remove", Content = new Button { Content = "也删除" } },
                },
            },
        };
        Show(provider);
        Assert.Equal(["新建"], Buttons(provider));
        Assert.Contains(provider.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "只读");
    });

    [Fact]
    [Upstream("Can", "给数组时有其中任意一个就算有")]
    public Task Any_of_several_permissions_is_enough() => HeadlessUi.Run(() =>
    {
        var provider = new Provider
        {
            Permissions = permission => permission == "cost:edit",
            Child = new StackPanel
            {
                Children =
                {
                    new Can { Permission = "cost:add, cost:edit", Content = new Button { Content = "编辑" } },
                    new Can { Permission = "cost:add,cost:remove", Content = new Button { Content = "不该出现" } },
                },
            },
        };
        Show(provider);
        Assert.Equal(["编辑"], Buttons(provider));
        Assert.True(Provider.Can(provider.Child!, "cost:add", "cost:edit"));
        Assert.False(Provider.Can(provider.Child!, "cost:add"));
    });

    [Fact]
    [Upstream("Can", "没接入权限判断时一律放行")]
    public Task Everything_is_allowed_without_permissions() => HeadlessUi.Run(() =>
    {
        var can = new Can { Permission = "anything", Content = new Button { Content = "放行" } };
        Show(can);
        Assert.Equal(["放行"], Buttons(can));
        Assert.True(Provider.Can(can, "anything"));
    });

    [Fact]
    [Upstream("Can", "权限变了界面跟着变")]
    public Task Follows_a_new_permission_function() => HeadlessUi.Run(() =>
    {
        string[] granted = ["cost:add"];
        var provider = new Provider { Permissions = permission => granted.Contains(permission), Child = new Can { Permission = "cost:remove", Content = new Button { Content = "删除" } } };
        var window = Show(provider);
        Assert.Empty(Buttons(provider));
        // 换角色后换一个新的判断函数：里面的 Can 跟着变
        granted = ["cost:add", "cost:remove"];
        provider.Permissions = permission => granted.Contains(permission);
        HeadlessUi.Flush(window);
        Assert.Equal(["删除"], Buttons(provider));
    });
}
