using Avalonia.Controls;

namespace Rune.Avalonia.Examples;

/// <summary>Upload 页的示例（vue-tsx-pro 文档站 input/Upload）。Upload 还没做，先有只负责选文件的 FileDrop（import）。</summary>
static class UploadExamples
{
    public static Example[] All =>
    [
        new("Upload", "import", "导入 CSV", "FileDrop 只负责选文件：点击打开系统的文件对话框（按 Accept 筛选），也可以拖进来；读文件、校验、导入由自己决定。不是 CSV 的文件经 Rejected 交出来。", Import),
    ];

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
}
