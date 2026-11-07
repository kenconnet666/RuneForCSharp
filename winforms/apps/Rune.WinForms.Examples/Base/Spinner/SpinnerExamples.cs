namespace Rune.WinForms.Examples;

/// <summary>Spinner 的示例（vue-tsx-pro 文档站 feedback/Progress 页的 spinner 一节）。WinForms 里放进窗体的是 SpinnerControl。</summary>
static class SpinnerExamples
{
    public static Example[] All =>
    [
        new("Progress", "spinner", "旋转指示", "SpinnerControl 默认跟随字体大小与 ForeColor，旁边有文字时是装饰；单独出现时写 Label，读屏当作进度条念出来。", Spinners),
    ];

    static Control Spinners() => Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg },
        Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Lg, Wrap = true },
        [
            .. Build.Steps.Select(size => new SpinnerControl { IconSize = size }),
            FollowText(TextTone.Accent, "跟随文字颜色"),
            FollowText(TextTone.Success, "同步中", TextSize.Sm),
        ]),
        Build.Card(new CardControl(), Build.Flex(new FlexControl { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Md },
            // 单独出现、旁边没有文字时写 Label：读屏当作进度条念出来
            Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm },
                new SpinnerControl { Label = "正在生成报表" },
                new SmallControl { Text = "正在生成报表，大约需要 30 秒", Tone = TextTone.Muted }),
            new ButtonControl { Text = "取消", TextSize = TextSize.Sm, Variant = ButtonVariant.Ghost })));

    /// <summary>与文字同一行：颜色、大小跟随这一行的字体与 ForeColor（TS 里是外面包一层 Text）。</summary>
    static Control FollowText(TextTone tone, string text, TextSize size = default) =>
        Build.Styled(Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Xs }, new SpinnerControl(), new TextControl { Text = text }), size, tone);
}
