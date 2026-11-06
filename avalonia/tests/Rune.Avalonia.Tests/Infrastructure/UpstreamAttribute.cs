namespace Rune.Avalonia.Tests;

/// <summary>
/// 这个测试对应 vue-tsx-pro 的哪个测试用例（组件名与 it 名字的开头），tools/parity/behavior.mjs 据此对照行为的覆盖。
/// 名字只写开头一段（够区分即可、不带引号），上游改了措辞时对照报出来。
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
sealed class UpstreamAttribute(string component, string test) : Attribute
{
    /// <summary>vue-tsx-pro 的组件名（测试文件 .test/&lt;组件&gt;.test.tsx）。</summary>
    public string Component { get; } = component;

    /// <summary>it 名字的开头。</summary>
    public string Test { get; } = test;
}
