using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace Rune.Avalonia.Examples;

/// <summary>Button 的示例（vue-tsx-pro 文档站 base/Button；图标按钮、预设按钮也在这一页）。Field、ButtonGroup、Tooltip 做出来后补上 types、group 与 disabled 的其余部分。</summary>
static class ButtonExamples
{
    public static Example[] All =>
    [
        new("Button", "basic", "基础用法", "一组操作里只有一个主要操作用默认的实底样式，其余按重要程度依次减弱；破坏性操作放在另一侧。页脚默认靠结束边排，要两端分开时在 Footer 里放一个 Flex（Justify=between），再用样式给它的外层 ContentPresenter 写 Flex.Grow 占满一行。", () => new ButtonBasic()),
        new("Button", "variants", "样式", "solid 是一组操作里的主要操作，每组最多一个；其余按强调程度递减。", () => new ButtonVariants()),
        new("Button", "tones", "色调", "Tone 与 Badge、Alert 同一套：neutral 用于取消这类不需要强调的操作，danger 用于删除等破坏性操作。", () => new ButtonTones()),
        new("Button", "sizes", "尺寸", "Size 是字号档位，内边距、高度、图标都按 em 随之缩放；不写时继承外面的字号。", () => new ButtonSizes()),
        new("Button", "radius", "圆角", "Radius 默认 md；full 是胶囊形。", () => new ButtonRadius()),
        new("Button", "icons", "图标", "Icon 在文字前、IconEnd 在文字后，大小跟随按钮的字号；内容也可以放别的组件（Badge）。", () => new ButtonIcons()),
        new("Button", "icon-buttons", "图标按钮", "IconButton 宽高相等，默认 ghost 样式、中性色（一排图标不会满屏品牌色）；外观选项同 Button。没有文字，必须写 AutomationProperties.Name，读屏才知道它是做什么的。", () => new ButtonIconButtons()),
        new("Button", "presets", "关闭、清空与复制", "预设按钮：CloseButton 读屏念语言包的“关闭”；CopyButton 复制固定的 Value，或点击时才用 ValueFactory 生成的值，复制后换成对勾，Copied 报告结果。ClearButton 是 TextField、Select 的 Clearable 用的清空按钮。", Presets),
        new("Button", "loading", "加载中", "IsLoading 显示旋转指示，宽度不变、点击无效、焦点保留；很快完成的加载不出现指示，出现了至少显示 0.5 秒。", () => new ButtonLoading()),
        new("Button", "full-width", "占满宽度", "FullWidth 占满一行：登录页、移动端的主要操作。", () => new ButtonFullWidth()),
        new("Button", "links", "链接按钮", "点了去别处用 LinkButton 而不是 Button 加点击：读屏念“链接”；只有图标的链接用 IconLinkButton（页脚的文档、订阅、联系）。桌面没有站内地址，Href 写绝对地址、用系统打开，External 显示在新窗口打开并告诉读屏。", () => new ButtonLinks()),
        new("Button", "group", "按钮组", "ButtonGroup 把相关的按钮连成一条：拆分按钮、翻页、缩放、对齐方式。组上写的 Variant、Tone、Size 是组里按钮的默认值，按钮自己写的优先；Orientation=vertical 竖排。", Group),
        new("Button", "disabled", "不可用", "IsEnabled=False 时整体变淡、不响应点击。", () => new ButtonDisabled()),
    ];

    static IconButton Icon(IconData icon, string name)
    {
        var button = new IconButton { Icon = icon };
        AutomationProperties.SetName(button, name);
        return button;
    }

    static ButtonGroup Group(string name, ButtonGroup group, params Control[] buttons)
    {
        AutomationProperties.SetName(group, name);
        group.Children.AddRange(buttons);
        return group;
    }

    static Control Group()
    {
        ButtonGroup Neutral(string name, params Control[] buttons) => Group(name, new ButtonGroup { Variant = ButtonVariant.Surface, Tone = StatusTone.Neutral }, buttons);
        var toolbar = new Toolbar
        {
            Gap = Space.Sm,
            Children =
            {
                Neutral("撤销", Icon(Lucide.Undo2, "撤销"), Icon(Lucide.Redo2, "重做")),
                Neutral("字形", Icon(Lucide.LetterB, "加粗"), Icon(Lucide.Italic, "斜体"), Icon(Lucide.Underline, "下划线")),
                Neutral("列表", Icon(Lucide.List, "无序列表"), Icon(Lucide.ListOrdered, "有序列表")),
                Neutral("对齐", Icon(Lucide.TextAlignStart, "左对齐"), Icon(Lucide.TextAlignCenter, "居中"), Icon(Lucide.TextAlignEnd, "右对齐")),
            },
        };
        AutomationProperties.SetName(toolbar, "编辑器工具栏");

        var more = Icon(Lucide.ChevronDown, "更多合并方式");
        more.Variant = ButtonVariant.Solid;
        var merge = Group("合并", new ButtonGroup(), new Button { Icon = Lucide.GitMerge, Content = "合并请求" }, more);

        var page = 3;
        var current = new Button { Content = $"第 {page} 页" };
        var previous = Icon(Lucide.ChevronLeft, "上一页");
        var next = Icon(Lucide.ChevronRight, "下一页");
        previous.Click += (_, _) => current.Content = $"第 {page = Math.Max(1, page - 1)} 页";
        next.Click += (_, _) => current.Content = $"第 {++page} 页";
        var paging = Group("翻页", new ButtonGroup { Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral }, previous, current, next);

        var zoom = 100;
        var reset = new Button { Content = "100%" };
        var zoomOut = Icon(Lucide.Minus, "缩小");
        var zoomIn = Icon(Lucide.Plus, "放大");
        zoomOut.Click += (_, _) => reset.Content = $"{zoom = Math.Max(25, zoom - 25)}%";
        zoomIn.Click += (_, _) => reset.Content = $"{zoom = Math.Min(400, zoom + 25)}%";
        reset.Click += (_, _) => reset.Content = $"{zoom = 100}%";
        var zooming = Group("缩放", new ButtonGroup { Variant = ButtonVariant.Surface, Tone = StatusTone.Neutral, Size = TextSize.Sm }, zoomOut, reset, zoomIn);

        var export = Group("导出", new ButtonGroup { Orientation = AttachedOrientation.Vertical, Variant = ButtonVariant.Outline, Tone = StatusTone.Neutral },
            new Button { Content = "导出 PDF" }, new Button { Content = "导出 Word" }, new Button { Content = "导出 Markdown" });

        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Lg, VerticalAlignment = VerticalAlignment.Top,
            Children =
            {
                toolbar,
                new Flex
                {
                    Gap = Space.Lg, Wrap = true, Align = FlexAlign.Start,
                    Children =
                    {
                        new Flex { Direction = FlexDirection.Column, Gap = Space.Md, Align = FlexAlign.Start, Children = { merge, paging, zooming } },
                        export,
                    },
                },
            },
        };
    }

    static Control Presets()
    {
        var root = new Flex { Direction = FlexDirection.Column, Gap = Space.Md, VerticalAlignment = VerticalAlignment.Top };
        var notice = new Card();
        var close = new CloseButton();
        close.Click += (_, _) => root.Children[0] = new Small { Tone = TextTone.Muted, Text = "提示已关闭（刷新示例可以恢复）" };
        notice.Content = new Flex
        {
            Align = FlexAlign.Start, Justify = FlexJustify.Between, Gap = Space.Sm,
            Children =
            {
                new Flex
                {
                    Direction = FlexDirection.Column, Gap = Space.Xs,
                    Children = { new Strong { Text = "新版本已发布" }, new Text { Size = TextSize.Sm, Tone = TextTone.Muted, Text = "右上角的 CloseButton 读屏念语言包里的“关闭”。" } },
                },
                close,
            },
        };
        const string apiKey = "sk-live-3f9a7c21e8b44d0a";
        var key = new Text();
        key.Inlines!.Add(new Code { Inlines = { new global::Avalonia.Controls.Documents.Run(apiKey) } });
        var copyKey = new CopyButton { Value = apiKey };
        AutomationProperties.SetName(copyKey, "复制 API 密钥");
        var copies = 0;
        var count = new Small { Tone = TextTone.Muted, Text = "已复制 0 次" };
        // 模拟向服务端申请一条一次性的邀请链接
        var copyInvite = new CopyButton
        {
            ValueFactory = async () =>
            {
                await Task.Delay(800);
                return $"https://example.com/invite/{Guid.NewGuid().ToString("N")[..8]}";
            },
            Timeout = TimeSpan.FromSeconds(3),
        };
        AutomationProperties.SetName(copyInvite, "复制邀请链接");
        copyInvite.Copied += (_, e) =>
        {
            if (e.Success) count.Text = $"已复制 {++copies} 次";
        };
        var copy = new Card
        {
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Sm,
                Children =
                {
                    new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { new Icon { Data = Lucide.KeyRound, Tone = TextTone.Muted }, key, copyKey } },
                    new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Children = { new Icon { Data = Lucide.Link, Tone = TextTone.Muted }, new Text { Size = TextSize.Sm, Text = "邀请链接（点击时才生成）" }, copyInvite, count } },
                },
            },
        };
        root.Children.AddRange([notice, copy]);
        return root;
    }
}

/// <summary>Button/icon-buttons。</summary>
public partial class ButtonIconButtons : UserControl
{
    /// <summary>加载示例。</summary>
    public ButtonIconButtons() => InitializeComponent();
}

/// <summary>Button/basic：点"发布"切换草稿与已发布。</summary>
public partial class ButtonBasic : UserControl
{
    /// <summary>加载示例。</summary>
    public ButtonBasic()
    {
        InitializeComponent();
        Publish.Click += (_, _) =>
        {
            var published = Publish.Content as string == "发布";
            Publish.Content = published ? "撤回" : "发布";
            Status.Text = published ? "已发布" : "草稿 · 3 分钟前保存";
        };
    }
}

/// <summary>Button/variants。</summary>
public partial class ButtonVariants : UserControl
{
    /// <summary>加载示例。</summary>
    public ButtonVariants() => InitializeComponent();
}

/// <summary>Button/tones。</summary>
public partial class ButtonTones : UserControl
{
    /// <summary>加载示例。</summary>
    public ButtonTones() => InitializeComponent();
}

/// <summary>Button/sizes。</summary>
public partial class ButtonSizes : UserControl
{
    /// <summary>加载示例。</summary>
    public ButtonSizes() => InitializeComponent();
}

/// <summary>Button/radius。</summary>
public partial class ButtonRadius : UserControl
{
    /// <summary>加载示例。</summary>
    public ButtonRadius() => InitializeComponent();
}

/// <summary>Button/icons。</summary>
public partial class ButtonIcons : UserControl
{
    /// <summary>加载示例。</summary>
    public ButtonIcons() => InitializeComponent();
}

/// <summary>Button/loading：点击后模拟一次请求。</summary>
public partial class ButtonLoading : UserControl
{
    /// <summary>加载示例。</summary>
    public ButtonLoading()
    {
        InitializeComponent();
        Save.Click += (_, _) => Request(Save, TimeSpan.FromSeconds(1.5));
        Submit.Click += (_, _) => Request(Submit, TimeSpan.FromSeconds(2));
        Delete.Click += (_, _) => Request(Delete, TimeSpan.FromSeconds(1.2));
        Refresh.Click += (_, _) => Request(Refresh, TimeSpan.FromSeconds(1));
        Quick.Click += (_, _) => Request(Quick, TimeSpan.FromSeconds(0.1));
    }

    static async void Request(ButtonCore button, TimeSpan duration)
    {
        button.IsLoading = true;
        await Task.Delay(duration);
        button.IsLoading = false;
    }
}

/// <summary>Button/full-width。</summary>
public partial class ButtonFullWidth : UserControl
{
    /// <summary>加载示例。</summary>
    public ButtonFullWidth() => InitializeComponent();
}

/// <summary>Button/disabled。</summary>
public partial class ButtonDisabled : UserControl
{
    /// <summary>加载示例。</summary>
    public ButtonDisabled() => InitializeComponent();
}

/// <summary>Button/links。</summary>
public partial class ButtonLinks : UserControl
{
    /// <summary>加载示例。</summary>
    public ButtonLinks() => InitializeComponent();
}
