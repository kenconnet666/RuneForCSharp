using System.Windows.Controls;
using System.Windows.Input;

namespace Rune.Wpf.Tests;

/// <summary>SearchField：默认搜索图标、可清空、名称"搜索"；编辑中停手（Delay 0 时每次）、回车、清空才发 SearchRequested，同一个词不重复；从外面改值不发。</summary>
public class SearchFieldTests
{
    [Fact]
    [Upstream("SearchField", "打字过滤列表；回车立即触发 onSearch；清空按钮清掉并触发")]
    [Upstream("SearchField", "type=")]
    [Upstream("SearchField", "onSearch：打字等停手，回车立即，清空立即；同一个词不重复")]
    [Upstream("SearchField", "父级从外面改了值（重置）：不触发 onSearch，之后再输入同样的词照常触发")]
    public Task Searches_when_typing_pausing_enter_and_clearing() => WpfUi.Run(() =>
    {
        var field = new SearchField();
        var host = new StackPanel { Children = { field, new TextBox() } };
        host.Resources.MergedDictionaries.Add(new RuneTheme());
        WpfUi.Show(host);
        var searched = new List<string>();
        field.SearchRequested += (_, text) => searched.Add(text);

        Assert.Equal(Lucide.Search, field.Icon);
        Assert.True(field.Clearable);
        Assert.Equal(Provider.GetLang(field).SelectSearch, field.PlaceholderText);
        Assert.Equal(Provider.GetLang(field).SelectSearch, field.Peer().GetName());

        field.Text = "支架"; // 没有焦点：从外面改的，不发
        Assert.Empty(searched);

        Keyboard.Focus(field);
        field.Text = "底座"; // 默认等 300ms：还没发
        Assert.Empty(searched);
        field.Search(); // 回车同样立即搜索
        Assert.Equal(["底座"], searched);
        field.Search(); // 同一个词不重复
        Assert.Single(searched);

        field.Delay = TimeSpan.Zero;
        field.Text = "盖板";
        Assert.Equal("盖板", searched[^1]);
        field.Text = ""; // 清空立即发
        Assert.Equal("", searched[^1]);

        // 搜过“盖板”后父级从外面重置：不发；之后再输入“盖板”照常发
        field.Text = "盖板";
        var count = searched.Count;
        Keyboard.ClearFocus();
        field.Text = "";
        Assert.Equal(count, searched.Count);
        Keyboard.Focus(field);
        field.Text = "盖板";
        Assert.Equal((count + 1, "盖板"), (searched.Count, searched[^1]));
    });
}
