using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Rune.Avalonia;

/// <summary>
/// 树的一项（<see cref="Tree"/> 按展开成一列的行建的，虚拟滚动时复用，不单独用）：缩进、展开箭头（加载子节点时是加载指示）、勾选框、图标、文字。
/// 外观同列表行：悬停、键盘高亮 fillHover；选中的是当前项的样子（accentSoft 底、accentText 字，悬停深一档）；不可用变淡；
/// 拖动时被拖的那一项变淡，放进某一项里时它带 accent 的框与 accentSoft 底，放在前后时画一条 accent 的线。不可聚焦：焦点留在树上。
/// </summary>
public sealed class TreeItem : Control
{
    readonly Tree owner;
    readonly Border root = new() { Name = TreeParts.Item };
    readonly StackPanel layout = new() { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
    readonly Border indent = new();
    readonly Panel toggle = new() { Name = TreeParts.Toggle, Background = Brushes.Transparent };
    readonly Icon chevron = new() { Data = Lucide.ChevronRight, RenderTransformOrigin = RelativePoint.Center, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly Spinner spinner = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly Border checkbox = new() { Name = TreeParts.Checkbox };
    readonly Icon mark = new();
    readonly Icon icon = new();
    readonly TextBlock label = new() { Name = TreeParts.Label, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    readonly ContentPresenter custom = new() { VerticalAlignment = VerticalAlignment.Center };
    TreeItemState state;

    internal TreeItem(Tree owner)
    {
        this.owner = owner;
        toggle.Children.AddRange([chevron, spinner]);
        checkbox.Child = mark;
        layout.Children.AddRange([indent, toggle, checkbox, icon, label, custom]);
        root.Child = layout;
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    /// <summary>这一行。</summary>
    internal FlatTreeNode<object>? Row => DataContext as FlatTreeNode<object>;

    /// <summary>这一行的状态（测试用）。</summary>
    internal TreeItemState State => state;

    /// <summary>勾选框（测试用）。</summary>
    internal Control CheckboxPart => checkbox;

    /// <summary>展开箭头（测试用）。</summary>
    internal Control TogglePart => toggle;

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Sync();
    }

    /// <summary>按行与状态更新：内容、外观、读屏的名称与说明；尺寸按 em。</summary>
    internal void Sync()
    {
        if (Row is not { } row) return;
        state = owner.StateOf(row);
        var (text, data, template) = owner.ContentOf(row, state.Expanded);
        var em = owner.FontSize;
        double Space(string key) => this.Resource<double>(key) * em;
        indent.Width = row.Depth * owner.Indent * em;
        indent.IsVisible = row.Depth > 0;
        toggle.Width = toggle.Height = 1.5 * em;
        chevron.IsVisible = row.Expandable && !state.Loading;
        chevron.RenderTransform = state.Expanded ? new RotateTransform(FlowDirection == FlowDirection.RightToLeft ? -90 : 90) : null;
        chevron.Foreground = this.Resource<IBrush>(ThemeKeys.Brush.TextMuted);
        spinner.IsVisible = state.Loading;
        // 勾选框画成 Checkbox 的样子：选中、半选时 accent 实底、accentContrast 的对勾或横线
        checkbox.IsVisible = owner.Checkable;
        checkbox.Width = checkbox.Height = 1.125 * em;
        checkbox.BorderThickness = new Thickness(this.Resource<double>(ThemeKeys.BorderWidth.Sm));
        checkbox.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Sm) * em);
        checkbox.Margin = new Thickness(0, 0, Space(ThemeKeys.Space.Xs), 0);
        var marked = state.Checked != CheckState.Unchecked;
        checkbox.Background = this.Resource<IBrush>(marked ? ThemeKeys.Brush.Accent : ThemeKeys.Brush.Surface);
        checkbox.BorderBrush = this.Resource<IBrush>(marked ? ThemeKeys.Brush.Accent : ThemeKeys.Brush.BorderStrong);
        mark.Data = state.Checked == CheckState.Indeterminate ? Lucide.Minus : Lucide.Check;
        mark.IsVisible = marked;
        mark.StrokeWidth = 3;
        mark.Width = mark.Height = 0.875 * em;
        mark.Foreground = this.Resource<IBrush>(ThemeKeys.Brush.AccentContrast);
        icon.Data = data;
        icon.IsVisible = data is not null;
        label.Text = text;
        label.IsVisible = template is null;
        label.FontWeight = state.Matched ? this.Resource<FontWeight>(ThemeKeys.FontWeight.Semibold) : FontWeight.Normal;
        label.LineHeight = this.Resource<double>(ThemeKeys.LineHeight.Normal) * em;
        custom.IsVisible = template is not null;
        custom.ContentTemplate = template;
        custom.Content = template is null ? null : row.Node;
        layout.Spacing = Space(ThemeKeys.Space.Xs);
        root.Padding = new Thickness(Space(ThemeKeys.Space.Xs), Space(ThemeKeys.Space.Xs), Space(ThemeKeys.Space.Sm), Space(ThemeKeys.Space.Xs));
        root.CornerRadius = new CornerRadius(this.Resource<double>(ThemeKeys.Radius.Sm) * em);
        // 树比列表行紧凑：只保底 24px 的点击区域（TS 的 treeStyles.item 盖掉了列表行的控件高度）
        root.MinHeight = this.Resource<double>(ThemeKeys.Size.MinTarget);
        // 行与行之间上下各 1 像素
        Margin = new Thickness(0, 1);
        Paint();
        IsEnabled = !state.Disabled;
        AutomationProperties.SetName(this, text);
        AutomationProperties.SetHelpText(this, $"{row.Depth + 1} 级，第 {row.Position} 项，共 {row.SetSize} 项");
        InvalidateVisual();
    }

    /// <summary>底色、文字色、透明度、拖动的落点框。</summary>
    void Paint()
    {
        var current = state.Selected;
        var highlighted = owner.IsHighlighted(Row?.Key ?? "") || (IsPointerOver && !state.Disabled);
        string? background = state.Drop == TreeDropZone.Inside ? ThemeKeys.Brush.AccentSoft
            : current ? (IsPointerOver ? ThemeKeys.Brush.AccentSoftHover : ThemeKeys.Brush.AccentSoft)
            : highlighted ? ThemeKeys.Brush.FillHover : null;
        root.Background = background is null ? Brushes.Transparent : this.Resource<IBrush>(background);
        root.BorderBrush = state.Drop == TreeDropZone.Inside ? this.Resource<IBrush>(ThemeKeys.Brush.Accent) : null;
        root.BorderThickness = new Thickness(state.Drop == TreeDropZone.Inside ? 2 : 0);
        root.SetValue(global::Avalonia.Controls.Documents.TextElement.ForegroundProperty, this.Resource<IBrush>(current ? ThemeKeys.Brush.AccentText : ThemeKeys.Brush.Text));
        Opacity = state.Disabled ? this.Resource<double>(ThemeKeys.Opacity.Disabled) : state.Dragging ? 0.5 : 1;
    }

    /// <summary>放在前后时画一条 accent 的线：从这一层的缩进处开始，压在上边或下边。</summary>
    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (state.Drop is not ({ } zone and not TreeDropZone.Inside) || Row is not { } row) return;
        var em = owner.FontSize;
        var start = (row.Depth * owner.Indent + 1.75) * em;
        var y = zone == TreeDropZone.Before ? 0 : Bounds.Height - 2;
        context.DrawRectangle(this.Resource<IBrush>(ThemeKeys.Brush.Accent), null, new RoundedRect(new Rect(start, y, Math.Max(0, Bounds.Width - start), 2), 1));
    }

    /// <inheritdoc />
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        Paint();
    }

    /// <inheritdoc />
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        Paint();
    }

    /// <summary>按下：焦点留在树上（不在这一行上），记下拖动的起点。</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Row is not { } row || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        // 触摸时照常滚动，不拖
        if (e.Pointer.Type != PointerType.Touch) owner.Press(row.Key, e.GetPosition(owner));
        e.Handled = true;
    }

    /// <summary>松开在这一行上（没拖）：点了箭头只展开收起，点了勾选框只勾选，点了别处按树的规则。</summary>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (Row is not { } row || e.InitialPressMouseButton != MouseButton.Left || !new Rect(Bounds.Size).Contains(e.GetPosition(this))) return;
        var target = e.Source as Visual;
        bool Within(Control part) => target is not null && (target == part || part.IsVisualAncestorOf(target));
        if (row.Expandable && Within(toggle)) owner.ClickToggle(row.Key);
        else if (owner.Checkable && Within(checkbox)) owner.ClickCheck(row.Key);
        else owner.Click(row.Key);
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new TreeItemPeer(this);

    /// <summary>读屏：树节点；可展开的带展开 / 收起，能选时带选中，能勾时带勾选（三态）。</summary>
    sealed class TreeItemPeer(TreeItem owner) : ControlAutomationPeer(owner), IExpandCollapseProvider, ISelectionItemProvider, IToggleProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TreeItem;

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        public ExpandCollapseState ExpandCollapseState => owner.Row is not { Expandable: true } ? ExpandCollapseState.LeafNode
            : owner.state.Expanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

        public bool ShowsMenu => false;

        public void Expand()
        {
            if (owner.Row is { Expandable: true } row && !owner.state.Expanded) owner.owner.ClickToggle(row.Key);
        }

        public void Collapse()
        {
            if (owner.Row is { Expandable: true } row && owner.state.Expanded) owner.owner.ClickToggle(row.Key);
        }

        public bool IsSelected => owner.state.Selected;

        public ISelectionProvider? SelectionContainer => null;

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
