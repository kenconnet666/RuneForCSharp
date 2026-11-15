using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Data;

namespace Rune.Wpf.Tests;

/// <summary>FormSection：分组的读屏语义、栅格的列（最多几列、窄时减少）、这一组的字段布局覆盖 Form 的、可折叠。</summary>
public class FormSectionTests
{
    static FrameworkElement Show(FrameworkElement content)
    {
        content.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(content);
        WpfUi.Flush();
        return content;
    }

    [Fact]
    [Upstream("FormSection", "role=")]
    public Task A_titled_section_is_a_group_named_by_its_title() => WpfUi.Run(() =>
    {
        var section = new FormSection { Title = "项目基本信息", Description = "带 * 的必填", HeadingLevel = 2, Children = { new Field { Label = "项目名称", Content = new TextField() } } };
        Show(section);

        var peer = section.Peer();
        Assert.Equal((AutomationControlType.Group, "项目基本信息", "带 * 的必填"), (peer.GetAutomationControlType(), peer.GetName(), peer.GetHelpText()));
        var heading = section.Parts.Heading;
        Assert.Equal(("项目基本信息", AutomationHeadingLevel.Level2), (heading.Text, AutomationProperties.GetHeadingLevel(heading)));
    });

    [Fact]
    [Upstream("FormSection", "没有标题时不是 group，只是一层栅格；columns 多列时写最多几列与最小列宽")]
    public Task Without_a_title_it_is_only_a_grid_of_at_most_n_columns() => WpfUi.Run(() =>
    {
        var section = new FormSection { Columns = 2, MinColumnWidth = new Length("18em"), Width = 800 };
        for (var i = 0; i < 3; i++) section.Children.Add(new Field { Label = $"字段 {i}", Content = new TextField() });
        Show(section);

        Assert.False(section.Peer().IsControlElement());
        Assert.Equal(Visibility.Collapsed, section.Parts.Header.Visibility);
        Assert.Equal(2, section.Parts.Body.ColumnCount); // 放得下更多也只排 2 列
        section.Width = 300;
        WpfUi.Flush();
        Assert.Equal(1, section.Parts.Body.ColumnCount); // 窄到放不下两列 18em
    });

    [Fact]
    [Upstream("FormSection", "这一组的布局与只读覆盖 Form 的，其他组照旧")]
    public Task The_section_layout_and_read_only_override_the_form() => WpfUi.Run(() =>
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
        Show(form);

        Assert.False(a.ShowsReadOnly);
        Assert.True(b.ShowsReadOnly);
        Assert.Equal((FieldLabelPlacement.Start, FieldLabelPlacement.Top), (fieldA.LabelPlacement, fieldB.LabelPlacement));
        Assert.True(a.TranslatePoint(default, fieldA).X > 0); // 标签一列，控件在右边
        Assert.Equal(0, b.TranslatePoint(default, fieldB).X);
    });

    [Fact]
    [Upstream("FormSection", "collapsible：标题是按钮，aria-expanded 跟着展开收起，open 双向绑定")]
    public Task A_collapsible_title_is_a_button_bound_to_open() => WpfUi.Run(() =>
    {
        var state = new OpenState();
        var section = new FormSection { Title = "经费及项目依据", IsCollapsible = true, DataContext = state, Children = { new TextField() } };
        section.SetBinding(FormSection.IsOpenProperty, new Binding(nameof(OpenState.Open)));
        Show(section);

        var trigger = section.Parts.Trigger;
        Assert.Equal(AutomationHeadingLevel.Level3, AutomationProperties.GetHeadingLevel(trigger));
        var expand = (IExpandCollapseProvider)trigger.Peer();
        Assert.Equal(ExpandCollapseState.Expanded, expand.ExpandCollapseState);
        trigger.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        WpfUi.Flush();
        Assert.False(state.Open);
        Assert.Equal(ExpandCollapseState.Collapsed, expand.ExpandCollapseState);
    });

    sealed class OpenState
    {
        public bool Open { get; set; } = true;
    }
}
