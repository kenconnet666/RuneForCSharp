using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;

namespace Rune.Avalonia.Tests;

/// <summary>FormSection：分组的读屏语义、栅格的列（最多几列、窄时减少）、这一组的字段布局覆盖 Form 的、可折叠。</summary>
public class FormSectionTests
{
    [Fact]
    [Upstream("FormSection", "role=")]
    public Task A_titled_section_is_a_group_named_by_its_title() => HeadlessUi.Run(() =>
    {
        var section = new FormSection { Title = "项目基本信息", Description = "带 * 的必填", HeadingLevel = 2, Children = { new Field { Label = "项目名称", Content = new TextField() } } };
        HeadlessUi.Show(section);

        var peer = section.Peer();
        Assert.Equal((AutomationControlType.Group, "项目基本信息", "带 * 的必填"), (peer.GetAutomationControlType(), peer.GetName(), peer.GetHelpText()));
        var heading = section.Part<Text>(FormSectionParts.Heading);
        Assert.Equal(("项目基本信息", 2), (heading.Text, AutomationProperties.GetHeadingLevel(heading)));
    });

    [Fact]
    [Upstream("FormSection", "没有标题时不是 group，只是一层栅格；columns 多列时写最多几列与最小列宽")]
    public Task Without_a_title_it_is_only_a_grid_of_at_most_n_columns() => HeadlessUi.Run(() =>
    {
        var section = new FormSection { Columns = 2, MinColumnWidth = new Length("18em"), Width = 800 };
        for (var i = 0; i < 3; i++) section.Children.Add(new Field { Label = $"字段 {i}", Content = new TextField() });
        var window = HeadlessUi.Show(section);

        Assert.False(section.Peer().IsControlElement());
        Assert.False(section.Part<Border>(FormSectionParts.Header).IsVisible);
        Assert.Equal(2, section.Body.ColumnCount); // 放得下更多也只排 2 列
        section.Width = 300;
        HeadlessUi.Flush(window);
        Assert.Equal(1, section.Body.ColumnCount); // 窄到放不下两列 18em
    });

    [Fact]
    [Upstream("FormSection", "这一组的布局与只读覆盖 Form 的，其他组照旧")]
    public Task The_section_layout_and_read_only_override_the_form() => HeadlessUi.Run(() =>
    {
        var (a, b) = (new TextField(), new TextField());
        var (fieldA, fieldB) = (new Field { Label = "甲", Content = a }, new Field { Label = "乙", Content = b });
        var form = new Form
        {
            LabelPlacement = FieldLabelPlacement.Start,
            Child = new StackPanel
            {
                Children =
                {
                    new FormSection { Title = "可编辑", Children = { fieldA } },
                    new FormSection { Title = "审批意见", IsReadOnly = true, LabelPlacement = FieldLabelPlacement.Top, Children = { fieldB } },
                },
            },
        };
        HeadlessUi.Show(form);

        Assert.DoesNotContain(":readonly", a.Classes);
        Assert.Contains(":readonly", b.Classes);
        Assert.Contains(":start", fieldA.Classes);
        Assert.DoesNotContain(":start", fieldB.Classes);
    });

    [Fact]
    [Upstream("FormSection", "collapsible：标题是按钮，aria-expanded 跟着展开收起，open 双向绑定")]
    public Task A_collapsible_title_is_a_button_bound_to_open() => HeadlessUi.Run(() =>
    {
        var state = new OpenState();
        var section = new FormSection { Title = "经费及项目依据", IsCollapsible = true, DataContext = state, Children = { new TextField() } };
        section.Bind(FormSection.IsOpenProperty, new Binding(nameof(OpenState.Open)));
        var window = HeadlessUi.Show(section);

        var trigger = section.Trigger;
        Assert.Equal(3, AutomationProperties.GetHeadingLevel(trigger));
        Assert.Equal(ExpandCollapseState.Expanded, ((IExpandCollapseProvider)trigger.Peer()).ExpandCollapseState);
        trigger.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        HeadlessUi.Flush(window);
        Assert.False(state.Open);
        Assert.Equal(ExpandCollapseState.Collapsed, ((IExpandCollapseProvider)trigger.Peer()).ExpandCollapseState);
        Assert.DoesNotContain(":open", section.Classes);
    });

    sealed class OpenState
    {
        public bool Open { get; set; } = true;
    }
}
