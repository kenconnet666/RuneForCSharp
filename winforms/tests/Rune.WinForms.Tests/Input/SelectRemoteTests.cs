namespace Rune.WinForms.Tests;

/// <summary>SelectControl 的远程选项（Load）：打开才请求、搜索词防抖、只采用最后一次、已选项的文字留着、失败提示、分页。</summary>
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

    /// <summary>边处理消息边等，直到条件成立；超过 3 秒算失败。</summary>
    static void WithSelect(SelectControl select, Action body)
    {
        using var form = new Form
        {
            StartPosition = FormStartPosition.Manual, Location = new Point(300, 300), Size = new Size(500, 400),
            FormBorderStyle = FormBorderStyle.None, ShowInTaskbar = false,
        };
        select.Dock = DockStyle.Top;
        form.Controls.Add(select);
        form.Show();
        WinFormsUi.Pump();
        try
        {
            body();
        }
        finally
        {
            select.IsOpen = false;
        }
    }

    static string[] Shown(SelectControl select) => [.. select.List.State.Options.Select(entry => entry.Option.Label)];

    [Fact]
    [Upstream("Select", "关着不请求；打开立即按空搜索词请求，结果代替 items")]
    public Task Closed_does_not_load_and_opening_loads_with_an_empty_query() => WinFormsUi.Run(() =>
    {
        var api = new FakeApi();
        var select = new SelectControl { Load = api.Load, ToOption = ToOption, Searchable = true, ItemsSource = new[] { new Person("u9", "本地") } };
        WithSelect(select, () =>
        {
            WinFormsUi.Wait(TimeSpan.FromMilliseconds(50));
            Assert.Empty(api.Calls);

            select.IsOpen = true;
            WinFormsUi.Until(() => Shown(select).Length == 3);
            Assert.Equal([""], api.Calls);
            Assert.Equal(["林小满", "周一航", "许知远"], Shown(select));
        });
    });

    [Fact]
    [Upstream("Select", "搜索词停下 300 ms 再请求，只采用最后一次；选中的项换出结果后触发框仍显示文字")]
    public Task Typing_waits_for_a_pause_and_the_selected_label_survives_new_results() => WinFormsUi.Run(() =>
    {
        var api = new FakeApi();
        var select = new SelectControl { Load = api.Load, ToOption = ToOption, Searchable = true };
        WithSelect(select, () =>
        {
            select.IsOpen = true;
            WinFormsUi.Until(() => Shown(select).Length == 3);
            select.Value = "u1";

            // 连着输入两次：防抖期间的那次不请求（等待的时长交给 Avalonia 的同一用例）
            select.SearchBox.Text = "周";
            select.SearchBox.Text = "周一";
            WinFormsUi.Until(() => api.Calls.Count == 2 && Shown(select).Length == 1);
            WinFormsUi.Wait(TimeSpan.FromMilliseconds(350));
            Assert.Equal(["", "周一"], api.Calls);
            Assert.Equal(["周一航"], Shown(select));
            Assert.Equal("林小满", select.ValuePart.Text);
        });
    });

    [Fact]
    [Upstream("Select", "加载失败：列表里提示“加载失败，请重试”")]
    public Task A_failed_load_says_so_in_the_list() => WinFormsUi.Run(() =>
    {
        var api = new FakeApi { FailNext = true };
        var select = new SelectControl { Load = api.Load, ToOption = ToOption };
        WithSelect(select, () =>
        {
            select.IsOpen = true;
            WinFormsUi.Until(() => select.List.EmptyText is not null);
            Assert.Equal("加载失败，请重试", select.List.EmptyText);
        });
    });

    [Fact]
    public Task Scrolling_near_the_end_loads_the_next_page() => WinFormsUi.Run(() =>
    {
        var pages = new List<int>();
        Task<OptionsPage> Load(string query, int page, CancellationToken cancellation)
        {
            pages.Add(page);
            var items = Enumerable.Range((page - 1) * 20, 20).Select(index => (object?)$"供应商 {index:000}").ToList();
            return Task.FromResult(new OptionsPage(items, page < 3 ? page + 1 : null));
        }
        var select = new SelectControl { Load = Load };
        WithSelect(select, () =>
        {
            select.IsOpen = true;
            WinFormsUi.Until(() => select.List.State.Options.Count == 20);
            WinFormsUi.Pump();
            Assert.Equal([1], pages);

            var scroll = select.List.Controls.OfType<ScrollAreaControl>().First();
            scroll.ScrollOffset = new Point(0, 100000);
            WinFormsUi.Until(() => select.List.State.Options.Count >= 40);
            Assert.Equal([1, 2], pages.Take(2));
        });
    });
}
