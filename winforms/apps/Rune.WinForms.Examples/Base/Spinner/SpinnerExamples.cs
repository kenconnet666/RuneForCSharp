using static Rune.WinForms.Examples.Layout;

namespace Rune.WinForms.Examples;

/// <summary>Spinner 的示例（vue-tsx-pro 文档站 feedback/Progress 页的 spinner 一节）。WinForms 里放进窗体的是 SpinnerControl。</summary>
static class SpinnerExamples
{
    public static Example[] All =>
    [
        new("Spinner", "spinner", "旋转指示", "SpinnerControl 默认跟随字体大小与 ForeColor，旁边有文字时是装饰；单独出现时写 Label，读屏当作进度条念出来。", Spinners),
    ];

    static Control Spinners()
    {
        var sizes = Wrap(24,
        [
            .. Steps.Select(size => new SpinnerControl { IconSize = size, Anchor = AnchorStyles.None }),
            FollowText(TextTone.Accent, "跟随文字颜色", null),
            FollowText(TextTone.Success, "同步中", TextSize.Sm),
        ]);
        var card = Row(8, new SpinnerControl { Label = "正在生成报表", Anchor = AnchorStyles.None },
            new SmallControl { Text = "正在生成报表，大约需要 30 秒", Tone = TextTone.Muted, Anchor = AnchorStyles.None });
        card.Padding = new Padding(Dip(16));
        card.MinimumSize = new Size(Dip(420), 0);
        card.BackColor = RuneTheme.Current.Peek().Colors.Surface;
        card.BorderStyle = BorderStyle.FixedSingle;
        return Column(24, sizes, card);
    }

    /// <summary>与文字同一行：颜色、大小跟随这一行的字体与 ForeColor（Text 的 Tone 只管文字，这里给整行设颜色）。</summary>
    static Control FollowText(TextTone tone, string text, TextSize? size)
    {
        var row = Row(4, new SpinnerControl { Anchor = AnchorStyles.None }, new TextControl { Text = text, Anchor = AnchorStyles.None });
        row.ForeColor = tone.ColorIn(new ElementContext(row.DeviceDpi, row.Font, row.ForeColor, false));
        if (size?.Pixels is { } pixels) row.Font = new Font(Fonts.Installed(RuneTheme.Current.Peek().Fonts.Sans), pixels * 72 / 96);
        return row;
    }
}
