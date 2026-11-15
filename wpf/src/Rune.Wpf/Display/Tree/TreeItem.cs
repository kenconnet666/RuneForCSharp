using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace Rune.Wpf;

/// <summary>
/// 树的一项（<see cref="Tree"/> 按展开成一列的行建的，虚拟滚动时复用，不单独用）：缩进、展开箭头（加载子节点时是加载指示）、勾选框、图标、文字。
/// 外观同列表行：悬停、键盘高亮 fillHover；选中的是当前项的样子（accentSoft 底、accentText 字，悬停深一档）；不可用变淡；
/// 拖动时被拖的那一项变淡，放进某一项里时它带 accent 的框与 accentSoft 底，放在前后时画一条 accent 的线。不可聚焦：焦点留在树上。
/// </summary>
public sealed class TreeItem : FrameworkElement
{
    readonly Tree owner;
    readonly Border root = new();
    readonly StackPanel layout = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    readonly FrameworkElement indent = new();
    readonly System.Windows.Controls.Grid toggle = new() { Background = Brushes.Transparent };
    readonly Icon chevron = new() { Data = Lucide.ChevronRight, RenderTransformOrigin = new Point(0.5, 0.5), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly Spinner spinner = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly Border checkbox = new() { BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Center };
    readonly Icon mark = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly Icon icon = new() { VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock label = new() { TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    readonly ContentPresenter custom = new() { VerticalAlignment = VerticalAlignment.Center };
    TreeItemState state;

    internal TreeItem(Tree owner)
    {
        this.owner = owner;
        toggle.Children.Add(chevron);
        toggle.Children.Add(spinner);
        checkbox.Child = mark;
        foreach (var child in new UIElement[] { indent, toggle, checkbox, icon, label, custom }) layout.Children.Add(child);
        root.Child = layout;
        AddVisualChild(root);
        AddLogicalChild(root);
        Cursor = Cursors.Hand;
        MouseEnter += (_, _) => Paint();
        MouseLeave += (_, _) => Paint();
    }

    /// <summary>这一行（虚拟滚动复用时换）。</summary>
    internal FlatTreeNode<object>? Row
    {
        get;
        set
        {
            field = value;
            Sync();
        }
    }

    /// <summary>这一行的状态（测试用）。</summary>
    internal TreeItemState State => state;

    /// <summary>勾选框（测试用）。</summary>
    internal FrameworkElement CheckboxPart => checkbox;

    /// <summary>展开箭头（测试用）。</summary>
    internal FrameworkElement TogglePart => toggle;

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => root;

    /// <inheritdoc />
    protected override System.Collections.IEnumerator LogicalChildren => new object[] { root }.GetEnumerator();

    /// <summary>按行与状态更新：内容、外观、读屏的名称与说明；尺寸按 em。</summary>
    internal void Sync()
    {
        if (Row is not { } row) return;
        state = owner.StateOf(row);
        var (text, data, template) = owner.ContentOf(row, state.Expanded);
        var em = owner.Em;
        double Space(string key) => this.Resource<double>(key) * em;
        indent.Width = row.Depth * owner.Indent * em;
        indent.Visibility = row.Depth > 0 ? Visibility.Visible : Visibility.Collapsed;
        toggle.Width = toggle.Height = 1.5 * em;
        chevron.Visibility = row.Expandable && !state.Loading ? Visibility.Visible : Visibility.Hidden;
        chevron.RenderTransform = state.Expanded ? new RotateTransform(90) : null;
        chevron.SetResourceReference(Control.ForegroundProperty, ThemeKeys.Brush.TextMuted);
        spinner.Visibility = state.Loading ? Visibility.Visible : Visibility.Collapsed;
        // 勾选框画成 Checkbox 的样子：选中、半选时 accent 实底、accentContrast 的对勾或横线
        checkbox.Visibility = owner.Checkable ? Visibility.Visible : Visibility.Collapsed;
        checkbox.Width = checkbox.Height = em;
        checkbox.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Sm) * em);
        var marked = state.Checked != CheckState.Unchecked;
        checkbox.SetResourceReference(Border.BackgroundProperty, marked ? ThemeKeys.Brush.Accent : ThemeKeys.Brush.Surface);
        checkbox.SetResourceReference(Border.BorderBrushProperty, marked ? ThemeKeys.Brush.Accent : ThemeKeys.Brush.BorderStrong);
        mark.Data = state.Checked == CheckState.Indeterminate ? Lucide.Minus : Lucide.Check;
        mark.Visibility = marked ? Visibility.Visible : Visibility.Hidden;
        mark.StrokeWidth = 3;
        mark.Width = mark.Height = 0.875 * em;
        mark.SetResourceReference(Control.ForegroundProperty, ThemeKeys.Brush.AccentContrast);
        icon.Data = data;
        icon.Visibility = data is null ? Visibility.Collapsed : Visibility.Visible;
        label.Text = text;
        label.Visibility = template is null ? Visibility.Visible : Visibility.Collapsed;
        label.FontWeight = state.Matched ? FontWeights.SemiBold : FontWeights.Normal;
        custom.Visibility = template is null ? Visibility.Collapsed : Visibility.Visible;
        custom.ContentTemplate = template;
        custom.Content = template is null ? null : row.Node;
        // StackPanel 没有间距：每个元素右边留 xs；勾选框自己还有 xs 的外边距（同上游）
        foreach (FrameworkElement child in layout.Children) child.Margin = new Thickness(0, 0, (child == checkbox ? 2 : 1) * Space(ThemeKeys.Space.Xs), 0);
        root.Padding = new Thickness(Space(ThemeKeys.Space.Xs), Space(ThemeKeys.Space.Xs), Space(ThemeKeys.Space.Sm), Space(ThemeKeys.Space.Xs));
        root.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Sm) * em);
        root.MinHeight = Math.Max(ControlHeight.Of(em, ControlHeight.DefaultFactor), 24);
        // 行与行之间上下各 1 像素
        Margin = new Thickness(0, 1, 0, 1);
        Paint();
        IsEnabled = !state.Disabled;
        AutomationProperties.SetName(this, text);
        AutomationProperties.SetHelpText(this, $"{row.Depth + 1} 级，第 {row.Position} 项，共 {row.SetSize} 项");
        InvalidateVisual();
    }

    /// <summary>底色、文字色、透明度、拖动的落点框。</summary>
    void Paint()
    {
        if (Row is not { } row) return;
        var current = state.Selected;
        var highlighted = owner.IsHighlighted(row.Key) || (IsMouseOver && !state.Disabled);
        var background = state.Drop == TreeDropZone.Inside ? ThemeKeys.Brush.AccentSoft
            : current ? (IsMouseOver ? ThemeKeys.Brush.AccentSoftHover : ThemeKeys.Brush.AccentSoft)
            : highlighted ? ThemeKeys.Brush.FillHover : null;
        if (background is null) root.Background = Brushes.Transparent;
        else root.SetResourceReference(Border.BackgroundProperty, background);
        root.SetResourceReference(Border.BorderBrushProperty, ThemeKeys.Brush.Accent);
        root.BorderThickness = new Thickness(state.Drop == TreeDropZone.Inside ? 2 : 0);
        root.SetResourceReference(TextElement.ForegroundProperty, current ? ThemeKeys.Brush.AccentText : ThemeKeys.Brush.Text);
        Opacity = state.Disabled ? this.Resource<double>(ThemeKeys.Opacity.Disabled) : state.Dragging ? 0.5 : 1;
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

    /// <summary>放在前后时画一条 accent 的线：从这一层的缩进处开始，压在上边或下边。</summary>
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        if (state.Drop is not ({ } zone and not TreeDropZone.Inside) || Row is not { } row) return;
        var em = owner.Em;
        var start = (row.Depth * owner.Indent + 1.75) * em;
        var y = zone == TreeDropZone.Before ? 0 : ActualHeight - 2;
        context.DrawRoundedRectangle(this.Resource<Brush>(ThemeKeys.Brush.Accent), null, new Rect(start, y, Math.Max(0, ActualWidth - start), 2), 1, 1);
    }

    /// <summary>按下：焦点留在树上（不在这一行上），记下拖动的起点（触笔、触摸时不拖）。</summary>
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (Row is not { } row) return;
        if (e.StylusDevice is null) owner.Press(row.Key, e.GetPosition(owner));
        e.Handled = true;
    }

    /// <summary>松开在这一行上（没拖）：点了箭头只展开收起，点了勾选框只勾选，点了别处按树的规则。</summary>
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (Row is not { } row) return;
        var source = e.OriginalSource as DependencyObject;
        bool Within(DependencyObject part)
        {
            for (var node = source; node is not null && node != this; node = VisualTreeHelper.GetParent(node))
                if (node == part) return true;
            return false;
        }
        if (row.Expandable && Within(toggle)) owner.ClickToggle(row.Key);
        else if (owner.Checkable && Within(checkbox)) owner.ClickCheck(row.Key);
        else owner.Click(row.Key);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TreeItemPeer(this);

    /// <summary>读屏：树节点；可展开的带展开 / 收起，能选时带选中，能勾时带勾选（三态）。</summary>
    sealed class TreeItemPeer(TreeItem owner) : FrameworkElementAutomationPeer(owner), IExpandCollapseProvider, ISelectionItemProvider, IToggleProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TreeItem;

        protected override string GetClassNameCore() => nameof(TreeItem);

        public override object? GetPattern(PatternInterface patternInterface) => patternInterface switch
        {
            PatternInterface.ExpandCollapse or PatternInterface.SelectionItem => this,
            PatternInterface.Toggle when owner.owner.Checkable => this,
            _ => base.GetPattern(patternInterface),
        };

        public ExpandCollapseState ExpandCollapseState => owner.Row is not { Expandable: true } ? ExpandCollapseState.LeafNode
            : owner.state.Expanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public void Expand()
        {
            if (owner.Row is { Expandable: true } row && !owner.state.Expanded) owner.owner.ClickToggle(row.Key);
        }

        public void Collapse()
        {
            if (owner.Row is { Expandable: true } row && owner.state.Expanded) owner.owner.ClickToggle(row.Key);
        }

        public bool IsSelected => owner.state.Selected;

        public IRawElementProviderSimple? SelectionContainer => null;

        public void Select()
        {
            if (owner.Row is { } row && !owner.state.Selected) owner.owner.Click(row.Key);
        }

        public void AddToSelection() => Select();

        public void RemoveFromSelection()
        {
            if (owner.Row is { } row && owner.state.Selected) owner.owner.Click(row.Key);
        }

        public ToggleState ToggleState => owner.state.Checked switch
        {
            CheckState.Checked => ToggleState.On,
            CheckState.Indeterminate => ToggleState.Indeterminate,
            _ => ToggleState.Off,
        };

        public void Toggle()
        {
            if (owner.Row is { } row) owner.owner.ClickCheck(row.Key);
        }
    }
}
