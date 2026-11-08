using System.Runtime.InteropServices;

namespace Rune.WinForms.Tests;

/// <summary>
/// Checkbox、Radio 与两个组：点击、空格、读屏切换；组的值写回；部分勾选；方向键在单选组里移动并选中、Tab 停在选中项；
/// 只读、出错随组；数据生成的项；拦下切换；方框、圆点的颜色。
/// </summary>
public class ChoiceTests
{
    const uint WmLButtonDown = 0x0201, WmLButtonUp = 0x0202, WmKeyDown = 0x0100, WmKeyUp = 0x0101;

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);

    static ElementContext ContextOf(Control control) => ElementContext.Of(control);

    /// <summary>真实的鼠标消息：在方框、圆的中间按下、松开。</summary>
    static void Click<T>(ChoiceControl<T> control) where T : ChoiceBase, new()
    {
        var box = control.Element.ControlBounds(ContextOf(control));
        var position = (nint)(((int)(box.Y + box.Height / 2) << 16) | ((int)(box.X + box.Width / 2) & 0xFFFF));
        SendMessage(control.Handle, WmLButtonDown, 1, position);
        SendMessage(control.Handle, WmLButtonUp, 0, position);
    }

    static void Press(Control control, Keys key)
    {
        SendMessage(control.Handle, WmKeyDown, (nint)key, 0);
        SendMessage(control.Handle, WmKeyUp, (nint)key, unchecked((nint)0xC0000001));
    }

    static Color Pixel<T>(ChoiceControl<T> control, float dx = 0.5f, float dy = 0.5f) where T : ChoiceBase, new()
    {
        var box = control.Element.ControlBounds(ContextOf(control));
        using var bitmap = new Bitmap(control.Width, control.Height);
        control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, control.Size));
        return bitmap.GetPixel((int)(box.X + dx * box.Width), (int)(box.Y + dy * box.Height));
    }

    static void AssertNear(Color expected, Color actual, int tolerance = 8)
    {
        Assert.InRange(Math.Abs(expected.R - actual.R), 0, tolerance);
        Assert.InRange(Math.Abs(expected.G - actual.G), 0, tolerance);
        Assert.InRange(Math.Abs(expected.B - actual.B), 0, tolerance);
    }

    [Fact]
    [Upstream("Checkbox", "原生 checkbox：点击写回")]
    public Task Checkbox_toggles_and_shows_an_indeterminate_state() => WinFormsUi.Run(() =>
    {
        var agree = new CheckboxControl { Text = "同意", Dock = DockStyle.Top };
        var all = new CheckboxControl { Text = "全选", IsIndeterminate = true, Dock = DockStyle.Top };
        using var form = WinFormsUi.Show(agree, all);

        Assert.Equal(AccessibleRole.CheckButton, agree.AccessibilityObject.Role);
        Assert.Equal("同意", agree.AccessibilityObject.Name);
        Click(agree);
        Assert.True(agree.IsChecked);
        Assert.True(agree.AccessibilityObject.State.HasFlag(AccessibleStates.Checked));
        Press(agree, Keys.Space);
        Assert.False(agree.IsChecked);

        Assert.True(all.AccessibilityObject.State.HasFlag(AccessibleStates.Mixed)); // 部分勾选：读屏念"混合"
        all.Invalidate();
        all.Update();
        Assert.Same(Lucide.Minus, all.Element.IndicatorPart.Data); // 横线
    });

    [Fact]
    public Task Checkbox_box_uses_the_tone_fill_when_checked_and_danger_when_invalid() => WinFormsUi.Run(() =>
    {
        var plain = new CheckboxControl { Dock = DockStyle.Top };
        var on = new CheckboxControl { IsChecked = true, Tone = StatusTone.Success, Dock = DockStyle.Top };
        var bad = new CheckboxControl { IsInvalid = true, Dock = DockStyle.Top };
        using var form = WinFormsUi.Show(plain, on, bad);
        var colors = RuneTheme.Current.Value.Colors;

        AssertNear(colors.Surface, Pixel(plain)); // 没勾：表面底色
        AssertNear(colors.BorderStrong, Pixel(plain, 0.5f, 0.08f), 24); // 描边（抗锯齿边缘放宽）
        AssertNear(ToneFill.Colors(StatusTone.Success, colors).Fill, Pixel(on, 0.12f, 0.5f)); // 勾上：色调实底（避开中间的对勾）
        AssertNear(colors.Danger, Pixel(bad, 0.5f, 0.08f), 24); // 出错：描边是危险色
        Assert.Equal(1.125f * plain.Font.Size * plain.DeviceDpi / 72, plain.Element.ControlBounds(ContextOf(plain)).Width, 1f);
    });

    [Fact]
    [Upstream("Checkbox", "复选组：value 是勾选项的数组")]
    public Task Checkbox_group_writes_the_checked_values_back() => WinFormsUi.Run(() =>
    {
        var email = new CheckboxControl { Value = "email", Text = "邮件" };
        var sms = new CheckboxControl { Value = "sms", Text = "短信" };
        var group = new CheckboxGroupControl { Value = ["email"] };
        group.Controls.AddRange([email, sms]);
        using var form = WinFormsUi.Show(group);

        Assert.Equal(AccessibleRole.Grouping, group.AccessibilityObject.Role);
        Assert.Equal((true, false), (email.IsChecked, sms.IsChecked));
        Click(sms);
        Assert.Equal(["email", "sms"], group.Value);
        Click(email);
        Assert.Equal(["sms"], group.Value);
        Assert.Equal((false, true), (email.IsChecked, sms.IsChecked));

        group.IsReadOnly = true; // 只读随组
        Click(sms);
        Assert.Equal(["sms"], group.Value);
        Assert.True(sms.AccessibilityObject.State.HasFlag(AccessibleStates.ReadOnly));
    });

    [Fact]
    [Upstream("RadioGroup", "同组的 radio 共用自动生成的 name")]
    public Task Radio_group_selects_one_value_and_disables_as_a_whole() => WinFormsUi.Run(() =>
    {
        var express = new RadioControl { Value = "express", Text = "快递" };
        var pickup = new RadioControl { Value = "pickup", Text = "自提" };
        var group = new RadioGroupControl { Value = "express" };
        group.Controls.AddRange([express, pickup]);
        using var form = WinFormsUi.Show(group);

        Assert.Equal(AccessibleRole.RadioButton, express.AccessibilityObject.Role);
        Assert.Equal((true, false), (express.IsChecked, pickup.IsChecked));
        Click(pickup);
        Assert.Equal("pickup", group.Value);
        Assert.Equal((false, true), (express.IsChecked, pickup.IsChecked));

        group.Enabled = false;
        Assert.False(express.CanSelect || pickup.CanSelect);
    });

    [Fact]
    public Task Radio_group_has_one_tab_stop_and_arrows_move_and_select() => WinFormsUi.Run(() =>
    {
        var (a, b, c) = (new RadioControl { Value = "a", Text = "甲" }, new RadioControl { Value = "b", Text = "乙", Enabled = false }, new RadioControl { Value = "c", Text = "丙" });
        var group = new RadioGroupControl();
        group.Controls.AddRange([a, b, c]);
        using var form = WinFormsUi.Show(group);

        Assert.Equal((true, false, false), (a.TabStop, b.TabStop, c.TabStop)); // 没选中时停在第一项
        group.Value = "c";
        Assert.Equal((false, true), (a.TabStop, c.TabStop));

        Press(c, Keys.Down); // 到头从另一端继续
        Assert.Equal("a", group.Value);
        Press(a, Keys.Right); // 跳过不可用的
        Assert.Equal("c", group.Value);

        group.IsReadOnly = true;
        Press(c, Keys.Up);
        Assert.Equal("c", group.Value);
    });

    [Fact]
    public Task Radio_dot_and_circle_follow_the_group_tone_and_size() => WinFormsUi.Run(() =>
    {
        var on = new RadioControl { Value = "x" };
        var off = new RadioControl { Value = "y" };
        var group = new RadioGroupControl { Value = "x", Tone = StatusTone.Warning, TextSize = TextSize.Xl };
        group.Controls.AddRange([on, off]);
        using var form = WinFormsUi.Show(group);
        var colors = RuneTheme.Current.Value.Colors;
        var (fill, onFill) = ToneFill.Colors(StatusTone.Warning, colors);

        Assert.Equal(1, on.Element.DotScale);
        Assert.Equal(0, off.Element.DotScale); // 没选中：圆点缩成 0
        AssertNear(onFill, Pixel(on)); // 圆点：实底上的颜色
        AssertNear(fill, Pixel(on, 0.2f, 0.5f)); // 圆：色调实底
        AssertNear(colors.Surface, Pixel(off));
        Assert.True(on.Element.ControlBounds(ContextOf(on)).Width > 1.125f * on.Font.Size * on.DeviceDpi / 72 + 2); // 字号随组
    });

    [Fact]
    [Upstream("Switch", "Switch 与 Checkbox")]
    [Upstream("Radio", "Radio：整组对回父级的值")]
    public Task Cancelled_toggles_keep_the_old_value() => WinFormsUi.Run(() =>
    {
        var checkbox = new CheckboxControl { IsChecked = true, Dock = DockStyle.Top };
        checkbox.Toggling += (_, e) => e.Cancel = true;
        var (a, b) = (new RadioControl { Value = "a" }, new RadioControl { Value = "b" });
        b.Toggling += (_, e) => e.Cancel = true;
        var group = new RadioGroupControl { Value = "a", Dock = DockStyle.Fill };
        group.Controls.AddRange([a, b]);
        using var form = WinFormsUi.Show(group, checkbox);

        Click(checkbox);
        Click(b);
        Assert.True(checkbox.IsChecked);
        Assert.Equal(("a", true, false), (group.Value, a.IsChecked, b.IsChecked));
    });

    [Fact]
    [Upstream("CheckboxGroup", "CheckboxGroup 的 items")]
    public Task Checkbox_group_items_come_from_options() => WinFormsUi.Run(() =>
    {
        var group = new CheckboxGroupControl
        {
            Value = ["email"],
            ItemsSource = new[]
            {
                new ListboxOption("email", "邮件") { Description = "每天汇总一封" },
                new ListboxOption("sms", "短信") { IsDisabled = true },
                new ListboxOption("app", "站内信"),
            },
        };
        using var form = WinFormsUi.Show(group);
        var items = group.Controls.OfType<CheckboxControl>().ToList();

        Assert.Equal(["email", "sms", "app"], items.Select(item => item.Value));
        Assert.Equal(["邮件", "短信", "站内信"], items.Select(item => item.Text));
        Assert.True(items[0].IsChecked);
        Assert.False(items[1].Enabled);
        Assert.Equal("每天汇总一封", items[0].AccessibilityObject.Help);
        Assert.True(items[0].Height > items[2].Height); // 说明在文字下面
        Click(items[2]);
        Assert.Equal(["email", "app"], group.Value);
    });

    /// <summary>TS 的 propsItem 是 ItemGenerated：按这一项的数据改生成的控件，值仍由组管理。</summary>
    [Fact]
    public Task Item_generated_customizes_items_and_the_group_keeps_values() => WinFormsUi.Run(() =>
    {
        var group = new RadioGroupControl { ItemsSource = new[] { "a", "b" } };
        group.ItemGenerated += (_, e) =>
        {
            e.Control.Name = $"radio-{e.Item}";
            ((RadioControl)e.Control).Value = "x";
        };
        using var form = WinFormsUi.Show(group);
        var radios = group.Controls.OfType<RadioControl>().ToList();

        Assert.Equal(["radio-a", "radio-b"], radios.Select(radio => radio.Name)); // 订阅时重新生成，已有的项也跟着变
        Assert.Equal(["a", "b"], radios.Select(radio => radio.Value));
    });

    [Fact]
    [Upstream("RadioGroup", "RadioGroup 的 items")]
    public Task Radio_group_items_come_from_strings_or_mapped_data() => WinFormsUi.Run(() =>
    {
        var plain = new RadioGroupControl { ItemsSource = new[] { "日", "周", "月" }, Dock = DockStyle.Top };
        var plans = new RadioGroupControl
        {
            ToOption = item => item is (string id, string name, string price) ? new ListboxOption(id, name) { Description = price } : throw new ArgumentException(null, nameof(item)),
            ItemsSource = new[] { ("basic", "基础版", "免费"), ("pro", "专业版", "¥99 / 月") },
            Dock = DockStyle.Top,
        };
        using var form = WinFormsUi.Show(plain, plans);

        Assert.Equal(["日", "周", "月"], plain.Controls.OfType<RadioControl>().Select(radio => radio.Text));
        var pro = plans.Controls.OfType<RadioControl>().ElementAt(1);
        Assert.Equal(("pro", "专业版", "¥99 / 月"), (pro.Value, pro.Text, pro.Description));
    });

    [Fact]
    public Task Scope_choice_defaults_reach_checkboxes_and_radio_groups() => WinFormsUi.Run(() =>
    {
        var checkbox = new CheckboxControl();
        var explicitTone = new CheckboxControl { Tone = StatusTone.Info };
        var group = new RadioGroupControl();
        var scope = new ProviderControl { Dock = DockStyle.Fill };
        scope.Defaults.Set(new ChoiceDefaults { Tone = StatusTone.Danger });
        scope.Controls.AddRange([checkbox, explicitTone, group]);
        using var form = WinFormsUi.Show(scope);

        Assert.Equal((StatusTone.Danger, StatusTone.Info, StatusTone.Danger), (checkbox.Tone, explicitTone.Tone, group.Tone));
    });

    [Fact]
    public Task Card_flows_from_the_group_and_draws_the_tone_when_checked() => WinFormsUi.Run(() =>
    {
        var basic = new RadioControl { Value = "basic", Text = "基础版", End = "¥0" };
        var pro = new RadioControl { Value = "pro", Text = "专业版", Description = "团队协作", End = "¥99" };
        var plain = new RadioControl { Value = "plain", Text = "不要卡片", Card = false };
        var plans = new RadioGroupControl { Value = "basic", Card = true, Orientation = Orientation.Horizontal, Width = 720 };
        plans.Controls.AddRange([basic, pro, plain]);
        using var form = WinFormsUi.Show(plans);
        WinFormsUi.Pump();

        Assert.True(basic.Element.AsCard); // 组的 Card 交给每一项
        Assert.False(plain.Element.AsCard); // 单项写的优先
        Assert.Equal(basic.Width, pro.Width); // 横排按最小宽度分列，每张一样宽
        Assert.Equal(0, basic.Padding.All); // 卡片的边就是控件的边
        var colors = ContextOf(basic).Theme.Colors;
        var tone = ToneFill.Colors(StatusTone.Accent, colors).Fill;
        using var bitmap = new Bitmap(basic.Width, basic.Height);
        basic.DrawToBitmap(bitmap, new Rectangle(Point.Empty, basic.Size));
        var edge = bitmap.GetPixel(basic.Width / 2, (int)(ContextOf(basic).Scale(Tokens.BorderWidth.Xs) / 2));
        Assert.True(Math.Abs(edge.B - tone.B) + Math.Abs(edge.R - tone.R) < 60, $"描边 {edge}，色调 {tone}"); // 勾选的卡片描边是色调色
        AssertNear(Drawing.Mix(colors.Surface, tone, 0.08), bitmap.GetPixel(basic.Width / 2, basic.Height - 6)); // 底色是色调 8% 混进 surface
        Click(pro);
        Assert.Equal("pro", plans.Value);
    });
}
