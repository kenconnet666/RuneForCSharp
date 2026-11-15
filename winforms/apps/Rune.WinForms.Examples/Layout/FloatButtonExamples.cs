namespace Rune.WinForms.Examples;

/// <summary>FloatButtonControl、FloatButtonGroupControl、BackTopControl 的示例（vue-tsx-pro 文档站 layout/FloatButton）。</summary>
static class FloatButtonExamples
{
    public static Example[] All =>
    [
        new("FloatButton", "group", "一组按钮与回到顶部", "FloatButtonGroupControl 把按钮竖排在父控件的结束边与底部，跟着父控件变大小、放在最上层（盖在同一个 Panel 里的内容上）；按钮悬停、聚焦时提示名称，Badge 是未读数。BackTopControl 滚过 Threshold 才出现，在组里没出现时不占位；点了平滑滚回顶部，焦点移到开头。", Group),
        new("FloatButton", "speed-dial", "速拨", "FloatButtonGroupControl 给了 Icon 就是速拨：点主按钮展开、收起其余按钮（读屏报展开、收起），Esc 或点外面收起；选了一项后写 IsOpen = false 收起。", SpeedDial),
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

    /// <summary>示例框：固定高度的卡片，里面一个 Panel 装内容与浮动按钮（按钮停在框的角落，窗体里用时就是窗体的角落）。</summary>
    static CardControl Frame(Control content, Control floating, Space padding, int height)
    {
        var page = new Panel();
        page.Height = page.LogicalToDeviceUnits(height);
        page.Controls.AddRange([floating, content]);
        return Build.Card(new CardControl { CardPadding = padding, Dock = DockStyle.Top }, page);
    }

    static Control Group()
    {
        var article = new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch };
        article.Padding = new Padding(article.LogicalToDeviceUnits(16), article.LogicalToDeviceUnits(12), article.LogicalToDeviceUnits(16), article.LogicalToDeviceUnits(48));
        article.Controls.Add(new SmallControl { Text = "往下滚：组里多出“回到顶部”", Tone = TextTone.Muted });
        foreach (var (title, text) in Sections)
            article.Controls.Add(Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs, Align = FlexAlign.Stretch },
                new H4Control { Text = title }, new ParagraphControl { Text = text }, new ParagraphControl { Text = text, Tone = TextTone.Muted }));
        var scroller = new ScrollAreaControl { Dock = DockStyle.Fill, Content = article };

        var service = new FloatButtonControl { Icon = Lucide.MessageCircle, AccessibleName = "联系客服（2 条未读）", Tip = "联系客服", Badge = 2 };
        service.Click += (_, _) =>
        {
            service.Badge = null;
            service.AccessibleName = "联系客服";
        };
        var group = new FloatButtonGroupControl { AccessibleName = "帮助" };
        group.Buttons.AddRange([service, new FloatButtonControl { Icon = Lucide.MessageSquareText, AccessibleName = "意见反馈" }, new BackTopControl { Target = scroller, Threshold = 80 }]);
        return Frame(scroller, group, Space.None, 320);
    }

    static Control SpeedDial()
    {
        var files = new ItemListControl { Dock = DockStyle.Top };
        foreach (var name in new[] { "季度经营分析.docx", "供应商名录.xlsx", "产品路线图.pdf" }) files.Controls.Add(new ItemListItemControl { Text = name });
        var count = new SmallControl { Tone = TextTone.Muted, Text = "共 3 个文件", Dock = DockStyle.Top };
        var dial = new FloatButtonGroupControl { Icon = Lucide.Plus, AccessibleName = "新建" };
        foreach (var (icon, label) in new[] { (Lucide.FileText, "新建文档"), (Lucide.FileSpreadsheet, "新建表格"), (Lucide.Upload, "上传文件") })
        {
            var action = new FloatButtonControl { Icon = icon, AccessibleName = label };
            action.Button.TextSize = TextSize.Md;
            action.Click += (_, _) =>
            {
                files.Controls.Add(new ItemListItemControl { Text = $"{(label.StartsWith("上传") ? label.Replace("上传", "未命名") : label.Replace("新建", "未命名"))} {files.Controls.Count + 1}" });
                count.Text = $"共 {files.Controls.Count} 个文件";
                dial.IsOpen = false;
            };
            dial.Buttons.Add(action);
        }
        var list = new Panel { Dock = DockStyle.Fill };
        list.Controls.AddRange([count, files]);
        return Frame(list, dial, Space.Sm, 320);
    }
}
