using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Platform.Storage;

namespace Rune.Avalonia.Examples;

/// <summary>Upload 页的示例（vue-tsx-pro 文档站 input/Upload）。上游 form 示例的原生表单提交（隐藏的 file input）桌面上没有：提交时直接读 Files。</summary>
static class UploadExamples
{
    public static Example[] All =>
    [
        new("Upload", "wall", "图片墙：预览、排序与粘贴", "ListType=\"picture\" 是方形缩略图，点开看大图（墙上的图左右切换）；拖动把手调整顺序（第一张是封面），截图后在这里按 Ctrl + V 也能加进来；上传中盖一层进度，名字里带“失败”的图会上传失败、可以重试。", Wall),
        new("Upload", "photos", "选好就上传", "一打开就有几张图在不同的状态：已上传、正在上传（看进度）、上传失败（可以重试）、不合要求。给了 Uploader 时选到文件、父级放进来的待上传文件都马上开始上传；移除正在上传的文件会取消。", Photos),
        new("Upload", "file-drop", "FileDrop", "FileDrop 只负责选文件：点击打开系统的文件对话框（按 Accept 筛选），也可以拖进来；读文件、校验、导入由自己决定。不是 CSV 的文件经 Rejected 交出来。", Import),
        new("Upload", "form", "随表单提交与已有文件", "被退回后重新提交：上次交的文件（UploadFile.Existing）还在、可以删；没给 Uploader 时新选的停在“已选择”，提交时一起交。", Resume),
        new("Upload", "folder", "上传整个文件夹", "Directory 时对话框里选文件夹，或把文件夹拖进来，里面的文件（含子文件夹）都加进来，列表里显示相对路径。", Folder),
    ];

    /// <summary>
    /// 演示用的文件：内容在内存里（大小对就行），没有真的文件。IStorageFile 不让使用方实现（平台给的），这里用 DispatchProxy 做一个；
    /// 真实场景里的文件来自 FileDrop、系统的文件对话框。
    /// </summary>
    public class MemoryFile : System.Reflection.DispatchProxy
    {
        string name = "";
        int size;

        public static IStorageFile Create(string name, int size)
        {
            var file = Create<IStorageFile, MemoryFile>();
            var memory = (MemoryFile)(object)file;
            (memory.name, memory.size) = (name, size);
            return file;
        }

        protected override object? Invoke(System.Reflection.MethodInfo? method, object?[]? args) => method?.Name switch
        {
            "get_Name" => name,
            "get_Path" => new Uri($"memory:///{Uri.EscapeDataString(name)}"),
            "get_CanBookmark" => false,
            "GetBasicPropertiesAsync" => Task.FromResult(new StorageItemProperties((ulong)size, null, null)),
            "OpenReadAsync" => Task.FromResult<Stream>(new MemoryStream(new byte[size])),
            "Dispose" => null,
            _ => throw new NotSupportedException($"演示用的文件不支持 {method?.Name}"),
        };
    }

    /// <summary>假的上传：每 300 毫秒进 20%；名字里带“失败”的上传失败（重试也一样）；移除时经取消令牌停下。</summary>
    static async Task<object?> UploadPhoto(IStorageFile file, IProgress<double> progress, CancellationToken cancellation)
    {
        for (var fraction = 0.2; ; fraction += 0.2)
        {
            await Task.Delay(300, cancellation);
            progress.Report(Math.Min(fraction, 1));
            if (fraction < 1) continue;
            if (file.Name.Contains("失败")) throw new InvalidOperationException("网络中断");
            return $"/files/{Uri.EscapeDataString(file.Name)}";
        }
    }

    static Control Wall()
    {
        // 已有的照片（上次交的）：用示例图纸当照片
        var upload = new Upload
        {
            Files = [.. ImageExamples.Drawings.Take(3).Select(drawing => UploadFile.Existing($"{drawing.Title}.svg", url: ImageExamples.Drawing(drawing.Code), id: drawing.Code))],
            Uploader = UploadPhoto, ListType = UploadListType.Picture, Multiple = true, Accept = "image/*", MaxSize = 5_000_000, MaxFiles = 8, Sortable = true, Pasteable = true,
        };
        var cover = new Small { Tone = TextTone.Muted };
        void Update() => cover.Text = $"封面：{(upload.Files.Count > 0 ? upload.Files[0].Name : "还没有照片")}";
        upload.PropertyChanged += (_, e) =>
        {
            if (e.Property == Upload.FilesProperty) Update();
        };
        Update();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch,
            Children = { new Field { Label = "验收照片", Description = "最多 8 张，单张不超过 5 MB；第一张是封面", Content = upload }, cover },
        };
    }

    /// <summary>假的上传：每 150 毫秒进 6%；名字里带“断网”的第一次在中途失败，重试就好。</summary>
    static readonly HashSet<string> Tried = [];

    static async Task<object?> FakeUpload(IStorageFile file, IProgress<double> progress, CancellationToken cancellation)
    {
        var fails = file.Name.Contains("断网") && Tried.Add(file.Name);
        for (var fraction = 0.06; ; fraction += 0.06)
        {
            await Task.Delay(150, cancellation);
            progress.Report(Math.Min(fraction, 1));
            if (fails && fraction > 0.5) throw new InvalidOperationException("网络中断");
            if (fraction >= 1) return new Uri($"https://cdn.example.com/{Uri.EscapeDataString(file.Name)}");
        }
    }

    static Control Photos()
    {
        var upload = new Upload
        {
            Multiple = true, Accept = "image/*", MaxSize = 2_000_000, MaxFiles = 6,
            Files =
            [
                UploadFile.Existing("主图.jpg", 864_000, new Uri("https://cdn.example.com/cover.jpg"), id: "cover"),
                // 父级放进来的待上传文件：给了 Uploader 时马上开始上传
                new UploadFile("side", "侧面.jpg", UploadStatus.Selected) { Size = 1_200_000, File = MemoryFile.Create("侧面.jpg", 1_200_000) },
                new UploadFile("detail", "细节（断网）.jpg", UploadStatus.Selected) { Size = 940_000, File = MemoryFile.Create("细节（断网）.jpg", 940_000) },
                new UploadFile("raw", "原图.tif", UploadStatus.Error) { Size = 18_500_000, Error = "不能超过 2 MB", Rejected = true },
            ],
        };
        upload.Uploader = FakeUpload;
        var summary = new Small { Tone = TextTone.Muted };
        void Update()
        {
            int Count(Func<UploadFile, bool> match) => upload.Files.Count(match);
            summary.Text = $"{Count(photo => photo.Status == UploadStatus.Done)} 张已上传 · {Count(photo => photo.Status == UploadStatus.Uploading)} 张上传中 · "
                           + $"{Count(photo => photo.Status == UploadStatus.Error && !photo.Rejected)} 张失败（可以重试）· {Count(photo => photo.Rejected)} 张不合要求";
        }
        upload.PropertyChanged += (_, e) =>
        {
            if (e.Property == Upload.FilesProperty) Update();
        };
        Update();
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch,
            Children = { new Field { Label = "商品图片", Description = "最多 6 张，每张不超过 2 MB", Content = upload }, summary },
        };
    }

    static Control Import()
    {
        var drop = new FileDrop { Accept = ".csv,text/csv", Icon = Lucide.FileSpreadsheet, Description = "第一行是表头：姓名、手机号、部门" };
        var rejected = new Alert { Tone = StatusTone.Danger, Title = "不是 CSV 文件", IsVisible = false };
        var imported = new Small { Tone = TextTone.Muted, IsVisible = false };
        drop.FilesPicked += async (_, e) =>
        {
            rejected.IsVisible = false;
            var file = e.Files[0];
            await using var stream = await file.File.OpenReadAsync();
            using var reader = new StreamReader(stream);
            var rows = (await reader.ReadToEndAsync()).Split('\n').Count(line => line.Trim().Length > 0);
            imported.Text = $"已读取 {file.Name}：{rows} 行";
            imported.IsVisible = true;
        };
        drop.Rejected += (_, e) =>
        {
            rejected.Content = string.Join("、", e.Files.Select(file => file.Name));
            rejected.IsVisible = true;
        };
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { drop, rejected, imported } };
    }

    static Control Resume()
    {
        var evidence = new Upload
        {
            Multiple = true, Accept = ".pdf,.doc,.docx",
            Files =
            [
                UploadFile.Existing("方案评审纪要.pdf", 482_000, new Uri("https://files.example.com/review"), id: "f-1"),
                UploadFile.Existing("验证报告.pdf", url: new Uri("https://files.example.com/report"), id: "f-2"),
            ],
        };
        var note = new Textarea { Text = "补上了试验报告的第 3 节。", Rows = 2 };
        var submitted = new Small { Tone = TextTone.Muted, IsVisible = false };
        var submit = new Button { Content = "重新提交" };
        submit.Click += (_, _) =>
        {
            var kept = evidence.Files.Where(item => item.Status == UploadStatus.Done && item.File is null).Select(item => item.Id).ToList();
            var added = evidence.Files.Where(item => item.File is not null && !item.Rejected).Select(item => item.Name).ToList();
            var text = $"保留 {(kept.Count > 0 ? string.Join("、", kept) : "（无）")}；新文件 {(added.Count > 0 ? string.Join("、", added) : "（无）")}";
            submitted.Inlines = [new Run("提交："), new Code { Inlines = { new Run(text) } }];
            submitted.IsVisible = true;
        };
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch,
            Children =
            {
                new Field { Label = "交付证据", Description = "上次的文件还在，可以删、可以补；新选的提交时一起交", Content = evidence },
                new Field { Label = "说明", Content = note },
                new Flex { Justify = FlexJustify.End, Children = { submit } },
                submitted,
            },
        };
    }

    /// <summary>演示用的上传：半秒后完成；真实场景把相对路径一起交给后端，按目录存。</summary>
    static async Task<object?> UploadInFolder(IStorageFile file, IProgress<double> progress, CancellationToken cancellation)
    {
        progress.Report(0.5);
        await Task.Delay(500, cancellation);
        return file.Name;
    }

    static Control Folder()
    {
        var upload = new Upload { Directory = true, Uploader = UploadInFolder };
        var summary = new Small { Tone = TextTone.Muted, IsVisible = false };
        upload.PropertyChanged += (_, e) =>
        {
            if (e.Property != Upload.FilesProperty) return;
            summary.IsVisible = upload.Files.Count > 0;
            summary.Text = $"共 {upload.Files.Count} 个文件，{upload.Files.Count(item => item.Status == UploadStatus.Done)} 个已上传";
        };
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch,
            Children = { new Field { Label = "项目资料", Description = "选一个文件夹，或把文件夹拖进来", Content = upload }, summary },
        };
    }
}
