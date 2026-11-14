using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Rune.Wpf;

/// <summary>
/// 搜索框（vue-tsx-pro 的 SearchField）：TextField 加上搜索图标、清空按钮、默认的名称与占位文字（语言包的"搜索"），以及"要搜索了"的时机。
/// <code>
/// &lt;rune:SearchField Text="{Binding Keyword, UpdateSourceTrigger=PropertyChanged}" /&gt;   &lt;!-- 本地过滤：每敲一个字都用 Keyword 过滤 --&gt;
/// &lt;rune:SearchField SearchRequested="OnSearch" PlaceholderText="搜索零件" /&gt;                &lt;!-- 请求：停手 300ms、回车、清空才发 --&gt;
/// </code>
/// Text 每敲一个字都更新（输入框里的草稿）；<see cref="SearchRequested"/> 只在该查的时候发：停手 <see cref="Delay"/>（默认 300ms，0 是每次输入都发）、
/// 按回车、清空（回车、清空立即），和上一次相同的词不再发。没在编辑时（没有焦点）改了 Text 算从外面改（重置、恢复保存的条件），不发。
/// 外观同 TextField（用 TextField 的样式）；下拉、表头筛选里的搜索框都是它。
/// </summary>
public class SearchField : TextField
{
    /// <summary>停手多久发 SearchRequested；0 是每次输入都发。</summary>
    public static readonly DependencyProperty DelayProperty = DependencyProperty.Register(
        nameof(Delay), typeof(TimeSpan), typeof(SearchField), new PropertyMetadata(TimeSpan.FromMilliseconds(300)));

    readonly DispatcherTimer timer = new();
    // 上一次发出去的词：一样的不再发
    string searched = "";
    // 清空按钮正在清：焦点可能不在输入框里，也算自己改的
    bool clearing;

    static SearchField()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(SearchField), new FrameworkPropertyMetadata(typeof(TextField)));
        IconProperty.OverrideMetadata(typeof(SearchField), new FrameworkPropertyMetadata(Lucide.Search));
        ClearableProperty.OverrideMetadata(typeof(SearchField), new FrameworkPropertyMetadata(true));
        TypeProperty.OverrideMetadata(typeof(SearchField), new FrameworkPropertyMetadata(TextFieldType.Search));
    }

    /// <summary>新建：占位文字没写时在加载后取语言包。</summary>
    public SearchField()
    {
        timer.Tick += (_, _) => Search();
        Loaded += (_, _) =>
        {
            if (ReadLocalValue(PlaceholderTextProperty) == DependencyProperty.UnsetValue && string.IsNullOrEmpty(PlaceholderText))
                SetCurrentValue(PlaceholderTextProperty, Provider.GetLang(this).SelectSearch);
        };
        Unloaded += (_, _) => timer.Stop();
    }

    /// <summary>停手多久发 SearchRequested。</summary>
    public TimeSpan Delay { get => (TimeSpan)GetValue(DelayProperty); set => SetValue(DelayProperty, value); }

    /// <summary>要搜索了（TS 的 onSearch）：停手、回车、清空时发，参数是要搜的词。</summary>
    public event EventHandler<string>? SearchRequested;

    /// <summary>立即按现在的文字搜索（和上一次相同时不发）。</summary>
    public void Search()
    {
        timer.Stop();
        var text = Text ?? "";
        if (text == searched) return;
        searched = text;
        SearchRequested?.Invoke(this, text);
    }

    /// <inheritdoc />
    protected override void OnTextChanged(TextChangedEventArgs e)
    {
        base.OnTextChanged(e);
        var text = Text ?? "";
        timer.Stop();
        // 不在编辑时是从外面改的值：记成已搜过，不发
        if (!IsKeyboardFocusWithin && !clearing) searched = text;
        else if (text.Length == 0 || Delay <= TimeSpan.Zero) Search();
        else
        {
            timer.Interval = Delay;
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

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Enter) Search();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new SearchFieldPeer(this);

    /// <summary>读屏：没写名称、也不在字段里时叫语言包的"搜索"（TS 的默认 aria-label）。</summary>
    sealed class SearchFieldPeer(SearchField owner) : TextInputPeer(owner)
    {
        protected override string GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : Provider.GetLang(owner).SelectSearch;
    }
}
