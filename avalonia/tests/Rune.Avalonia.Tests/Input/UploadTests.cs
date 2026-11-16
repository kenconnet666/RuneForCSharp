using System.Reflection;
using Avalonia.Automation;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;

namespace Rune.Avalonia.Tests;

/// <summary>
/// Upload：选到就上传（进度、完成、失败重试）；父级放进来的待上传文件也上传；移除时取消；超过大小的列出错误、单选替换；部件；已有文件；
/// Field 的只读、不可用、必填；图片墙的缩略图与"添加图片"；粘贴与把手的键盘排序。
/// </summary>
public class UploadTests
{
    /// <summary>假的文件：名字、大小与地址（DispatchProxy 实现 IStorageFile）。</summary>
    public class FakeFile : DispatchProxy
    {
        string name = "";
        ulong size;

        public static IStorageFile Create(string name, ulong size = 1)
        {
            var file = DispatchProxy.Create<IStorageFile, FakeFile>();
            var fake = (FakeFile)(object)file;
            (fake.name, fake.size) = (name, size);
            return file;
        }

        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
        {
            "get_Name" => name,
            "get_Path" => new Uri($"file:///uploads/{Uri.EscapeDataString(name)}"),
            "GetBasicPropertiesAsync" => Task.FromResult(new StorageItemProperties(size, null, null)),
            "Dispose" => null,
            _ => throw new NotSupportedException(method?.Name),
        };
    }

    static PickedFile Picked(string name, ulong size = 1) => new(FakeFile.Create(name, size), name);

    static void Drop(Upload upload, params PickedFile[] files) => upload.DropPart.Deliver(files);

    static T Named<T>(Control root, string name) where T : Control =>
        root.GetVisualDescendants().OfType<T>().First(control => AutomationProperties.GetName(control) == name);

    static void Click(Control button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    /// <summary>上传函数：记下每次调用，测试自己决定成功还是失败。</summary>
    sealed class Calls
    {
        public List<(TaskCompletionSource<object?> Done, CancellationToken Cancellation)> All { get; } = [];

        public Task<object?> Upload(IStorageFile file, IProgress<double> progress, CancellationToken cancellation)
        {
            progress.Report(0.5);
            var done = new TaskCompletionSource<object?>();
            All.Add((done, cancellation));
            return done.Task;
        }
    }

    [Fact]
    [Upstream("Upload", "选到文件就上传：进度、完成后记下结果；失败时可以重试")]
    public Task Uploads_report_progress_and_failures_can_retry() => HeadlessUi.Run(() =>
    {
        var calls = new Calls();
        var upload = new Upload { Multiple = true, Uploader = calls.Upload };
        var window = HeadlessUi.Show(upload);
        Drop(upload, Picked("a.txt"), Picked("b.txt"));
        HeadlessUi.Until(() => upload.Files.Count == 2 && upload.Files.All(item => item.Progress == 0.5));
        Assert.Equal([UploadStatus.Uploading, UploadStatus.Uploading], upload.Files.Select(item => item.Status));
        Assert.Equal(2, upload.GetVisualDescendants().OfType<Progress>().Count());

        calls.All[0].Done.SetResult("/a.txt");
        calls.All[1].Done.SetException(new InvalidOperationException("网络错误"));
        HeadlessUi.Until(() => upload.Files[1].Status == UploadStatus.Error);
        Assert.Equal((UploadStatus.Done, (object?)"/a.txt"), (upload.Files[0].Status, upload.Files[0].Response));
        Assert.Equal("网络错误", upload.Files[1].Error);

        Click(Named<IconButton>(upload, "重新上传 b.txt"));
        HeadlessUi.Until(() => upload.Files[1].Status == UploadStatus.Uploading);
        calls.All[2].Done.SetResult(null);
        HeadlessUi.Until(() => upload.Files[1].Status == UploadStatus.Done);
        HeadlessUi.Flush(window);
    });

    [Fact]
    [Upstream("Upload", "父级放进来的“已选择”文件（带 file）在给了 upload 时也开始上传，只上传一次")]
    public Task Selected_files_from_the_parent_upload_once() => HeadlessUi.Run(() =>
    {
        var calls = new Calls();
        var upload = new Upload { Multiple = true, Files = [new UploadFile("draft", "draft.txt", UploadStatus.Selected) { File = FakeFile.Create("draft.txt") }] };
        upload.Uploader = calls.Upload;
        HeadlessUi.Show(upload);
        HeadlessUi.Until(() => upload.Files[0].Status == UploadStatus.Uploading);
        Assert.Single(calls.All);
    });

    [Fact]
    [Upstream("Upload", "移除正在上传的文件时中止")]
    public Task Removing_cancels_the_upload() => HeadlessUi.Run(() =>
    {
        var calls = new Calls();
        var upload = new Upload { Uploader = calls.Upload };
        HeadlessUi.Show(upload);
        Drop(upload, Picked("a.txt"));
        HeadlessUi.Until(() => calls.All.Count == 1);
        Click(Named<IconButton>(upload, "移除 a.txt"));
        Assert.True(calls.All[0].Cancellation.IsCancellationRequested);
        Assert.Empty(upload.Files);
    });

    [Fact]
    [Upstream("Upload", "超过 maxSize 的文件列出错误、不上传；单选时新文件替换旧的")]
    public Task Too_large_files_are_rejected_and_single_replaces() => HeadlessUi.Run(() =>
    {
        var calls = new Calls();
        var upload = new Upload { MaxSize = 10, Uploader = calls.Upload };
        HeadlessUi.Show(upload);
        Drop(upload, Picked("big.txt", 2000));
        HeadlessUi.Until(() => upload.Files.Count == 1);
        Assert.True(upload.Files[0] is { Status: UploadStatus.Error, Rejected: true });
        Assert.Contains("10", upload.Files[0].Error);
        Assert.Empty(calls.All);

        Drop(upload, Picked("small.txt"));
        HeadlessUi.Until(() => upload.Files[0].Name == "small.txt" && calls.All.Count == 1);
        Assert.Single(upload.Files);
    });

    [Fact]
    [Upstream("Upload", "部件属性：每一项的 body、文件名、大小或错误、进度条；进度与名称由组件决定")]
    public Task Rows_show_name_meta_and_progress() => HeadlessUi.Run(() =>
    {
        var upload = new Upload
        {
            Files =
            [
                new UploadFile("a", "a.txt", UploadStatus.Uploading) { File = FakeFile.Create("a.txt"), Progress = 0.5 },
                new UploadFile("b", "b.txt", UploadStatus.Error) { File = FakeFile.Create("b.txt"), Error = "网络错误" },
            ],
        };
        HeadlessUi.Show(upload);
        Assert.Equal(2, upload.GetVisualDescendants().OfType<Control>().Count(control => control.Name == UploadParts.Body));
        Assert.Equal(["a.txt", "b.txt"], upload.GetVisualDescendants().OfType<Text>().Where(text => text.Name == UploadParts.Name).Select(text => text.Text));
        Assert.Equal("网络错误", upload.GetVisualDescendants().OfType<Text>().Single(text => text.Name == UploadParts.Meta).Text);
        var progress = upload.GetVisualDescendants().OfType<Progress>().Single();
        Assert.Equal(50, progress.Value);
        Assert.Equal("a.txt", AutomationProperties.GetName(progress));
    });

    [Fact]
    [Upstream("Upload", "部件属性：文件图标、重试、移除；每项的名称、点击与不可用由组件管理")]
    public Task Retry_and_remove_follow_disabled() => HeadlessUi.Run(() =>
    {
        var calls = new Calls();
        var upload = new Upload
        {
            Uploader = calls.Upload, IsEnabled = false,
            Files = [new UploadFile("a", "a.txt", UploadStatus.Error) { File = FakeFile.Create("a.txt"), Error = "网络错误" }],
        };
        var window = HeadlessUi.Show(upload);
        Assert.Contains(upload.GetVisualDescendants().OfType<Icon>(), icon => icon.Name == UploadParts.Icon);
        Assert.False(Named<IconButton>(upload, "移除 a.txt").IsEffectivelyEnabled);
        // 名称带文件名：列表里每一项的按钮读屏能区分
        var retry = Named<IconButton>(upload, "重新上传 a.txt");
        Assert.False(retry.IsEffectivelyEnabled);
        upload.IsEnabled = true;
        HeadlessUi.Flush(window);
        Click(Named<IconButton>(upload, "重新上传 a.txt"));
        HeadlessUi.Until(() => upload.Files[0].Status == UploadStatus.Uploading);
        Assert.Single(calls.All);
    });

    [Fact]
    [Upstream("Upload", "已有文件：状态是完成，文件名链到地址；不重新上传，可以移除")]
    public Task Existing_files_link_and_can_be_removed() => HeadlessUi.Run(() =>
    {
        var calls = new Calls();
        var upload = new Upload
        {
            Uploader = calls.Upload, Multiple = true,
            Files = [UploadFile.Existing("验证报告.pdf", url: new Uri("https://example.com/files/7")), UploadFile.Existing("评审纪要.pdf", size: 2048)],
        };
        HeadlessUi.Show(upload);
        Assert.All(upload.Files, item => Assert.Equal(UploadStatus.Done, item.Status));
        Assert.Equal(new Uri("https://example.com/files/7"), upload.GetVisualDescendants().OfType<Link>().Single().Href);
        Assert.Contains(upload.GetVisualDescendants().OfType<Text>(), text => text.Text == "2 kB");
        Assert.Empty(calls.All);
        Click(Named<IconButton>(upload, "移除 验证报告.pdf"));
        Assert.Equal(["评审纪要.pdf"], upload.Files.Select(item => item.Name));
    });

    [Fact]
    [Upstream("Upload", "读 Field 的只读与不可用；已有文件时选择框不再原生必填（它选完就清空，否则表单交不了）")]
    public Task Field_states_reach_the_drop_zone() => HeadlessUi.Run(() =>
    {
        IReadOnlyList<UploadFile> files = [new UploadFile("a", "a.txt", UploadStatus.Done) { File = FakeFile.Create("a.txt") }];
        var readOnly = new Upload { Files = files };
        var disabled = new Upload { Files = files };
        var filled = new Upload { Files = files };
        var empty = new Upload();
        HeadlessUi.Show(new StackPanel
        {
            Children =
            {
                new Field { Label = "附件", IsReadOnly = true, Content = readOnly },
                new Field { Label = "附件", IsEnabled = false, Content = disabled },
                new Field { Label = "附件", IsRequired = true, Content = filled },
                new Field { Label = "附件", IsRequired = true, Content = empty },
            },
        });
        Assert.False(readOnly.DropPart.IsVisible);
        Assert.Empty(readOnly.GetVisualDescendants().OfType<IconButton>());
        Assert.False(disabled.DropPart.IsEffectivelyEnabled);
        Assert.False(disabled.GetVisualDescendants().OfType<IconButton>().Single().IsEffectivelyEnabled);
        Assert.False(filled.DropPart.IsRequired);
        Assert.True(empty.DropPart.IsRequired);
    });

    [Fact]
    [Upstream("Upload", "picture：图片是可以点开的缩略图（本地地址），别的文件是图标与文件名；选文件的区域是最后一格")]
    public Task Picture_wall_shows_thumbnails_and_the_add_card_last() => HeadlessUi.Run(() =>
    {
        var upload = new Upload { ListType = UploadListType.Picture, Multiple = true, Accept = "image/*,.pdf" };
        HeadlessUi.Show(upload);
        Drop(upload, Picked("支架.png"), Picked("报告.pdf"));
        HeadlessUi.Until(() => upload.Files.Count == 2);
        var wall = upload.GetVisualDescendants().OfType<ImageGroup>().Single();
        var cards = wall.Children.OfType<Panel>().Where(card => card.Name == UploadParts.Card).ToList();
        Assert.Equal(2, cards.Count);
        var image = cards[0].GetVisualDescendants().OfType<Image>().Single();
        Assert.True(image.Preview);
        Assert.Equal("支架.png", image.Alt);
        Assert.Contains(cards[1].GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "报告.pdf");
        // 选文件的区域紧跟在缩略图后面，和它们排在一行
        Assert.Same(upload.DropPart, wall.Children[^1]);
    });

    [Fact]
    [Upstream("Upload", "pasteable：粘贴的图片加进来，不在 accept 里的列出、带错误；sortable：把手用键盘移动")]
    public Task Paste_and_keyboard_sorting() => HeadlessUi.Run(() =>
    {
        var upload = new Upload { ListType = UploadListType.Picture, Multiple = true, Accept = "image/*", Pasteable = true, Sortable = true };
        var window = HeadlessUi.Show(upload);
        _ = upload.Paste([FakeFile.Create("截图.png"), FakeFile.Create("说明.txt")]);
        HeadlessUi.Until(() => upload.Files.Count == 2);
        Assert.Equal([("截图.png", UploadStatus.Selected), ("说明.txt", UploadStatus.Error)], upload.Files.Select(item => (item.Name, item.Status)));
        Assert.Contains(upload.GetVisualDescendants().OfType<Text>(), text => text.Text?.Contains("也可以粘贴图片") == true);

        var handle = Named<IconButton>(upload, "移动 截图.png");
        foreach (var key in new[] { Key.Space, Key.Right, Key.Space })
        {
            Named<IconButton>(upload, "移动 截图.png").RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, Source = handle });
            HeadlessUi.Flush(window);
        }
        Assert.Equal(["说明.txt", "截图.png"], upload.Files.Select(item => item.Name));
    });

    [Fact]
    [Upstream("Upload", "列表模式的选择区域照常有图标与默认提示（不传子元素）")]
    public Task List_mode_drop_zone_keeps_its_defaults() => HeadlessUi.Run(() =>
    {
        var upload = new Upload { Description = "不超过 2 MB" };
        HeadlessUi.Show(upload);
        Assert.Null(upload.DropPart.Content);
        Assert.Equal("不超过 2 MB", upload.DropPart.Description);
    });
}
