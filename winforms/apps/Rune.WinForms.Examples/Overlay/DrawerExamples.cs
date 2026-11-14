namespace Rune.WinForms.Examples;

/// <summary>Drawer 页的示例（vue-tsx-pro 文档站 overlay/Drawer）。上游的列表是 DataTable（还没有），这里用 ItemListControl。</summary>
static class DrawerExamples
{
    public static Example[] All =>
    [
        new("Drawer", "detail", "详情：列表留在原处", "点一行从结束边滑出详情（关键信息、时限、流转记录），列表留在原处；关上后焦点回到那一行。正在看的那一行一直高亮。", Detail),
        new("Drawer", "edit", "编辑：拖宽度与关闭前确认", "靠正文的一边可以拖宽、拖到几乎关上（PanelWidth，变了发 PanelWidthChanged，这里记在静态字段里）；有没保存的改动时，取消、关闭按钮、Esc、点遮罩都先经过 BeforeClose 确认；保存用 Confirm（保存时加载中、关不掉）。", Edit),
        new("Drawer", "sides", "方向与宽度", "Side 是 Start / End / Top / Bottom（Start / End 是逻辑方向，从右到左排版时左右互换）；左右两侧按 DialogSize 的档位定宽，靠正文的一边可以拖动调宽；上下两侧的高度随内容，最多到窗体高度。", Sides),
        new("Drawer", "filters", "筛选", "条件多、页面放不下时收进抽屉：Confirm 是“查看结果”，Footer 里的“重置”排在它前面，按钮上带已设几项。", Filters),
        new("Drawer", "imperative", "命令式：从列表的一行打开", "不在界面里放抽屉、不管 IsOpen：new DrawerControl { … }.ShowAsync(owner) 从一行的“编辑”直接打开；保存后改回列表，ShowAsync 得到关上的原因，下面一行告诉用户是保存还是放弃。", Imperative),
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

    static FlexControl Badges(Project project) =>
        Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Sm, AutoSize = true }, new BadgeControl { Tone = project.Tone, Text = project.Status }, new DeadlineBadgeControl { Due = project.Due, Today = Today });

    static Control Detail()
    {
        var drawer = new DrawerControl { DialogSize = DialogSize.Sm, Footer = new ButtonControl { Variant = ButtonVariant.Soft, Tone = StatusTone.Neutral, Icon = Lucide.ExternalLink, Text = "打开完整详情" } };
        var list = new ItemListControl { Divided = true, Hoverable = true, AccessibleName = "项目", Dock = DockStyle.Top };
        var items = new List<ItemListItemControl>();
        foreach (var project in Projects)
        {
            var item = new ItemListItemControl { Text = project.Name, Description = $"{project.Code} · {project.Team}", End = Badges(project) };
            item.Invoked += (_, _) =>
            {
                foreach (var other in items) other.IsSelected = other == item;
                drawer.Title = project.Name;
                drawer.Description = $"{project.Code} · {project.Team}";
                drawer.Content = DetailOf(project);
                drawer.IsOpen = true;
            };
            items.Add(item);
            list.Controls.Add(item);
        }
        drawer.OpenChanged += (_, e) =>
        {
            if (!e.IsOpen) foreach (var item in items) item.IsSelected = false;
        };
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Align = FlexAlign.Stretch },
            Build.Card(new CardControl { Variant = CardVariant.Outline, CardPadding = Space.Xs, Dock = DockStyle.Top }, list), drawer);
    }

    static Control DetailOf(Project project)
    {
        var descriptions = new DescriptionsControl { Columns = 2 };
        foreach (var item in new DescriptionItem[]
        {
            new() { Label = "主办", Value = project.Owner },
            new() { Label = "责任团队", Value = project.Team },
            new() { Label = "预计年节省", Value = $"¥{project.Saving:N0}" },
            new() { Label = "当前环节截止", Value = project.Due.ToString("yyyy-MM-dd") },
        }) descriptions.Items.Add(item);
        var timeline = new TimelineControl { TextSize = TextSize.Sm, Reverse = true, Items = [.. project.Records.Select(record => new TimelineItem { Title = record.Title, Time = record.Time, Tone = record.Tone ?? default })] };
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch },
            Badges(project), descriptions,
            Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch }, new H3Control { Text = "流转记录", TextSize = TextSize.Sm }, timeline));
    }

    // 拖动后的宽度（真实应用里存进用户设置）
    static int? editWidth;

    static Control Edit()
    {
        var savedName = "显示器支架合并";
        var savedNote = "两件合并为一件，减少紧固件";
        var name = new TextFieldControl();
        var note = new TextareaControl { Rows = 4 };
        var title = new TextControl { Text = savedName };
        var summary = new SmallControl { Tone = TextTone.Muted, Text = savedNote };
        bool Dirty() => name.Text != savedName || note.Text != savedNote;
        var drawer = new DrawerControl
        {
            Title = "编辑项目", Description = "改了没保存时关闭会先确认；左边的竖线可以拖动调宽，往右拖到底只剩一窄条", PanelWidth = editWidth, ConfirmText = "保存",
            Content = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch },
                new FieldControl { Label = "项目名称", IsRequired = true, Content = name }, new FieldControl { Label = "简要描述", Content = note }),
        };
        drawer.PanelWidthChanged += (_, _) => editWidth = drawer.PanelWidth;
        // 能不能关：没改过，或者确认放弃
        drawer.BeforeClose = async _ => !Dirty() || await AlertDialogControl.ConfirmAsync(name, new ConfirmOptions { Title = "放弃未保存的改动？", ConfirmText = "放弃", Tone = StatusTone.Danger });
        drawer.Confirm = async () =>
        {
            await Task.Delay(600);
            (savedName, savedNote) = (name.Text, note.Text);
            (title.Text, summary.Text) = (savedName, savedNote);
            return true;
        };
        var edit = new ButtonControl { Variant = ButtonVariant.Soft, Icon = Lucide.Pencil, Text = "编辑" };
        edit.Click += (_, _) =>
        {
            (name.Text, note.Text) = (savedName, savedNote);
            drawer.IsOpen = true;
        };
        var info = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Xs, AutoSize = true }, title, summary);
        return Build.Card(new CardControl { Dock = DockStyle.Top },
            Build.Flex(new FlexControl { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Md, Dock = DockStyle.Top }, info, edit, drawer));
    }

    static Control Sides()
    {
        var size = new ToggleGroupControl { IsRequired = true, TextSize = TextSize.Sm, AccessibleName = "宽度档位" };
        size.Controls.AddRange([.. new[] { ("xs", "30rem"), ("sm", "40rem"), ("md", "48rem"), ("lg", "64rem") }.Select(pair => new ToggleItemControl { Value = pair.Item1, Text = $"{pair.Item1} · {pair.Item2}" })]);
        size.Value = "xs";
        var drawer = new DrawerControl
        {
            Content = new ParagraphControl { Text = "Start / End 是逻辑方向：从右到左排版时左右互换。左右两侧按 DialogSize 的档位定宽（与 Container 同档），靠正文的一边可以拖动调宽；顶部、底部的抽屉高度随内容，最多到窗体高度。" },
        };
        var buttons = new FlexControl { Gap = Space.Sm, Wrap = true, AutoSize = true };
        foreach (var (side, icon, label) in new[] { (DrawerSide.Start, Lucide.ArrowRight, "起始边"), (DrawerSide.End, Lucide.ArrowLeft, "结束边"), (DrawerSide.Top, Lucide.ArrowDown, "顶部"), (DrawerSide.Bottom, Lucide.ArrowUp, "底部") })
        {
            var button = new ButtonControl { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = icon, Text = $"从{label}滑入" };
            button.Click += (_, _) =>
            {
                drawer.Side = side;
                drawer.DialogSize = DialogSize.Parse(size.Value ?? "xs");
                drawer.Title = $"Side=\"{side}\" DialogSize=\"{drawer.DialogSize}\"";
                drawer.IsOpen = true;
            };
            buttons.Controls.Add(button);
        }
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start },
            new FieldControl { Label = "宽度档位（左右两侧）", Content = size }, buttons, drawer);
    }

    static Control Filters()
    {
        var team = new SelectControl { ItemsSource = new[] { "生产部", "研发中心", "产品部", "供应链管理" }, PlaceholderText = "全部团队" };
        var status = new CheckboxGroupControl { AccessibleName = "状态" };
        status.Controls.AddRange([.. new[] { ("designing", "设计中"), ("verifying", "测试中"), ("confirming", "待验收"), ("settled", "已结算") }.Select(pair => new CheckboxControl { Value = pair.Item1, Text = pair.Item2 })]);
        var urgent = new SwitchControl { Text = "只看临期（3 天内）" };
        int Count() => (team.Value is { Length: > 0 } ? 1 : 0) + (status.Value is { Count: > 0 } ? 1 : 0) + (urgent.IsChecked ? 1 : 0);
        var summary = new SmallControl { Tone = TextTone.Muted, Text = "没有筛选条件：显示全部 128 个项目" };
        var reset = new ButtonControl { Variant = ButtonVariant.Ghost, Tone = StatusTone.Neutral, Text = "重置" };
        var drawer = new DrawerControl
        {
            DialogSize = DialogSize.Xs, Title = "筛选项目", Footer = reset,
            Content = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Lg, Align = FlexAlign.Stretch },
                new FieldControl { Label = "责任团队", Content = team }, new FieldControl { Label = "状态", Content = status }, new FieldControl { Label = "时限", Content = urgent }),
        };
        void Update() => drawer.ConfirmText = $"查看结果（已设 {Count()} 项）";
        team.ValueChanged += (_, _) => Update();
        status.ValueChanged += (_, _) => Update();
        urgent.IsCheckedChanged += (_, _) => Update();
        reset.Click += (_, _) => (team.Value, status.Value, urgent.IsChecked) = (null, [], false);
        var open = new ButtonControl { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral, Icon = Lucide.Funnel, Text = "筛选" };
        drawer.Confirm = () =>
        {
            var applied = Count();
            open.Text = applied > 0 ? $"筛选 · {applied}" : "筛选";
            summary.Text = applied > 0 ? $"已设 {applied} 项条件" : "没有筛选条件：显示全部 128 个项目";
            return Task.FromResult(true);
        };
        Update();
        open.Click += (_, _) => drawer.IsOpen = true;
        return Build.Flex(new FlexControl { Align = FlexAlign.Center, Gap = Space.Md, Wrap = true }, open, summary, drawer);
    }

    sealed record Supplier(string Id, string Name, string Contact, string Phone);

    static Control Imperative()
    {
        var rows = new List<Supplier> { new("S001", "华辰精密", "林小满", "021-5566 1001"), new("S002", "启航塑胶", "周一航", "0512-6688 2002"), new("S003", "远山钣金", "陈默", "029-8811 3003") };
        var list = new ItemListControl { Divided = true, Inset = Space.Md, AccessibleName = "供应商", Dock = DockStyle.Top };
        var status = new SmallControl { Tone = TextTone.Muted, Text = "点“编辑”在抽屉里改一行" };
        var verdicts = new Dictionary<OpenChangeReason, string> { [OpenChangeReason.Confirm] = "已保存", [OpenChangeReason.Escape] = "按 Esc 放弃", [OpenChangeReason.Outside] = "点遮罩放弃" };
        void Render()
        {
            foreach (var old in list.Controls.Cast<Control>().ToList()) old.Dispose();
            for (var index = 0; index < rows.Count; index++)
            {
                var row = rows[index];
                var at = index;
                var edit = new IconButtonControl { Icon = Lucide.Pencil, TextSize = TextSize.Sm, AccessibleName = $"编辑 {row.Name}" };
                edit.Click += async (_, _) =>
                {
                    var (name, contact, phone) = (new TextFieldControl { Text = row.Name }, new TextFieldControl { Text = row.Contact }, new TextFieldControl { Text = row.Phone });
                    var drawer = new DrawerControl
                    {
                        Title = $"编辑 {row.Name}", Description = $"编号 {row.Id}", DialogSize = DialogSize.Xs, ConfirmText = "保存",
                        Content = Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Stretch },
                            new FieldControl { Label = "名称", IsRequired = true, Content = name }, new FieldControl { Label = "联系人", Content = contact }, new FieldControl { Label = "电话", Content = phone }),
                    };
                    drawer.Confirm = async () =>
                    {
                        await Task.Delay(500);
                        rows[at] = row with { Name = name.Text, Contact = contact.Text, Phone = phone.Text };
                        return true;
                    };
                    var reason = await drawer.ShowAsync(edit);
                    status.Text = $"{row.Name}：{verdicts.GetValueOrDefault(reason, "放弃编辑")}";
                    drawer.Dispose();
                    if (reason == OpenChangeReason.Confirm) Render();
                };
                list.Controls.Add(new ItemListItemControl { Text = row.Name, Description = $"{row.Id} · {row.Contact} · {row.Phone}", End = edit });
            }
        }
        Render();
        return Build.Flex(new FlexControl { Direction = FlexDirection.Column, Gap = Space.Sm, Align = FlexAlign.Stretch },
            Build.Card(new CardControl { Variant = CardVariant.Outline, CardPadding = Space.Xs, Dock = DockStyle.Top }, list), status);
    }
}
