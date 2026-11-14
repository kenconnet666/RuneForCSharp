namespace Rune.WinForms.Tests;

/// <summary>ControlGroupControl：输入框外壳、选择框、按钮去掉相邻一侧的圆角；输入框分掉剩余宽度、按钮与输入框等高；在 FieldControl 里时组拿标签，里面的控件不拿。</summary>
public class ControlGroupTests
{
    [Fact]
    public Task Inputs_share_the_width_and_neighbours_lose_inner_corners() => WinFormsUi.Run(() =>
    {
        var currency = new SelectControl { ItemsSource = new[] { "CNY", "USD" }, Value = "USD", AccessibleName = "币种" };
        var amount = new NumberFieldControl { Value = 1200, AccessibleName = "金额" };
        var convert = new ButtonControl { Text = "换算" };
        var group = new ControlGroupControl();
        group.Controls.AddRange([currency, amount, convert]);
        var field = new FieldControl { Label = "合同金额", Description = "按当天中间价换算", Content = group, Width = 600 };
        using var form = WinFormsUi.Show(field);
        field.PerformLayout();

        Assert.Equal(AttachedCorners.TopRight | AttachedCorners.BottomRight, Attached.Get(currency));
        Assert.Equal(AttachedCorners.TopLeft | AttachedCorners.BottomLeft, Attached.Get(convert));
        Assert.Equal(group.ClientSize.Width, convert.Right); // 输入框分掉剩余宽度，按钮贴在末尾
        Assert.True(amount.Width > currency.Width);
        Assert.Equal(amount.Height, convert.Height);
        Assert.Equal(Padding.Empty, convert.Padding); // 与输入框等高：不另留焦点环的边距

        Assert.Equal(AccessibleRole.Grouping, group.AccessibilityObject.Role);
        Assert.Equal("合同金额", group.AccessibilityObject.Name);
        Assert.Equal("按当天中间价换算", group.AccessibilityObject.Description);
        Assert.Equal("金额", amount.AccessibilityObject.Name); // 里面的控件用自己的名称
    });
}
