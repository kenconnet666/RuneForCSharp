using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Rune.Wpf.Examples;

/// <summary>FloatButton、FloatButtonGroup、BackTop 的示例（vue-tsx-pro 文档站 layout/FloatButton）。</summary>
static class FloatButtonExamples
{
    public static Example[] All =>
    [
        new("FloatButton", "group", "一组按钮与回到顶部", "FloatButtonGroup 把按钮竖排在所在面板的结束边与底部（放在盖住内容的 Grid 里就浮在内容上）；按钮悬停、聚焦时提示名称，Badge 是未读数。BackTop 滚过 Threshold 才出现，在组里没出现时不占位；点了平滑滚回顶部，焦点移到开头。", Group),
        new("FloatButton", "speed-dial", "速拨", "FloatButtonGroup 给了 Icon 就是速拨：点主按钮展开、收起其余按钮（读屏能展开、收起），Esc 或点外面收起；IsOpen 双向绑定，选了一项后写 false 收起。", SpeedDial),
    ];

    static readonly (string Title, string Text)[] Sections =
    [
        ("开通账号", "管理员在“成员”里邀请同事，对方用邮件里的链接设置密码后即可登录。"),
        ("导入数据", "支持 Excel 与 CSV，第一行是列名；导入前可以预览，出错的行会单独列出。"),
        ("设置审批", "每类单据可以配置多级审批，金额超过阈值时自动加签财务负责人。"),
        ("对接系统", "开放接口用应用密钥签名，回调地址需要在“集成”里登记并通过校验。"),
        ("导出报表", "报表按筛选条件导出，超过一万行时改为后台生成，完成后站内通知。"),
        ("权限与审计", "角色决定能看哪些菜单与数据，关键操作都会写进审计日志，可以按人与时间查询。"),
    ];

    static FloatButton Button(IconData icon, string name)
    {
        var button = new FloatButton { Icon = icon };
        AutomationProperties.SetName(button, name);
        return button;
    }

    static FrameworkElement Group()
    {
        var article = new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Margin = new Thickness(16, 12, 16, 48) };
        article.Children.Add(new Small { Text = "往下滚：组里多出“回到顶部”", Tone = TextTone.Muted });
        foreach (var (title, text) in Sections)
            article.Children.Add(new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Children = { new H4 { Text = title }, new Paragraph { Text = text }, new Paragraph { Text = text, Tone = TextTone.Muted } } });
        var scroller = new ScrollArea { Height = 320, Content = article };

        var unread = 2;
        var service = Button(Lucide.MessageCircle, "联系客服（2 条未读）");
        service.Tip = "联系客服";
        service.Badge = unread;
        service.Click += (_, _) =>
        {
            unread = 0;
            service.Badge = null;
            AutomationProperties.SetName(service, "联系客服");
        };
        var group = new FloatButtonGroup { Children = { service, Button(Lucide.MessageSquareText, "意见反馈"), new BackTop { Target = scroller, Threshold = 80 } } };
        AutomationProperties.SetName(group, "帮助");
        // 盖在内容上：同一个 Grid 里，组靠右下角
        return new Card { Padding = Space.None, ClipToBounds = true, Content = new System.Windows.Controls.Grid { Children = { scroller, group } } };
    }

    static FrameworkElement SpeedDial()
    {
        var files = new ItemList();
        foreach (var name in new[] { "季度经营分析.docx", "供应商名录.xlsx", "产品路线图.pdf" }) files.Children.Add(new ItemListItem { Title = name });
        var count = new Small { Tone = TextTone.Muted, Margin = new Thickness(12, 8, 12, 8), Text = "共 3 个文件" };
        var dial = new FloatButtonGroup { Icon = Lucide.Plus };
        AutomationProperties.SetName(dial, "新建");
        foreach (var (icon, label) in new[] { (Lucide.FileText, "新建文档"), (Lucide.FileSpreadsheet, "新建表格"), (Lucide.Upload, "上传文件") })
        {
            var action = Button(icon, label);
            action.Button.Size = TextSize.Md;
            action.Click += (_, _) =>
            {
                files.Children.Add(new ItemListItem { Title = $"{(label.StartsWith("上传") ? label.Replace("上传", "未命名") : label.Replace("新建", "未命名"))} {files.Children.Count + 1}" });
                count.Text = $"共 {files.Children.Count} 个文件";
                dial.IsOpen = false;
            };
            dial.Children.Add(action);
        }
        return new Card { Padding = Space.Sm, MinHeight = 320, Content = new System.Windows.Controls.Grid { Children = { new StackPanel { Children = { files, count } }, dial } } };
    }
}
