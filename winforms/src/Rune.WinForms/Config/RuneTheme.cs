namespace Rune.WinForms;

/// <summary>
/// 应用级的主题（WinForms 没有资源系统，主题是信号，DESIGN §4、§10）：元素绘制时读 <see cref="Current"/>，
/// 换主题时只有读过它的元素重画。
/// <code>
/// RuneTheme.Current.Value = BuiltinThemes.Dark;
/// </code>
/// 局部主题（Provider 作用域）做 Provider 时再加，在这之上按 Parent 链取最近的一层。
/// </summary>
public static class RuneTheme
{
    /// <summary>当前的主题，默认亮色。</summary>
    public static Signal<ThemeTokens> Current { get; } = new(BuiltinThemes.Light);
}
