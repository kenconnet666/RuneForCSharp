using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;

namespace Rune.Avalonia.Examples;

/// <summary>Drawer 页的示例（vue-tsx-pro 文档站 overlay/Drawer）。上游的列表是 DataTable（还没有），这里用 ItemList。</summary>
static class DrawerExamples
{
    public static Example[] All =>
    [
        new("Drawer", "detail", "详情：列表留在原处", "点一行从结束边滑出详情（关键信息、时限、流转记录），列表留在原处；关上后焦点回到那一行。正在看的那一行一直高亮。", Detail),
        new("Drawer", "edit", "编辑：拖宽度与关闭前确认", "靠正文的一边可以拖宽、拖到几乎关上（Width 双向绑定，这里记在静态字段里）；有没保存的改动时，取消、关闭按钮、Esc、点遮罩都先经过 BeforeClose 确认；保存用 Confirm（保存时加载中、关不掉）。", Edit),
        new("Drawer", "sides", "方向与宽度", "Side 是 start / end / top / bottom（start / end 是逻辑方向，从右到左排版时左右互换）；左右两侧按 Size 的档位定宽（同 Container），靠正文的一边可以拖动调宽；上下两侧的高度随内容，最多到窗口高度。", Sides),
        new("Drawer", "filters", "筛选", "条件多、页面放不下时收进抽屉：Confirm 是“查看结果”，Footer 里的“重置”排在它前面，按钮上带已设几项。", Filters),
        new("Drawer", "imperative", "命令式：从列表的一行打开", "不在界面里放 Drawer、不管 IsOpen：new Drawer { … }.ShowAsync(owner) 从一行的“编辑”直接打开；保存后改回列表，ShowAsync 得到关上的原因，下面一行告诉用户是保存还是放弃。", Imperative),
    ];

    sealed record Project(string Code, string Name, string Team, string Owner, string Status, StatusTone Tone, decimal Saving, DateOnly Due, (string Title, string Time, StatusTone? Tone)[] Records);

    static readonly DateOnly Today = new(2026, 10, 21);

    static readonly Project[] Projects =
    [
        new("PO-2026-0187", "显示器支架合并为一体成型", "研发中心", "林小满", "测试中", StatusTone.Accent, 174752, new(2026, 10, 23),
            [("林小满 发起", "09-02 10:12", StatusTone.Accent), ("王晴 · 资料审核：通过", "09-05 14:30", StatusTone.Success), ("周宁 · 方案设计：提交方案", "09-28 16:05", StatusTone.Success), ("到达 验证实施", "10-08 09:00", null)]),
        new("PO-2026-0214", "线束卡箍改用铝合金", "生产部", "周宁", "设计中", StatusTone.Info, 86400, new(2026, 10, 16),
            [("周宁 发起", "09-20 09:40", StatusTone.Accent), ("陆川 · 部门审批：退回修改", "10-10 11:20", StatusTone.Warning)]),
        new("PO-2026-0233", "机柜导轨第二供应商", "供应链管理", "韩笑", "待验收", StatusTone.Warning, 312000, new(2026, 10, 30),
            [("韩笑 发起", "08-12 15:00", StatusTone.Accent), ("方圆 · 费用核算：出测算", "09-30 10:30", StatusTone.Success)]),
    ];

    static Control Detail()
    {
        var drawer = new Drawer { Size = DialogSize.Sm, Footer = new Button { Variant = ButtonVariant.Soft, Tone = StatusTone.Neutral, Icon = Lucide.ExternalLink, Content = "打开完整详情" } };
        var list = new ItemList { Divided = true, Hoverable = true };
        AutomationProperties.SetName(list, "项目");
        var items = new List<ItemListItem>();
        foreach (var project in Projects)
        {
            var item = new ItemListItem
            {
                Title = project.Name, Description = $"{project.Code} · {project.Team}",
                End = new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { new Badge { Tone = project.Tone, Content = project.Status }, new DeadlineBadge { Due = project.Due, Today = Today } } },
            };
            item.Invoked += (_, _) =>
            {
                foreach (var other in items) other.IsSelected = other == item;
                drawer.Title = project.Name;
                drawer.Description = $"{project.Code} · {project.Team}";
                drawer.Content = DetailOf(project);
                drawer.IsOpen = true;
            };
            items.Add(item);
            list.Children.Add(item);
        }
        drawer.OpenChanged += (_, e) =>
        {
            if (!e.IsOpen) foreach (var item in items) item.IsSelected = false;
        };
        return new Flex { Direction = FlexDirection.Column, Children = { new Card { Variant = CardVariant.Outline, Padding = Space.Xs, Content = list }, drawer } };
    }

    static Control DetailOf(Project project)
    {
        var descriptions = new Descriptions { Columns = 2 };
        descriptions.Items.AddRange(
        [
            new DescriptionItem { Label = "主办", Value = project.Owner },
            new DescriptionItem { Label = "责任团队", Value = project.Team },
            new DescriptionItem { Label = "预计年节省", Value = $"¥{project.Saving:N0}" },
            new DescriptionItem { Label = "当前环节截止", Value = project.Due.ToString("yyyy-MM-dd") },
        ]);
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Lg,
            Children =
            {
                new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { new Badge { Tone = project.Tone, Content = project.Status }, new DeadlineBadge { Due = project.Due, Today = Today } } },
                descriptions,
                new Flex
                {
                    Direction = FlexDirection.Column, Gap = Space.Sm,
                    Children =
                    {
                        new H3 { Text = "流转记录", Size = TextSize.Sm },
                        new Timeline { Size = TextSize.Sm, Reverse = true, Items = [.. project.Records.Select(record => new TimelineItem { Title = record.Title, Time = record.Time, Tone = record.Tone ?? default })] },
                    },
                },
            },
        };
    }

    // 拖动后的宽度（真实应用里存进用户设置）
    static double editWidth = 560;

    static Control Edit()
    {
        var savedName = "显示器支架合并";
        var savedNote = "两件合并为一件，减少紧固件";
        var name = new TextField();
        var note = new Textarea { Rows = 4 };
        var title = new Text { Text = savedName };
        var summary = new Small { Tone = TextTone.Muted, Text = savedNote };
        bool Dirty() => name.Text != savedName || note.Text != savedNote;
        var drawer = new Drawer
        {
            Title = "编辑项目", Description = "改了没保存时关闭会先确认；左边的竖线可以拖动调宽，往右拖到底只剩一窄条", Width = editWidth, ConfirmText = "保存",
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Md,
                Children = { new Field { Label = "项目名称", IsRequired = true, Content = name }, new Field { Label = "简要描述", Content = note } },
            },
        };
        drawer.PropertyChanged += (_, e) =>
        {
            if (e.Property == Layoutable.WidthProperty) editWidth = drawer.Width;
        };
        // 能不能关：没改过，或者确认放弃
        drawer.BeforeClose = async _ => !Dirty() || await AlertDialog.ConfirmAsync(name, new ConfirmOptions { Title = "放弃未保存的改动？", ConfirmText = "放弃", Tone = StatusTone.Danger });
        drawer.Confirm = async () =>
        {
            await Task.Delay(600);
            (savedName, savedNote) = (name.Text ?? "", note.Text ?? "");
            (title.Text, summary.Text) = (savedName, savedNote);
            return true;
        };
        var edit = new Button { Variant = ButtonVariant.Soft, Icon = Lucide.Pencil, Content = "编辑" };
        edit.Click += (_, _) =>
        {
            (name.Text, note.Text) = (savedName, savedNote);
            drawer.IsOpen = true;
        };
        return new Card
        {
            Content = new Flex
            {
                Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Md,
                Children = { new Flex { Direction = FlexDirection.Column, Gap = Space.Xs, Children = { title, summary } }, edit, drawer },
            },
        };
    }

    static Control Sides()
    {
        var size = new ToggleGroup { IsRequired = true, Value = "xs", Size = TextSize.Sm };
        foreach (var (key, width) in new[] { ("xs", "30rem"), ("sm", "40rem"), ("md", "48rem"), ("lg", "64rem") }) size.Children.Add(new ToggleItem { Value = key, Content = $"{key} · {width}" });
        AutomationProperties.SetName(size, "宽度档位");
        var drawer = new Drawer
        {
            Content = new Paragraph { Text = "start / end 是逻辑方向：从右到左排版时左右互换。左右两侧按 Size 的档位定宽（与 Container 同档），靠正文的一边可以拖动调宽；顶部、底部的抽屉高度随内容，最多到窗口高度。" },
        };
        var buttons = new Flex { Gap = Space.Sm, Wrap = FlexWrap.Wrap };
        foreach (var (side, icon, label) in new[] { (DrawerSide.Start, Lucide.ArrowRight, "起始边"), (DrawerSide.End, Lucide.ArrowLeft, "结束边"), (DrawerSide.Top, Lucide.ArrowDown, "顶部"), (DrawerSide.Bottom, Lucide.ArrowUp, "底部") })
        {
            var button = new Button { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = icon, Content = $"从{label}滑入" };
            button.Click += (_, _) =>
            {
                drawer.Side = side;
                drawer.Size = DialogSize.Parse(size.Value ?? "xs");
                drawer.Title = $"Side=\"{side}\" Size=\"{drawer.Size}\"";
                drawer.IsOpen = true;
            };
            buttons.Children.Add(button);
        }
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Children = { new Field { Label = "宽度档位（左右两侧）", Content = size }, buttons, drawer } };
    }

    static Control Filters()
    {
        var team = new Select { ItemsSource = new[] { "生产部", "研发中心", "产品部", "供应链管理" }, Placeholder = "全部团队" };
        var status = new CheckboxGroup();
        foreach (var (value, label) in new[] { ("designing", "设计中"), ("verifying", "测试中"), ("confirming", "待验收"), ("settled", "已结算") }) status.Items.Add(new Checkbox { Value = value, Content = label });
        var urgent = new Switch { Content = "只看临期（3 天内）" };
        var applied = 0;
        int Count() => (team.Value is { Length: > 0 } ? 1 : 0) + (status.Value is { Count: > 0 } ? 1 : 0) + (urgent.IsChecked == true ? 1 : 0);
        var badge = new Badge { Size = TextSize.Xs, Radius = Radius.Full, IsVisible = false };
        var summary = new Small { Tone = TextTone.Muted, Text = "没有筛选条件：显示全部 128 个项目" };
        var reset = new Button { Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Content = "重置" };
        var drawer = new Drawer
        {
            Size = DialogSize.Xs, Title = "筛选项目", Footer = reset,
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Lg,
                Children = { new Field { Label = "责任团队", Content = team }, new Field { Label = "状态", Content = status }, new Field { Label = "时限", Content = urgent } },
            },
        };
        void Update() => drawer.ConfirmText = $"查看结果（已设 {Count()} 项）";
        team.PropertyChanged += (_, e) =>
        {
            if (e.Property == Select.ValueProperty) Update();
        };
        status.PropertyChanged += (_, e) =>
        {
            if (e.Property == CheckboxGroup.ValueProperty) Update();
        };
        urgent.PropertyChanged += (_, e) =>
        {
            if (e.Property.Name == nameof(Switch.IsChecked)) Update();
        };
        reset.Click += (_, _) => (team.Value, status.Value, urgent.IsChecked) = (null, [], false);
        drawer.Confirm = () =>
        {
            applied = Count();
            badge.Content = applied.ToString();
            badge.IsVisible = applied > 0;
            summary.Text = applied > 0 ? $"已设 {applied} 项条件" : "没有筛选条件：显示全部 128 个项目";
            return Task.FromResult(true);
        };
        Update();
        var open = new Button { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = Lucide.Funnel, Content = new Flex { Align = FlexAlign.Center, Gap = Space.Xs, Children = { new TextBlock { Text = "筛选" }, badge } } };
        open.Click += (_, _) => drawer.IsOpen = true;
        return new Flex { Align = FlexAlign.Center, Gap = Space.Md, Wrap = FlexWrap.Wrap, Children = { open, summary, drawer } };
    }

    sealed record Supplier(string Id, string Name, string Contact, string Phone);

    static Control Imperative()
    {
        var rows = new List<Supplier> { new("S001", "华辰精密", "林小满", "021-5566 1001"), new("S002", "启航塑胶", "周一航", "0512-6688 2002"), new("S003", "远山钣金", "陈默", "029-8811 3003") };
        var list = new ItemList { Divided = true, Inset = Space.Md };
        AutomationProperties.SetName(list, "供应商");
        var status = new Small { Tone = TextTone.Muted, Text = "点“编辑”在抽屉里改一行" };
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        var verdicts = new Dictionary<OpenChangeReason, string> { [OpenChangeReason.Confirm] = "已保存", [OpenChangeReason.Escape] = "按 Esc 放弃", [OpenChangeReason.Outside] = "点遮罩放弃" };
        void Render()
        {
            list.Children.Clear();
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var at = index;
                var edit = new IconButton { Icon = Lucide.Pencil, Size = TextSize.Sm };
                AutomationProperties.SetName(edit, $"编辑 {row.Name}");
                edit.Click += async (_, _) =>
                {
                    var (name, contact, phone) = (new TextField { Text = row.Name }, new TextField { Text = row.Contact }, new TextField { Text = row.Phone });
                    var drawer = new Drawer
                    {
                        Title = $"编辑 {row.Name}", Description = $"编号 {row.Id}", Size = DialogSize.Xs, ConfirmText = "保存",
                        Content = new Flex
                        {
                            Direction = FlexDirection.Column, Gap = Space.Md,
                            Children = { new Field { Label = "名称", IsRequired = true, Content = name }, new Field { Label = "联系人", Content = contact }, new Field { Label = "电话", Content = phone } },
                        },
                    };
                    drawer.Confirm = async () =>
                    {
                        await Task.Delay(500);
                        rows[at] = row with { Name = name.Text ?? "", Contact = contact.Text ?? "", Phone = phone.Text ?? "" };
                        return true;
                    };
                    var reason = await drawer.ShowAsync(edit);
                    status.Text = $"{row.Name}：{verdicts.GetValueOrDefault(reason, "放弃编辑")}";
                    if (reason == OpenChangeReason.Confirm) Render();
                };
                list.Children.Add(new ItemListItem { Title = row.Name, Description = $"{row.Id} · {row.Contact} · {row.Phone}", End = edit });
            }
        }
        Render();
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Children = { new Card { Variant = CardVariant.Outline, Padding = Space.Xs, Content = list }, status } };
    }
}
