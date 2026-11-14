using Avalonia.Controls;
using Avalonia.Input;

namespace Rune.Avalonia.Tests;

/// <summary>SearchField：默认搜索图标、可清空、名称"搜索"；编辑中停手（Delay 0 时每次）、回车、清空才发 SearchRequested，同一个词不重复；从外面改值不发。</summary>
public class SearchFieldTests
{
    [Fact]
    public Task Searches_when_typing_pausing_enter_and_clearing() => HeadlessUi.Run(() =>
    {
        var field = new SearchField();
        var other = new TextBox();
        HeadlessUi.Show(new StackPanel { Children = { field, other } });
        var searched = new List<string>();
        field.SearchRequested += (_, text) => searched.Add(text);

        Assert.Equal(Lucide.Search, field.Icon);
        Assert.True(field.Clearable);
        Assert.Equal(Provider.GetLang(field).SelectSearch, field.PlaceholderText);
        Assert.Equal(Provider.GetLang(field).SelectSearch, field.Peer().GetName());

        field.Text = "支架"; // 没有焦点：从外面改的，不发
        Assert.Empty(searched);

        field.Focus();
        field.Text = "底座"; // 默认等 300ms：还没发
        Assert.Empty(searched);
        field.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });
        Assert.Equal(["底座"], searched);
        field.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); // 同一个词不重复
        Assert.Single(searched);

        field.Delay = TimeSpan.Zero;
        field.Text = "盖板";
        Assert.Equal("盖板", searched[^1]);
        field.Text = ""; // 清空立即发
        Assert.Equal("", searched[^1]);
    });
}
