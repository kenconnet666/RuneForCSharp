using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace Rune.Avalonia;

/// <summary>组的满足方式。</summary>
public enum ConditionMode
{
    /// <summary>全部满足：<see cref="ConditionAll"/>。</summary>
    All,

    /// <summary>任一满足：<see cref="ConditionAny"/>。</summary>
    Any,

    /// <summary>都不满足：Not(Any)。</summary>
    None,

    /// <summary>不全满足：Not(All)。</summary>
    NotAll,
}

/// <summary>
/// 条件构造器（vue-tsx-pro 的 ConditionBuilder）：编辑全部、任一、取反加比较的条件树（<see cref="Condition"/>）。每条是"字段 · 比较方式 · 值"，
/// 比较方式随字段类型、值的输入随字段类型与比较方式；组的表头选满足方式（全部、任一、都不、不全），组里还能再加组。
/// <code>
/// &lt;rune:ConditionBuilder Fields="{Binding Fields}" Value="{Binding Condition}" AutomationProperties.Name="审批条件" /&gt;
/// </code>
/// <list type="bullet">
/// <item>写回的 Value 只含填完整的比较：没选字段、没填值的那几条留在界面上，不进 Value；一条也没有时是 null。父级改了 Value（与上次写回的不同）时整棵重读。</item>
/// <item>根总是一个组（全部、任一；都不满足是 Not(Any)，不全满足是 Not(All)）；传进来的单条比较、嵌套的取反按同样的规则读成组。</item>
/// <item>加了一条之后焦点落到它的字段上；删掉之后落到组的"添加条件"上。IsEnabled 为 false 时整个不可用；所在的 Field、Form 只读时不能增删条件。</item>
/// </list>
/// 部件：<see cref="ConditionBuilderParts"/>。
/// </summary>
public class ConditionBuilder : Control
{
    /// <summary>能参与比较的字段。</summary>
    public static readonly StyledProperty<IReadOnlyList<ConditionField>?> FieldsProperty = AvaloniaProperty.Register<ConditionBuilder, IReadOnlyList<ConditionField>?>(nameof(Fields));

    /// <summary>条件（双向绑定）：没有填完整的比较时是 null。</summary>
    public static readonly StyledProperty<Condition?> ValueProperty = AvaloniaProperty.Register<ConditionBuilder, Condition?>(nameof(Value), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>组最多嵌套几层，默认 2（根组里还能加一层组）；1 时不能加组。</summary>
    public static readonly StyledProperty<int> MaxDepthProperty = AvaloniaProperty.Register<ConditionBuilder, int>(nameof(MaxDepth), 2);

    /// <summary>控件与按钮的字号档位，不写时继承。</summary>
    public static readonly StyledProperty<TextSize> SizeProperty = AvaloniaProperty.Register<ConditionBuilder, TextSize>(nameof(Size));

    readonly Panel root = new();
    GroupNode tree = new(ConditionMode.All);
    // 上次写回的值（JSON）：父级改成别的才整棵重读，自己写回的不重读（没填完的那几条还要留在界面上）
    string emitted = "null";

    /// <summary>新建。</summary>
    public ConditionBuilder()
    {
        LogicalChildren.Add(root);
        VisualChildren.Add(root);
    }

    /// <summary>能参与比较的字段。</summary>
    public IReadOnlyList<ConditionField>? Fields { get => GetValue(FieldsProperty); set => SetValue(FieldsProperty, value); }

    /// <summary>条件。</summary>
    public Condition? Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }

    /// <summary>组最多嵌套几层。</summary>
    public int MaxDepth { get => GetValue(MaxDepthProperty); set => SetValue(MaxDepthProperty, value); }

    /// <summary>字号档位。</summary>
    public TextSize Size { get => GetValue(SizeProperty); set => SetValue(SizeProperty, value); }

    Lang Lang => Provider.GetLang(this);

    IReadOnlyList<ConditionField> FieldList => Fields ?? [];

    /// <summary>所在的 Field、Form 只读：里面的控件各自跟着，增删条件、分组的按钮由这里管（不可用由平台的 IsEnabled 继承）。</summary>
    bool Locked => Field.ReadOnlyOf(this);

    ConditionField? FieldOf(string? name) => FieldList.FirstOrDefault(field => field.Name == name);

    /// <inheritdoc />
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ValueProperty)
        {
            var key = Value?.ToJson() ?? "null";
            if (key == emitted) return;
            emitted = key;
            tree = Value is { } condition ? ToGroup(condition) : new GroupNode(ConditionMode.All);
            Render();
        }
        else if (change.Property == FieldsProperty || change.Property == SizeProperty || change.Property == MaxDepthProperty || change.Property == Provider.LangProperty
                 || change.Property == Field.IsReadOnlyProperty)
        {
            // 字段、字号、语言、只读变了：每条的控件都要重建
            Forget(tree);
            Render();
        }
    }

    /// <inheritdoc />
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Render();
    }

    static void Forget(GroupNode group)
    {
        group.ModeSelect = null;
        foreach (var child in group.Children)
            if (child is RuleNode rule) rule.Editor = null;
            else Forget((GroupNode)child);
    }

    /// <summary>界面上的树写回 Value。</summary>
    void Commit()
    {
        var next = ToCondition(tree);
        emitted = next?.ToJson() ?? "null";
        SetCurrentValue(ValueProperty, next);
    }

    double Em => TextElement.GetFontSize(this);

    double Token(string key) => this.Resource<double>(key) * Em;

    /// <summary>整棵重排：每条的控件（与写了一半的值）、组的满足方式下拉是缓存的，搬到新的容器里。</summary>
    void Render(Control? focus = null)
    {
        root.Children.Clear();
        root.Children.Add(Group(tree, 0, null));
        if (focus is not null) Dispatcher.UIThread.Post(() => focus.Focus(), DispatcherPriority.Loaded);
    }

    Control Group(GroupNode node, int depth, GroupNode? parent)
    {
        var lang = Lang;
        var nested = depth > 0;
        var group = new Flex { Name = ConditionBuilderParts.Group, Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch };
        if (node.Children.Count > 0 || nested)
        {
            var header = new Flex { Name = ConditionBuilderParts.Header, Align = FlexAlign.Center, Gap = Space.Sm };
            header.Children.Add(Adopt(node.ModeSelect ??= ModeSelect(node)));
            if (nested && parent is not null)
            {
                var removeGroup = new IconButton { Name = ConditionBuilderParts.RemoveGroup, Icon = Lucide.Trash, Variant = ButtonVariant.Ghost, Size = Size, IsEnabled = !Locked };
                AutomationProperties.SetName(removeGroup, lang.ConditionRemoveGroup);
                removeGroup.Click += (_, _) => Remove(parent, node);
                header.Children.Add(new Panel { [Flex.GrowProperty] = true });
                header.Children.Add(removeGroup);
            }
            group.Children.Add(header);
        }
        if (node.Children.Count == 0)
        {
            if (!nested) group.Children.Add(new Small { Name = ConditionBuilderParts.Empty, Tone = TextTone.Muted, Text = lang.ConditionEmpty });
        }
        else
        {
            var joiner = node.Mode is ConditionMode.Any or ConditionMode.None ? lang.ConditionOr : lang.ConditionAnd;
            var rows = new Flex { Name = ConditionBuilderParts.Rows, Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch };
            for (var index = 0; index < node.Children.Count; index++)
            {
                var row = new Flex { Name = ConditionBuilderParts.Row, Align = FlexAlign.Start, Gap = Space.Sm };
                // 连接词（且、或）一列，宽度固定：各条的字段对齐；与控件同高，对着这一条的第一行
                row.Children.Add(new Text
                {
                    Name = ConditionBuilderParts.Joiner, Text = index > 0 ? joiner : "", Tone = TextTone.Muted, Width = 2 * Em, TextAlignment = TextAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center, Height = ControlHeight, LineHeight = ControlHeight,
                    [AutomationProperties.AccessibilityViewProperty] = AccessibilityView.Raw,
                });
                Control content;
                if (node.Children[index] is RuleNode rule)
                {
                    content = Adopt((rule.Editor ??= new RuleEditor(this, node, rule)).Layout);
                    AutomationProperties.SetName(content, lang.ConditionRule(index + 1));
                }
                else content = Group((GroupNode)node.Children[index], depth + 1, node);
                Flex.SetGrow(content, true);
                row.Children.Add(content);
                rows.Children.Add(row);
            }
            group.Children.Add(rows);
        }
        var actions = new Flex { Name = ConditionBuilderParts.Actions, Wrap = FlexWrap.Wrap, Gap = Space.Xs };
        // 与条件的字段对齐：让出连接词那一列（2em）与列间距（0.5rem）
        if (node.Children.Count > 0) actions.Margin = new Thickness(2 * Em + 8, 0, 0, 0);
        node.AddRuleButton = new Button { Name = ConditionBuilderParts.AddRule, Variant = ButtonVariant.Ghost, Size = Size, Icon = Lucide.Plus, Content = lang.ConditionAddRule, IsEnabled = !Locked };
        node.AddRuleButton.Click += (_, _) => AddRule(node, grouped: false);
        actions.Children.Add(node.AddRuleButton);
        if (depth + 1 < MaxDepth)
        {
            var addGroup = new Button { Name = ConditionBuilderParts.AddGroup, Variant = ButtonVariant.Ghost, Size = Size, Icon = Lucide.Plus, Content = lang.ConditionAddGroup, IsEnabled = !Locked };
            addGroup.Click += (_, _) => AddRule(node, grouped: true);
            actions.Children.Add(addGroup);
        }
        group.Children.Add(actions);
        if (!nested) return group;
        // 嵌套的组：浅底加描边，和外层的条件分开；读屏是一组（"条件组"）
        return new Border
        {
            [AutomationProperties.NameProperty] = lang.ConditionGroup,
            Child = group, Padding = new Thickness(Token(ThemeKeys.Space.Sm)), BorderThickness = new Thickness(this.Resource<double>(ThemeKeys.BorderWidth.Xs)),
            CornerRadius = new CornerRadius(Token(ThemeKeys.Radius.Md)),
            [!Border.BackgroundProperty] = this.GetResourceObservable(ThemeKeys.Brush.SurfaceMuted).ToBinding(),
            [!Border.BorderBrushProperty] = this.GetResourceObservable(ThemeKeys.Brush.BorderMuted).ToBinding(),
        };
    }

    /// <summary>控件的高度（同 Select、输入框）：连接词按它居中。</summary>
    internal double ControlHeight => Math.Max(this.Resource<double>(ThemeKeys.Size.Control) * Em, this.Resource<double>(ThemeKeys.Size.MinTarget));

    Select ModeSelect(GroupNode node)
    {
        var lang = Lang;
        var select = new Select
        {
            Name = ConditionBuilderParts.Mode, Size = Size, Value = Key(node.Mode),
            ItemsSource = Enum.GetValues<ConditionMode>().Select(mode => new ListboxOption(Key(mode), lang.ConditionModes[Key(mode)])).ToList(),
        };
        AutomationProperties.SetName(select, lang.ConditionMode);
        select.PropertyChanged += (_, e) =>
        {
            if (e.Property != SelectBase.ValueProperty || select.Value is not { } key) return;
            var mode = Enum.GetValues<ConditionMode>().First(candidate => Key(candidate) == key);
            if (mode == node.Mode) return;
            node.Mode = mode;
            Commit();
            Render(select);
        };
        return select;
    }

    static string Key(ConditionMode mode) => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(mode.ToString());

    void AddRule(GroupNode parent, bool grouped)
    {
        var added = new RuleNode();
        parent.Children.Add(grouped ? new GroupNode(ConditionMode.All, [added]) : added);
        added.Editor = new RuleEditor(this, parent, added);
        Render(added.Editor.FieldSelect);
    }

    internal void Remove(GroupNode parent, Node child)
    {
        parent.Children.Remove(child);
        Commit();
        Render(parent.AddRuleButton);
    }

    /// <summary>重建时控件还挂在上一次的父级上：先拿下来再放进新的。</summary>
    static Control Adopt(Control control)
    {
        if (control.Parent is Panel panel) panel.Children.Remove(control);
        return control;
    }

    /// <summary>条件读成界面上的组：取反包着任一是"都不满足"、包着全部是"不全满足"，包着单条比较是只有它的"都不满足"。</summary>
    static GroupNode ToGroup(Condition condition) => condition switch
    {
        ConditionAll all => new GroupNode(ConditionMode.All, [.. all.Items.Select(ToNode)]),
        ConditionAny any => new GroupNode(ConditionMode.Any, [.. any.Items.Select(ToNode)]),
        ConditionNot { Inner: ConditionNot twice } => ToGroup(twice.Inner),
        ConditionNot { Inner: ConditionAny any } => new GroupNode(ConditionMode.None, [.. any.Items.Select(ToNode)]),
        ConditionNot { Inner: ConditionAll all } => new GroupNode(ConditionMode.NotAll, [.. all.Items.Select(ToNode)]),
        ConditionNot not => new GroupNode(ConditionMode.None, [ToNode(not.Inner)]),
        _ => new GroupNode(ConditionMode.All, [ToNode(condition)]),
    };

    static Node ToNode(Condition condition) => condition is ConditionRule rule ? new RuleNode { Field = rule.Field, Op = rule.Op, Value = rule.Value } : ToGroup(condition);

    /// <summary>界面上的组写回条件：只留填完整的比较，空的组去掉。</summary>
    static Condition? ToCondition(GroupNode node)
    {
        List<Condition> list = [];
        foreach (var child in node.Children)
        {
            if (child is GroupNode group)
            {
                if (ToCondition(group) is { } nested) list.Add(nested);
                continue;
            }
            var rule = (RuleNode)child;
            if (rule.Field is not { } field || rule.Op is not { } op) continue;
            var item = Condition.NeedsValue(op) ? new ConditionRule(field, op, rule.Value) : new ConditionRule(field, op);
            if (item.IsComplete) list.Add(item);
        }
        if (list.Count == 0) return null;
        return node.Mode switch
        {
            ConditionMode.All => Condition.All(list),
            ConditionMode.Any => Condition.Any(list),
            ConditionMode.None => Condition.Not(Condition.Any(list)),
            _ => Condition.Not(Condition.All(list)),
        };
    }

    /// <summary>换比较方式时把值换成新的形状：一个值 ↔ 一组值 ↔ 范围，尽量留住已经填的。</summary>
    internal static object? Reshape(object? value, ConditionOp op)
    {
        var list = Condition.ListOf(value);
        var first = list is not null ? list.FirstOrDefault(item => item is not null and not "") : value;
        return Condition.ShapeOf(op) switch
        {
            ConditionValueShape.None => null,
            ConditionValueShape.List => list is not null ? list.Where(item => item is not null).ToArray() : first is null ? Array.Empty<object?>() : new[] { first },
            ConditionValueShape.Range => list is { Count: 2 } ? value : new[] { first, null },
            _ => first,
        };
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

    internal abstract class Node;

    internal sealed class RuleNode : Node
    {
        public string? Field { get; set; }

        public ConditionOp? Op { get; set; }

        public object? Value { get; set; }

        /// <summary>这一条的控件（重建时搬过去，写了一半的值、焦点不丢）。</summary>
        public RuleEditor? Editor { get; set; }
    }

    internal sealed class GroupNode(ConditionMode mode, List<Node>? children = null) : Node
    {
        public ConditionMode Mode { get; set; } = mode;

        public List<Node> Children { get; } = children ?? [];

        public Select? ModeSelect { get; set; }

        /// <summary>"添加条件"：删掉一条之后焦点落到这里。</summary>
        public Button? AddRuleButton { get; set; }
    }

    /// <summary>一条比较的控件：字段、比较方式、值、删除；换字段时换比较方式与值的输入。</summary>
    internal sealed class RuleEditor
    {
        readonly ConditionBuilder owner;
        readonly RuleNode node;
        readonly Border valueHost = new() { Name = ConditionBuilderParts.Value };

        public RuleEditor(ConditionBuilder owner, GroupNode parent, RuleNode node)
        {
            this.owner = owner;
            this.node = node;
            var lang = owner.Lang;
            var fields = owner.FieldList;
            FieldSelect = new Select
            {
                Name = ConditionBuilderParts.Field, Size = owner.Size, Searchable = fields.Count > 8, Placeholder = lang.ConditionField, Value = node.Field,
                ItemsSource = fields.Select(field => new ListboxOption(field.Name, field.Label) { Group = field.Group }).ToList(),
            };
            AutomationProperties.SetName(FieldSelect, lang.ConditionField);
            FieldSelect.PropertyChanged += (_, e) =>
            {
                if (e.Property == SelectBase.ValueProperty && FieldSelect.Value != node.Field) SetField(FieldSelect.Value);
            };
            OperatorSelect = new Select { Name = ConditionBuilderParts.Operator, Size = owner.Size };
            AutomationProperties.SetName(OperatorSelect, lang.ConditionOperator);
            OperatorSelect.PropertyChanged += (_, e) =>
            {
                if (e.Property != SelectBase.ValueProperty || OperatorSelect.Value is not { } key) return;
                var op = Enum.GetValues<ConditionOp>().First(candidate => Condition.KeyOf(candidate) == key);
                if (op == node.Op) return;
                node.Op = op;
                node.Value = Reshape(node.Value, op);
                owner.Commit();
                SyncValue();
            };
            var remove = new IconButton { Name = ConditionBuilderParts.RemoveRule, Icon = Lucide.X, Variant = ButtonVariant.Ghost, Size = owner.Size, IsEnabled = !owner.Locked };
            AutomationProperties.SetName(remove, lang.ConditionRemoveRule);
            remove.Click += (_, _) => owner.Remove(parent, node);
            Layout = new RuleLayout(FieldSelect, OperatorSelect, valueHost, remove) { Name = ConditionBuilderParts.Rule };
            SyncOperators();
            SyncValue();
        }

        /// <summary>整条（读屏是一组，"条件 2"）。</summary>
        public RuleLayout Layout { get; }

        public Select FieldSelect { get; }

        public Select OperatorSelect { get; }

        ConditionField? Field => owner.FieldOf(node.Field);

        void SetField(string? name)
        {
            node.Field = name;
            var ops = Field is { } field ? Condition.OperatorsOf(field) : [];
            if (node.Op is not { } op || !ops.Contains(op)) node.Op = ops.Count > 0 ? ops[0] : null;
            // 换了字段，原来的值多半不适用：清空，是否型默认"是"
            node.Value = node.Op is { } next ? Reshape(Field?.Type == ConditionFieldType.Boolean ? true : null, next) : null;
            owner.Commit();
            SyncOperators();
            SyncValue();
        }

        void SyncOperators()
        {
            var field = Field;
            OperatorSelect.ItemsSource = field is null ? [] : Condition.OperatorsOf(field).Select(op => new ListboxOption(Condition.KeyOf(op), Condition.OperatorLabel(owner.Lang, field.Type, op))).ToList();
            OperatorSelect.Value = node.Op is { } op ? Condition.KeyOf(op) : null;
            OperatorSelect.IsEnabled = field is not null;
        }

        void SyncValue()
        {
            valueHost.Child = Field is { } field && node.Op is { } op ? ValueInput(field, op) : null;
            valueHost.IsVisible = valueHost.Child is not null;
        }

        void Change(object? next)
        {
            node.Value = next;
            owner.Commit();
        }

        /// <summary>值的输入：随字段类型与比较方式（介于是两个框，是其中之一是多选或标签）。</summary>
        Control? ValueInput(ConditionField field, ConditionOp op)
        {
            var lang = owner.Lang;
            var label = lang.ConditionValue(field.Label);
            var shape = Condition.ShapeOf(op);
            if (shape == ConditionValueShape.None) return null;
            if (field.Input is { } input) return input(new ConditionInputContext(field, op, node.Value, Change, label)) as Control;
            var control = field.Type switch
            {
                ConditionFieldType.Select or ConditionFieldType.Boolean => OptionInput(field, shape, lang),
                _ when shape == ConditionValueShape.List => TagsInput(field),
                ConditionFieldType.Number => NumberInput(shape),
                ConditionFieldType.Date => DateInput(shape),
                _ => TextInput(),
            };
            AutomationProperties.SetName(control, label);
            return control;
        }

        Control OptionInput(ConditionField field, ConditionValueShape shape, Lang lang)
        {
            IReadOnlyList<ConditionOption> options = field.Type == ConditionFieldType.Boolean ? [new(true, lang.ConditionTrue), new(false, lang.ConditionFalse)] : field.Options ?? [];
            // Select 的值是文字：选中后换回选项原来的值
            object? Back(string? key) => options.FirstOrDefault(option => Condition.ToText(option.Value) == key)?.Value;
            var select = new Select { Size = owner.Size, ItemsSource = options.Select(option => new ListboxOption(Condition.ToText(option.Value), option.Label)).ToList() };
            if (shape == ConditionValueShape.List)
            {
                select.SelectionMode = SelectionMode.Multiple;
                select.MaxTagCount = TagLimit.Responsive;
                select.Values = [.. (Condition.ListOf(node.Value) ?? []).Select(Condition.ToText)];
                select.PropertyChanged += (_, e) =>
                {
                    if (e.Property == SelectBase.ValuesProperty) Change((select.Values ?? []).Select(Back).ToArray());
                };
            }
            else
            {
                select.Value = node.Value is null ? null : Condition.ToText(node.Value);
                select.PropertyChanged += (_, e) =>
                {
                    if (e.Property == SelectBase.ValueProperty) Change(Back(select.Value));
                };
            }
            return select;
        }

        Control TagsInput(ConditionField field)
        {
            var tags = new TagsInput { Size = owner.Size, Values = [.. (Condition.ListOf(node.Value) ?? []).Select(Condition.ToText)] };
            tags.PropertyChanged += (_, e) =>
            {
                if (e.Property != Avalonia.TagsInput.ValuesProperty) return;
                var values = tags.Values ?? [];
                Change(field.Type == ConditionFieldType.Number ? values.Select(DecimalOf).Where(number => number is not null).ToArray() : values.ToArray());
            };
            return tags;
        }

        Control NumberInput(ConditionValueShape shape)
        {
            if (shape == ConditionValueShape.Range)
            {
                var ends = Condition.ListOf(node.Value) ?? [];
                var range = new NumberRangeField { Size = owner.Size, Value = new NumberRange(DecimalOf(ends.ElementAtOrDefault(0)), DecimalOf(ends.ElementAtOrDefault(1))) };
                range.PropertyChanged += (_, e) =>
                {
                    if (e.Property == NumberRangeField.ValueProperty) Change(new object?[] { range.Value.Start, range.Value.End });
                };
                return range;
            }
            var number = new NumberField { Size = owner.Size, Value = DecimalOf(node.Value) };
            number.PropertyChanged += (_, e) =>
            {
                if (e.Property == NumberField.ValueProperty) Change(number.Value);
            };
            return number;
        }

        Control DateInput(ConditionValueShape shape)
        {
            if (shape == ConditionValueShape.Range)
            {
                var ends = Condition.ListOf(node.Value) ?? [];
                var picker = new DateRangePicker { Size = owner.Size, Value = ends is [string start, string end] ? new IsoDateRange(start, end) : null };
                picker.PropertyChanged += (_, e) =>
                {
                    if (e.Property == DateRangePicker.ValueProperty) Change(picker.Value is { } range ? new object?[] { range.Start, range.End } : new object?[] { null, null });
                };
                return picker;
            }
            var date = new DatePicker { Size = owner.Size, Value = node.Value as string };
            date.PropertyChanged += (_, e) =>
            {
                if (e.Property == DatePicker.ValueProperty) Change(date.Value);
            };
            return date;
        }

        Control TextInput()
        {
            var text = new TextField { Size = owner.Size, Text = node.Value as string ?? "" };
            text.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextBox.TextProperty) Change(text.Text);
            };
            return text;
        }

        /// <summary>数字框、标签里的值读成 decimal；读不成时为 null。</summary>
        static decimal? DecimalOf(object? value) => value switch
        {
            null => null,
            decimal exact => exact,
            string text => decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : null,
            IConvertible convertible => convertible.ToDecimal(CultureInfo.InvariantCulture),
            _ => null,
        };
    }

    /// <summary>
    /// 一条比较的排法（上游 rule 的 flex-wrap）：字段（9rem 起，7–14rem）、比较方式（8rem 起，至少 6rem）、值（12rem 起，至少 8rem）按起始宽度排，放不下换行；
    /// 一行里剩下的宽度按 1 : 0 : 2 分给字段、比较方式、值（字段不超过 14rem），不够时按起始宽度缩到最小宽度；删除按钮按自己的宽度。
    /// </summary>
    internal sealed class RuleLayout : Panel
    {
        readonly (Control Child, double Basis, double Grow, double Min, double Max)[] items;
        Rect[] slots = [];

        public RuleLayout(Control field, Control op, Control value, Control remove)
        {
            const double rem = 16;
            items = [(field, 9 * rem, 1, 7 * rem, 14 * rem), (op, 8 * rem, 0, 6 * rem, double.PositiveInfinity), (value, 12 * rem, 2, 8 * rem, double.PositiveInfinity), (remove, double.NaN, 0, 0, double.PositiveInfinity)];
            Children.AddRange([field, op, value, remove]);
        }

        double Gap => this.Resource<double>(ThemeKeys.Space.Sm) * TextElement.GetFontSize(this);

        protected override Size MeasureOverride(Size availableSize)
        {
            var visible = items.Where(item => item.Child.IsVisible).ToList();
            foreach (var item in visible) item.Child.Measure(global::Avalonia.Size.Infinity);
            double Basis((Control Child, double Basis, double Grow, double Min, double Max) item) =>
                double.IsNaN(item.Basis) ? item.Child.DesiredSize.Width : Math.Clamp(item.Basis, item.Min, item.Max);
            var gap = Gap;
            var width = double.IsInfinity(availableSize.Width) ? visible.Sum(Basis) + gap * (visible.Count - 1) : availableSize.Width;
            var slotList = new Rect[items.Length];
            var y = 0.0;
            var start = 0;
            while (start < visible.Count)
            {
                // 这一行放得下的几项（至少一项）
                var end = start + 1;
                var used = Basis(visible[start]);
                while (end < visible.Count && used + gap + Basis(visible[end]) <= width) used += gap + Basis(visible[end++]);
                var line = visible.GetRange(start, end - start);
                var widths = line.Select(Basis).ToArray();
                var free = width - used;
                if (free > 0) Grow(line, widths, free);
                else if (free < 0) Shrink(line, widths, -free);
                var x = 0.0;
                var height = 0.0;
                for (var i = 0; i < line.Count; i++)
                {
                    line[i].Child.Measure(new Size(widths[i], double.PositiveInfinity));
                    height = Math.Max(height, line[i].Child.DesiredSize.Height);
                }
                for (var i = 0; i < line.Count; i++)
                {
                    slotList[Array.FindIndex(items, item => item.Child == line[i].Child)] = new Rect(x, y, widths[i], line[i].Child.DesiredSize.Height);
                    x += widths[i] + gap;
                }
                y += height + gap;
                start = end;
            }
            slots = slotList;
            return new Size(double.IsInfinity(availableSize.Width) ? width : availableSize.Width, Math.Max(0, y - gap));
        }

        /// <summary>剩下的宽度按 grow 分，到了最大宽度的不再分，多出来的给其余的（同 CSS 的 flex-grow）。</summary>
        static void Grow(List<(Control Child, double Basis, double Grow, double Min, double Max)> line, double[] widths, double free)
        {
            var open = Enumerable.Range(0, line.Count).Where(i => line[i].Grow > 0).ToList();
            while (free > 0.5 && open.Count > 0)
            {
                var total = open.Sum(i => line[i].Grow);
                var left = 0.0;
                foreach (var i in open.ToList())
                {
                    var next = widths[i] + free * line[i].Grow / total;
                    if (next < line[i].Max) widths[i] = next;
                    else
                    {
                        left += next - line[i].Max;
                        widths[i] = line[i].Max;
                        open.Remove(i);
                    }
                }
                free = left;
            }
        }

        /// <summary>不够时按起始宽度的比例缩，缩到最小宽度为止（同 CSS 的 flex-shrink: 1）。</summary>
        static void Shrink(List<(Control Child, double Basis, double Grow, double Min, double Max)> line, double[] widths, double over)
        {
            var open = Enumerable.Range(0, line.Count).Where(i => !double.IsNaN(line[i].Basis)).ToList();
            while (over > 0.5 && open.Count > 0)
            {
                var total = open.Sum(i => widths[i]);
                var left = 0.0;
                foreach (var i in open.ToList())
                {
                    var next = widths[i] - over * widths[i] / total;
                    if (next > line[i].Min) widths[i] = next;
                    else
                    {
                        left += line[i].Min - next;
                        widths[i] = line[i].Min;
                        open.Remove(i);
                    }
                }
                over = left;
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            for (var i = 0; i < items.Length; i++)
                if (items[i].Child.IsVisible && i < slots.Length) items[i].Child.Arrange(slots[i]);
            return finalSize;
        }
    }
}

/// <summary>ConditionBuilder 的部件名。</summary>
public static class ConditionBuilderParts
{
    /// <summary>每个组。</summary>
    public const string Group = "PART_Group";

    /// <summary>组的表头（满足方式、删除整组）。</summary>
    public const string Header = "PART_Header";

    /// <summary>满足方式的下拉。</summary>
    public const string Mode = "PART_Mode";

    /// <summary>组里的各行。</summary>
    public const string Rows = "PART_Rows";

    /// <summary>一行：连接词与一条比较（或一个嵌套的组）。</summary>
    public const string Row = "PART_Row";

    /// <summary>连接词（且、或）。</summary>
    public const string Joiner = "PART_Joiner";

    /// <summary>每条比较。</summary>
    public const string Rule = "PART_Rule";

    /// <summary>字段的下拉。</summary>
    public const string Field = "PART_Field";

    /// <summary>比较方式的下拉。</summary>
    public const string Operator = "PART_Operator";

    /// <summary>值的输入那一格。</summary>
    public const string Value = "PART_Value";

    /// <summary>"添加条件""添加条件组"那一行。</summary>
    public const string Actions = "PART_Actions";

    /// <summary>"添加条件"按钮。</summary>
    public const string AddRule = "PART_AddRule";

    /// <summary>"添加条件组"按钮。</summary>
    public const string AddGroup = "PART_AddGroup";

    /// <summary>每条比较的删除按钮。</summary>
    public const string RemoveRule = "PART_RemoveRule";

    /// <summary>嵌套组的删除整组按钮。</summary>
    public const string RemoveGroup = "PART_RemoveGroup";

    /// <summary>还没有条件时的提示。</summary>
    public const string Empty = "PART_Empty";
}
