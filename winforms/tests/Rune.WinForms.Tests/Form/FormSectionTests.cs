namespace Rune.WinForms.Tests;

/// <summary>FormSectionControl：分组的读屏语义、栅格的列（最多几列、窄时减少）、这一组的字段布局覆盖 FormControl 的、可折叠。</summary>
public class FormSectionTests
{
    [Fact]
    [Upstream("FormSection", "role=")]
    public Task A_titled_section_is_a_group_named_by_its_title() => WinFormsUi.Run(() =>
    {
        var section = new FormSectionControl { Title = "项目基本信息", Description = "带 * 的必填", HeadingLevel = 2, Dock = DockStyle.Top };
        section.Fields.Add(new FieldControl { Label = "项目名称", Content = new TextFieldControl() });
        using var form = WinFormsUi.Show(section);

        var accessible = section.AccessibilityObject;
        Assert.Equal((AccessibleRole.Grouping, "项目基本信息", "带 * 的必填"), (accessible.Role, accessible.Name, section.AccessibleDescription));
        Assert.Equal("项目基本信息", Assert.IsType<H2Control>(section.Parts.Heading).Text);
    });

    [Fact]
    [Upstream("FormSection", "没有标题时不是 group，只是一层栅格；columns 多列时写最多几列与最小列宽")]
    public Task Without_a_title_it_is_only_a_grid_of_at_most_n_columns() => WinFormsUi.Run(() =>
    {
        var section = new FormSectionControl { Columns = 2, MinColumnWidth = new Length("18em"), Dock = DockStyle.Top };
        for (var i = 0; i < 3; i++) section.Fields.Add(new FieldControl { Label = $"字段 {i}", Content = new TextFieldControl() });
        using var form = WinFormsUi.Show(section);
        form.ClientSize = new Size(form.LogicalToDeviceUnits(800), form.LogicalToDeviceUnits(600));
        WinFormsUi.Pump();

        Assert.Equal(AccessibleRole.Pane, section.AccessibleRole);
        Assert.False(section.Parts.Header.Visible);
        Assert.Equal(2, section.Grid.ColumnCount); // 放得下更多也只排 2 列
        form.ClientSize = new Size(form.LogicalToDeviceUnits(300), form.ClientSize.Height);
        WinFormsUi.Pump();
        Assert.Equal(1, section.Grid.ColumnCount); // 窄到放不下两列 18em
    });

    [Fact]
    [Upstream("FormSection", "这一组的布局与只读覆盖 Form 的，其他组照旧")]
    public Task The_section_layout_and_read_only_override_the_form() => WinFormsUi.Run(() =>
    {
        var (a, b) = (new TextFieldControl(), new TextFieldControl());
        var editable = new FormSectionControl { Title = "可编辑", Dock = DockStyle.Top };
        editable.Fields.Add(new FieldControl { Label = "甲", Content = a });
        var review = new FormSectionControl { Title = "审批意见", IsReadOnly = true, LabelPlacement = FieldLabelPlacement.Top, Dock = DockStyle.Top };
        review.Fields.Add(new FieldControl { Label = "乙", Content = b });
        var column = new FlexControl { Direction = FlexDirection.Column, AutoSize = true, Dock = DockStyle.Top };
        column.Controls.AddRange([editable, review]);
        var form = new FormControl { LabelPlacement = FieldLabelPlacement.Start, Dock = DockStyle.Fill };
        form.Controls.Add(column);
        using var window = WinFormsUi.Show(form);

        Assert.False(a.Input.ReadOnly);
        Assert.True(b.Input.ReadOnly);
        Assert.True(a.Left > 0); // 标签一列，控件在右边
        Assert.Equal(0, b.Left);
    });

    [Fact]
    [Upstream("FormSection", "collapsible：标题是按钮，aria-expanded 跟着展开收起，open 双向绑定")]
    public Task A_collapsible_title_is_a_button_that_toggles_open() => WinFormsUi.Run(() =>
    {
        var section = new FormSectionControl { Title = "经费及项目依据", IsCollapsible = true, Dock = DockStyle.Top };
        section.Fields.Add(new TextFieldControl());
        var changes = 0;
        section.OpenChanged += (_, _) => changes++;
        using var form = WinFormsUi.Show(section);

        var trigger = section.Parts.Trigger;
        Assert.Same(trigger, section.Parts.Heading);
        Assert.True(trigger.AccessibilityObject.State.HasFlag(AccessibleStates.Expanded));
        trigger.AccessibilityObject.DoDefaultAction();
        Assert.Equal((false, 1), (section.IsOpen, changes));
        Assert.True(trigger.AccessibilityObject.State.HasFlag(AccessibleStates.Collapsed));
    });
}
