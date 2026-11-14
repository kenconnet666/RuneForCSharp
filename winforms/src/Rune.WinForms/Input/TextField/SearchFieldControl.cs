using System.ComponentModel;

namespace Rune.WinForms;

/// <summary>
/// 搜索框（vue-tsx-pro 的 SearchField）：TextFieldControl 加上搜索图标、清空按钮、默认的名称与占位文字（语言包的"搜索"），以及"要搜索了"的时机。
/// <code>
/// var search = new SearchFieldControl { PlaceholderText = "搜索零件" };
/// search.TextChanged += (_, _) => Filter(search.Text);                    // 本地过滤：每敲一个字
/// search.SearchRequested += (_, keyword) => query.Reload(keyword);        // 请求：停手 300ms、回车、清空才发
/// </code>
/// Text 每敲一个字都变（输入框里的草稿）；<see cref="SearchRequested"/> 只在该查的时候发：停手 <see cref="Delay"/>（默认 300ms，0 是每次输入都发）、
/// 按回车、清空（回车、清空立即），和上一次相同的词不再发。没在编辑时（没有焦点）改了 Text 算从外面改（重置、恢复保存的条件），不发。
/// </summary>
[ToolboxItem(true)]
[DefaultEvent(nameof(SearchRequested))]
[Description("搜索框：停手、回车、清空时触发搜索。")]
public class SearchFieldControl : TextFieldControl
{
    readonly System.Windows.Forms.Timer timer = new();
    // 上一次发出去的词：一样的不再发
    string searched = "";
    // 清空按钮正在清：焦点可能不在输入框里，也算自己改的
    bool clearing;

    /// <summary>新建：搜索图标、可清空、type 是 search；占位文字没写时取语言包。</summary>
    public SearchFieldControl()
    {
        Icon = Lucide.Search;
        Clearable = true;
        Type = TextFieldType.Search;
        timer.Tick += (_, _) => Search();
    }

    /// <summary>停手多久发 SearchRequested；0 是每次输入都发。</summary>
    [Category("Behavior")]
    public TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(300);

    bool ShouldSerializeDelay() => Delay != TimeSpan.FromMilliseconds(300);

    void ResetDelay() => Delay = TimeSpan.FromMilliseconds(300);

    /// <summary>要搜索了（TS 的 onSearch）：停手、回车、清空时发，参数是要搜的词。</summary>
    public event EventHandler<string>? SearchRequested;

    /// <summary>立即按现在的文字搜索（和上一次相同时不发）。</summary>
    public void Search()
    {
        timer.Stop();
        if (Text == searched) return;
        searched = Text;
        SearchRequested?.Invoke(this, Text);
    }

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        if (string.IsNullOrEmpty(PlaceholderText)) PlaceholderText = ProviderControl.LangOf(this).SelectSearch;
    }

    /// <inheritdoc />
    protected override void OnTextChanged(EventArgs e)
    {
        base.OnTextChanged(e);
        timer.Stop();
        // 不在编辑时是从外面改的值：记成已搜过，不发
        if (!ContainsFocus && !clearing) searched = Text;
        else if (Text.Length == 0 || Delay <= TimeSpan.Zero) Search();
        else
        {
            timer.Interval = Math.Max(1, (int)Delay.TotalMilliseconds);
            timer.Start();
        }
    }

    /// <summary>清空立即搜索。</summary>
    protected override void ClearText()
    {
        clearing = true;
        try
        {
            base.ClearText();
        }
        finally
        {
            clearing = false;
        }
    }

    /// <summary>回车立即搜索（按键从里面的 TextBox 转过来）。</summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode != Keys.Enter) return;
        e.SuppressKeyPress = true;
        Search();
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing) timer.Dispose();
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override AccessibleObject CreateAccessibilityInstance() => new SearchAccessibleObject(this);

    /// <summary>读屏：没写名称、也不在字段里时叫语言包的"搜索"（TS 的默认 aria-label）。</summary>
    sealed class SearchAccessibleObject(SearchFieldControl owner) : FieldAccessibleObject(owner)
    {
        public override string? Name => base.Name is { Length: > 0 } name ? name : ProviderControl.LangOf(owner).SelectSearch;
    }
}
