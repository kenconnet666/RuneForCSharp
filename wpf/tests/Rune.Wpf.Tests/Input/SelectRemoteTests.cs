using System.Windows;
using System.Windows.Controls;

namespace Rune.Wpf.Tests;

/// <summary>Select 的远程选项（Load）：打开才请求、搜索词防抖、只采用最后一次、已选项的文字留着、失败提示、分页。</summary>
public class SelectRemoteTests
{
    sealed record Person(string Id, string Name);

    static readonly Person[] People = [new("u1", "林小满"), new("u2", "周一航"), new("u3", "许知远")];

    static readonly Func<object?, ListboxOption> ToOption = item => item is Person person ? new ListboxOption(person.Id, person.Name) : ListboxOption.From(item, null);

    /// <summary>假的接口：记下每次的搜索词，按名字包含搜索词返回；FailNext 时下一次抛错。</summary>
    sealed class FakeApi
    {
        public List<string> Calls { get; } = [];

        public bool FailNext { get; set; }

        public Task<OptionsPage> Load(string query, int page, CancellationToken cancellation)
        {
            Calls.Add(query);
            if (FailNext)
            {
                FailNext = false;
                return Task.FromException<OptionsPage>(new InvalidOperationException("网络断了"));
            }
            return Task.FromResult(OptionsPage.All(People.Where(person => person.Name.Contains(query))));
        }
    }

    static StackPanel Host(UIElement child)
    {
        var host = new StackPanel();
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        host.Children.Add(child);
        return host;
    }

    static string[] Shown(Select select) => [.. select.List.State.Options.Select(entry => entry.Option.Label)];

    [Fact]
    [Upstream("Select", "关着不请求；打开立即按空搜索词请求，结果代替 items")]
    public Task Closed_does_not_load_and_opening_loads_with_an_empty_query() => WpfUi.Run(() =>
    {
        var api = new FakeApi();
        var select = new Select { Load = api.Load, ToOption = ToOption, Searchable = true, ItemsSource = new[] { new Person("u9", "本地") } };
        WpfUi.Show(Host(select));
        WpfUi.Wait(TimeSpan.FromMilliseconds(50));
        Assert.Empty(api.Calls);

        select.IsOpen = true;
        WpfUi.Until(() => Shown(select).Length == 3);
        Assert.Equal([""], api.Calls);
        Assert.Equal(["林小满", "周一航", "许知远"], Shown(select));
    });

    [Fact]
    [Upstream("Select", "搜索词停下 300 ms 再请求，只采用最后一次；选中的项换出结果后触发框仍显示文字")]
    public Task Typing_waits_for_a_pause_and_the_selected_label_survives_new_results() => WpfUi.Run(() =>
    {
        var api = new FakeApi();
        var select = new Select { Load = api.Load, ToOption = ToOption, Searchable = true };
        WpfUi.Show(Host(select));
        select.IsOpen = true;
        WpfUi.Until(() => Shown(select).Length == 3);
        select.Value = "u1";

        // 连着输入两次：防抖期间的那次不请求（等待的时长交给 Avalonia 的同一用例，WPF 的计时器在 CI 上不准）
        select.SearchBox.Text = "周";
        select.SearchBox.Text = "周一";
        WpfUi.Until(() => api.Calls.Count == 2 && Shown(select).Length == 1);
        WpfUi.Wait(TimeSpan.FromMilliseconds(350));
        Assert.Equal(["", "周一"], api.Calls);
        Assert.Equal(["周一航"], Shown(select));
        Assert.Equal("林小满", ((TextBlock)select.Template.FindName(SelectParts.Value, select)).Text);
    });

    [Fact]
    [Upstream("Select", "加载失败：列表里提示“加载失败，请重试”")]
    public Task A_failed_load_says_so_in_the_list() => WpfUi.Run(() =>
    {
        var api = new FakeApi { FailNext = true };
        var select = new Select { Load = api.Load, ToOption = ToOption };
        WpfUi.Show(Host(select));
        select.IsOpen = true;
        WpfUi.Until(() => select.List.Empty is Empty);
        Assert.Equal("加载失败，请重试", ((Empty)select.List.Empty!).Description);
    });

    [Fact]
    public Task Scrolling_near_the_end_loads_the_next_page() => WpfUi.Run(() =>
    {
        var pages = new List<int>();
        Task<OptionsPage> Load(string query, int page, CancellationToken cancellation)
        {
            pages.Add(page);
            var items = Enumerable.Range((page - 1) * 20, 20).Select(index => (object?)$"供应商 {index:000}").ToList();
            return Task.FromResult(new OptionsPage(items, page < 3 ? page + 1 : null));
        }
        var select = new Select { Load = Load };
        WpfUi.Show(Host(select));
        select.IsOpen = true;
        WpfUi.Until(() => select.List.State.Options.Count == 20);
        WpfUi.Flush();
        Assert.Equal([1], pages);

        var scroll = WpfUi.Descendants(select.List).OfType<ScrollViewer>().First();
        // 滚到此刻的末尾（ScrollToBottom 会一直粘在末尾，加了行还在末尾，又取下一页）
        scroll.ScrollToVerticalOffset(scroll.ScrollableHeight);
        // 指针要是停在弹出层上，高亮跟着指针、滚进来，可能接着取第三页：只看取到了下一页
        WpfUi.Until(() => select.List.State.Options.Count >= 40);
        Assert.Equal([1, 2], pages.Take(2));
    });
}
