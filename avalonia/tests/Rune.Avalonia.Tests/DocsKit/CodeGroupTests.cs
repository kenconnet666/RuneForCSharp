using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>CodeGroup：标签页切换几段代码；选中的值双向绑定，几组绑同一个值一起切换。</summary>
public class CodeGroupTests
{
    static CodeGroup Build()
    {
        var group = new CodeGroup();
        foreach (var (label, code) in new[] { ("pnpm", "pnpm add x"), ("npm", "npm i x") })
        {
            var block = new CodeBlock { Code = code };
            CodeGroup.SetLabel(block, label);
            group.Items.Add(block);
        }
        return group;
    }

    static string?[] VisibleCode(CodeGroup group) =>
        [.. group.GetVisualDescendants().OfType<TabPanel>().Where(panel => panel.IsVisible).Select(panel => ((CodeBlock)panel.Content!).Code)];

    static void Click(Window window, Control control)
    {
        var at = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(at, MouseButton.Left);
        window.MouseUp(at, MouseButton.Left);
    }

    [Fact]
    [Upstream("CodeGroup", "默认显示第一段，点标签切换，选中的值写回")]
    public Task First_by_default_and_clicking_switches_and_writes_back() => HeadlessUi.Run(() =>
    {
        var group = Build();
        var window = HeadlessUi.Show(new StackPanel { Width = 500, Children = { group } });

        Assert.Equal(["pnpm add x"], VisibleCode(group));
        var list = group.GetVisualDescendants().OfType<TabList>().Single();
        Assert.Equal(CodeGroupParts.List, list.Name);
        Assert.Equal("pnpm / npm", AutomationProperties.GetName(list));
        Assert.All(group.Items, block => Assert.True(block.Flush));
        Click(window, list.GetVisualDescendants().OfType<Tab>().ElementAt(1));
        Assert.Equal("npm", group.Value);
        Assert.Equal(["npm i x"], VisibleCode(group));
    });

    [Fact]
    public Task Groups_bound_together_switch_together_and_values_override_labels() => HeadlessUi.Run(() =>
    {
        var (first, second) = (Build(), Build());
        second[!!CodeGroup.ValueProperty] = first[!!CodeGroup.ValueProperty];
        HeadlessUi.Show(new StackPanel { Width = 500, Children = { first, second } });

        first.Value = "npm";
        Assert.Equal(["npm i x"], VisibleCode(second));

        CodeGroup.SetItemValue(second.Items[0], "p");
        Assert.Equal(["p", "npm"], second.GetVisualDescendants().OfType<Tab>().Select(tab => tab.Value));
        second.Items.RemoveAt(1);
        Assert.Single(second.GetVisualDescendants().OfType<TabPanel>());
    });
}
