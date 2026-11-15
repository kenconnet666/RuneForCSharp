using Avalonia;
using Avalonia.Automation;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.LogicalTree;
using Avalonia.Metadata;

namespace Rune.Avalonia;

/// <summary>
/// 一组代码，用标签页切换（vue-tsx-pro docs-kit 的 CodeGroup）：安装命令（pnpm / npm / yarn）、同一段逻辑的几个文件。由 <see cref="Tabs"/> 与 <see cref="CodeBlock"/> 组合，标签栏就是代码块的标题栏。
/// <code>
/// &lt;rune:CodeGroup Value="{Binding Manager}"&gt;
///   &lt;rune:CodeBlock rune:CodeGroup.Label="pnpm" Language="bash" Code="pnpm add rune" /&gt;
///   &lt;rune:CodeBlock rune:CodeGroup.Label="npm" Language="bash" Code="npm i rune" /&gt;
/// &lt;/rune:CodeGroup&gt;
/// </code>
/// 各段是 CodeBlock（TS 的 items：一段的选项就是 CodeBlock 的属性），标签上的文字是附加属性 <see cref="LabelProperty"/>，这一段的值是 <see cref="ItemValueProperty"/>（默认等于标签）。
/// 代码块去掉自己的外框（Flush）。选中的值 <see cref="Value"/> 默认双向绑定、不写时是第一段，几组代码绑同一个值就一起切换。
/// 外框 borderMuted 描边、md 圆角、surface 底；标签栏 sm 字号、左右 space.sm、surfaceMuted 底，读屏名称默认是各段标签连起来（部件 <see cref="CodeGroupParts.List"/>）。
/// </summary>
public class CodeGroup : Control
{
    /// <summary>选中的那一段的值；不写时是第一段。</summary>
    public static readonly StyledProperty<string?> ValueProperty = AvaloniaProperty.Register<CodeGroup, string?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>一段代码在标签上的文字（TS 的 item.label）。</summary>
    public static readonly AttachedProperty<string?> LabelProperty = AvaloniaProperty.RegisterAttached<CodeGroup, CodeBlock, string?>("Label");

    /// <summary>一段代码的值，默认等于标签（TS 的 item.value）。</summary>
    public static readonly AttachedProperty<string?> ItemValueProperty = AvaloniaProperty.RegisterAttached<CodeGroup, CodeBlock, string?>("ItemValue");

    readonly Border root = new() { ClipToBounds = true };
    readonly Tabs tabs = new() { Size = TextSize.Sm };
    readonly TabList list = new() { Name = CodeGroupParts.List };

    static CodeGroup()
    {
        LabelProperty.Changed.AddClassHandler<CodeBlock>((block, _) => block.FindLogicalAncestorOfType<CodeGroup>()?.Rebuild());
        ItemValueProperty.Changed.AddClassHandler<CodeBlock>((block, _) => block.FindLogicalAncestorOfType<CodeGroup>()?.Rebuild());
    }

    /// <summary>新建：外框里是标签页（标签栏与各段的面板）。</summary>
    public CodeGroup()
    {
        root.BorderThickness = new Thickness(1);
        root.Bind(Border.BorderBrushProperty, root.GetResourceObservable(ThemeKeys.Brush.BorderMuted), BindingPriority.Template);
        root.Bind(Border.BackgroundProperty, root.GetResourceObservable(ThemeKeys.Brush.Surface), BindingPriority.Template);
        list.Bind(TabList.BackgroundProperty, list.GetResourceObservable(ThemeKeys.Brush.SurfaceMuted), BindingPriority.Template);
        tabs.Children.Add(list);
        tabs[!!Tabs.ValueProperty] = this[!!ValueProperty];
        root.Child = tabs;
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        Items.CollectionChanged += (_, _) => Rebuild();
    }

    /// <summary>各段代码（TS 的 items）。</summary>
    [Content]
    public AvaloniaList<CodeBlock> Items { get; } = [];

    /// <summary>选中的那一段的值。</summary>
    public string? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>标签上的文字。</summary>
    /// <param name="block">一段代码。</param>
    public static string? GetLabel(CodeBlock block) => block.GetValue(LabelProperty);

    /// <summary>设标签上的文字。</summary>
    /// <param name="block">一段代码。</param>
    /// <param name="value">文字。</param>
    public static void SetLabel(CodeBlock block, string? value) => block.SetValue(LabelProperty, value);

    /// <summary>一段代码的值。</summary>
    /// <param name="block">一段代码。</param>
    public static string? GetItemValue(CodeBlock block) => block.GetValue(ItemValueProperty);

    /// <summary>设一段代码的值。</summary>
    /// <param name="block">一段代码。</param>
    /// <param name="value">值。</param>
    public static void SetItemValue(CodeBlock block, string? value) => block.SetValue(ItemValueProperty, value);

    static string KeyOf(CodeBlock block) => GetItemValue(block) ?? GetLabel(block) ?? "";

    /// <summary>按各段重建标签与面板：先把代码块从旧面板里取出来，再放进新的。</summary>
    void Rebuild()
    {
        foreach (var panel in tabs.Children.OfType<TabPanel>().ToList())
        {
            panel.Content = null;
            tabs.Children.Remove(panel);
        }
        list.Children.Clear();
        foreach (var block in Items)
        {
            block.Flush = true;
            list.Children.Add(new Tab { Value = KeyOf(block), Content = GetLabel(block) });
            tabs.Children.Add(new TabPanel { Value = KeyOf(block), Content = block });
        }
        // 模板优先级：使用方写的名称（样式或本地值）盖得过
        list.SetValue(AutomationProperties.NameProperty, string.Join(" / ", Items.Select(GetLabel)), BindingPriority.Template);
    }

    /// <summary>随字号的尺寸：外框 md 圆角，标签栏左右 space.sm（标签栏是 sm 字号，按它算）。</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        root.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Md) * TextElement.GetFontSize(this));
        list.Padding = new Thickness(this.Resource<double>(ThemeKeys.Space.Sm) * TextElement.GetFontSize(list), 0);
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

/// <summary>CodeGroup 的部件名。</summary>
public static class CodeGroupParts
{
    /// <summary>标签栏（TS 的 propsList）：TabList。</summary>
    public const string List = "PART_List";
}
