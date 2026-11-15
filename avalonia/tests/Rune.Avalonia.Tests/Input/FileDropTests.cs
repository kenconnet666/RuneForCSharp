using System.Reflection;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Platform.Storage;

namespace Rune.Avalonia.Tests;

/// <summary>FileDrop：accept 的匹配；交出的文件按 accept 分成接受与拒绝两份，单选时只取第一个；文件夹递归读出文件、带相对路径；提示与说明两个部件。</summary>
public class FileDropTests
{
    /// <summary>假的文件、文件夹：只有名字与子项（DispatchProxy 实现 IStorageFile、IStorageFolder 的接口）。</summary>
    public class FakeItem : DispatchProxy
    {
        string name = "";
        IStorageItem[] children = [];

        public static IStorageFile File(string name)
        {
            var file = DispatchProxy.Create<IStorageFile, FakeItem>();
            ((FakeItem)(object)file).name = name;
            return file;
        }

        public static IStorageFolder Folder(string name, params IStorageItem[] children)
        {
            var folder = DispatchProxy.Create<IStorageFolder, FakeItem>();
            var fake = (FakeItem)(object)folder;
            fake.name = name;
            fake.children = children;
            return folder;
        }

        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
        {
            "get_Name" => name,
            "GetItemsAsync" => Items(),
            _ => throw new NotSupportedException(method?.Name),
        };

        async IAsyncEnumerable<IStorageItem> Items()
        {
            foreach (var child in children)
            {
                await Task.Yield();
                yield return child;
            }
        }
    }

    [Fact]
    [Upstream("Upload", "扩展名、通配类型、确切类型；不写 accept 时都接受")]
    public void Accept_matches_extensions_wildcards_and_exact_types()
    {
        Assert.True(FileAccept.Matches("a.PDF", ".pdf"));
        Assert.True(FileAccept.Matches("a.png", "image/*, .pdf"));
        Assert.False(FileAccept.Matches("a.txt", "image/*,application/json"));
        Assert.True(FileAccept.Matches("a.txt", null));
        Assert.Contains(".png", FileAccept.Extensions("image/*"));
        Assert.Equal([".pdf", ".json"], FileAccept.Extensions(".pdf, application/json"));
    }

    [Fact]
    [Upstream("FileDrop", "拖进来的文件按 accept 分成接受与拒绝两份；单选时只取第一个")]
    public Task Splits_accepted_and_rejected_and_takes_one_when_single() => HeadlessUi.Run(() =>
    {
        var drop = new FileDrop { Accept = "image/*" };
        HeadlessUi.Show(drop);
        IReadOnlyList<PickedFile>? picked = null, rejected = null;
        drop.FilesPicked += (_, e) => picked = e.Files;
        drop.Rejected += (_, e) => rejected = e.Files;
        drop.Deliver([new(FakeItem.File("a.png"), "a.png"), new(FakeItem.File("b.jpg"), "b.jpg"), new(FakeItem.File("c.txt"), "c.txt")]);
        Assert.Equal(["a.png"], picked!.Select(file => file.Name));
        Assert.Equal(["c.txt"], rejected!.Select(file => file.Name));
        drop.Multiple = true;
        drop.Deliver([new(FakeItem.File("a.png"), "a.png"), new(FakeItem.File("b.jpg"), "b.jpg")]);
        Assert.Equal(["a.png", "b.jpg"], picked!.Select(file => file.Name));
    });

    [Fact]
    [Upstream("FileDrop", "directory：对话框选文件夹（webkitdirectory、多选）；拖进来的文件夹递归读出文件，补上相对路径")]
    public async Task Folders_are_read_recursively_with_relative_paths()
    {
        var tree = FakeItem.Folder("报告", FakeItem.File("正文.pdf"), FakeItem.Folder("附图", FakeItem.File("图 1.png")));
        var files = await FileDrop.Collect([tree], folders: true);
        Assert.Equal(["报告/正文.pdf", "报告/附图/图 1.png"], files.Select(file => file.RelativePath));
        // 不是选文件夹时，拖进来的文件夹不展开
        Assert.Empty(await FileDrop.Collect([tree], folders: false));
    }

    [Fact]
    [Upstream("FileDrop", "部件属性：提示文字与说明是两个部件")]
    public Task Hint_and_description_are_parts() => HeadlessUi.Run(() =>
    {
        var drop = new FileDrop { Description = "最大 5 MB" };
        Provider.SetLang(drop, Lang.ZhCn);
        HeadlessUi.Show(drop);
        var texts = drop.GetLogicalDescendants().OfType<Text>().ToDictionary(text => text.Name!, text => text.Text);
        Assert.Equal("点击或拖放文件到这里", texts[FileDropParts.Hint]);
        Assert.Equal("最大 5 MB", texts[FileDropParts.Description]);
        Assert.Equal((AutomationControlType.Button, "点击或拖放文件到这里"), (drop.Peer().GetAutomationControlType(), drop.Peer().GetName()));
    });

    [Fact]
    public Task Read_only_locks_the_area() => HeadlessUi.Run(() =>
    {
        var drop = new FileDrop();
        var field = new Field { Label = "附件", IsReadOnly = true, Content = drop };
        HeadlessUi.Show(field);
        Assert.Contains(":locked", drop.Classes);
        Assert.False(drop.Peer().IsEnabled());
        Assert.Equal("附件", drop.Peer().GetName());
    });
}
