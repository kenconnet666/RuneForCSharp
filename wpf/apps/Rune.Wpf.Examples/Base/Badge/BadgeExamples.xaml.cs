using System.Windows;
using System.Windows.Controls;

namespace Rune.Wpf.Examples;

/// <summary>Badge 的示例（vue-tsx-pro 文档站 base/Badge）。in-context 等 Table，statuses 等 StatusBadge。</summary>
static class BadgeExamples
{
    public static Example[] All =>
    [
        new("Badge", "matrix", "样式与色调", "Variant 是 solid、soft（默认）、surface、outline，Tone 是六种状态色调；颜色都取主题 token，每套主题都过对比度检查。", () => new BadgeMatrix()),
        new("Badge", "shapes", "尺寸、形状与图标", "Size 默认 xs；Radius=\"full\" 是胶囊，计数常用实底；Icon 放在文字前，Dot 是同色的小圆点，表格里比整块底色更轻。", () => new BadgeShapes()),
        new("Badge", "tag", "Tag 标签", "Tag 的外观选项与 Badge 相同，作用域里 Badge 的默认值一起改两者；技能、分类、关键词这类成组出现的短文字用它。", TagBasic),
        new("Badge", "tag-removable", "可移除的标签", "订阅了 RemoveRequested 就有移除按钮：先淡出，离场结束才发；按钮的读屏名称是“移除 + 标签文字”，RemoveLabel 可以改。", TagRemovable),
        new("Badge", "tag-selectable", "可选中的标签", "写了 IsSelected 时整个标签是切换按钮，点一下切换：没选中时有描边，选中时实底色、前面一个对勾（不只靠颜色），不写色调时用 accent。筛选条件这类一排可选的短文字用它；要“只能选一个”的分段控件用 ToggleGroup。", TagSelectable),
    ];

    static readonly string[] Conditions = ["加急", "待我审批", "已逾期", "本周到期", "有附件"];

    static FrameworkElement TagSelectable()
    {
        var picked = new HashSet<string> { "加急", "已逾期" };
        var summary = new Small { Tone = TextTone.Muted };
        void Sync() => summary.Text = $"已选：{(picked.Count == 0 ? "（无）" : string.Join("、", Conditions.Where(picked.Contains)))}";
        var row = new Flex { Gap = Space.Xs, Wrap = FlexWrap.Wrap };
        System.Windows.Automation.AutomationProperties.SetName(row, "筛选");
        foreach (var option in Conditions)
        {
            var tag = new Tag { Content = option, IsSelected = picked.Contains(option) };
            System.ComponentModel.DependencyPropertyDescriptor.FromProperty(Tag.IsSelectedProperty, typeof(Tag)).AddValueChanged(tag, (_, _) =>
            {
                if (tag.IsSelected == true) picked.Add(option);
                else picked.Remove(option);
                Sync();
            });
            row.Children.Add(tag);
        }
        Sync();
        return new Flex { Direction = FlexDirection.Column, Gap = Space.Sm, Children = { row, summary } };
    }

    static FrameworkElement TagBasic()
    {
        static Flex Row(string label, params Tag[] tags)
        {
            var row = new Flex { Align = FlexAlign.Center, Gap = Space.Sm, Wrap = FlexWrap.Wrap };
            row.Children.Add(new Text { Size = TextSize.Xs, Tone = TextTone.Muted, Text = label });
            foreach (var tag in tags) row.Children.Add(tag);
            return row;
        }
        return new Flex
        {
            Direction = FlexDirection.Column, Gap = Space.Md,
            Children =
            {
                Row("技能",
                    new Tag { Icon = Lucide.CodeXml, Content = "TypeScript" },
                    new Tag { Icon = Lucide.Palette, Tone = StatusTone.Accent, Content = "设计系统" },
                    new Tag { Icon = Lucide.Database, Tone = StatusTone.Info, Content = "PostgreSQL" },
                    new Tag { Icon = Lucide.Smartphone, Tone = StatusTone.Success, Content = "移动端" },
                    new Tag { Icon = Lucide.Globe, Tone = StatusTone.Warning, Content = "国际化" }),
                Row("样式",
                    new Tag { Variant = StatusVariant.Solid, Tone = StatusTone.Accent, Content = "solid" },
                    new Tag { Variant = StatusVariant.Soft, Tone = StatusTone.Accent, Content = "soft" },
                    new Tag { Variant = StatusVariant.Surface, Tone = StatusTone.Accent, Content = "surface" },
                    new Tag { Variant = StatusVariant.Outline, Tone = StatusTone.Accent, Content = "outline" },
                    new Tag { Radius = Radius.Full, Tone = StatusTone.Neutral, Content = "胶囊形" },
                    new Tag { Size = TextSize.Sm, Tone = StatusTone.Neutral, Variant = StatusVariant.Surface, Content = "size=sm" }),
            },
        };
    }

    static readonly (string Label, StatusTone Tone)[] Filters =
    [
        ("价格：100–500 元", StatusTone.Accent),
        ("品牌：小米", StatusTone.Neutral),
        ("品牌：华为", StatusTone.Neutral),
        ("有货", StatusTone.Success),
        ("包邮", StatusTone.Info),
        ("评分 4.5 以上", StatusTone.Warning),
    ];

    static FrameworkElement TagRemovable()
    {
        var title = new Strong();
        var restore = new Button { Size = TextSize.Sm, Variant = ButtonVariant.Ghost, Icon = Lucide.RotateCcw, Content = "恢复" };
        var tags = new Flex { Gap = Space.Sm, Wrap = FlexWrap.Wrap };
        var empty = new Text { Size = TextSize.Sm, Tone = TextTone.Muted, Text = "没有筛选条件，显示全部商品" };
        void Sync()
        {
            var count = tags.Children.OfType<Tag>().Count();
            title.Text = $"已选筛选条件（{count}）";
            restore.IsEnabled = count != Filters.Length;
            empty.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        void Fill()
        {
            tags.Children.Clear();
            foreach (var (label, tone) in Filters)
            {
                var tag = new Tag { Tone = tone, Radius = Radius.Full, Content = label };
                tag.RemoveRequested += (_, _) =>
                {
                    tags.Children.Remove(tag);
                    Sync();
                };
                tags.Children.Add(tag);
            }
            tags.Children.Add(empty);
            Sync();
        }
        restore.Click += (_, _) => Fill();
        Fill();
        return new Card
        {
            Content = new Flex
            {
                Direction = FlexDirection.Column, Gap = Space.Md,
                Children =
                {
                    new Flex { Align = FlexAlign.Center, Justify = FlexJustify.Between, Gap = Space.Sm, Children = { title, restore } },
                    tags,
                },
            },
        };
    }
}

/// <summary>Badge/matrix：样式与色调。</summary>
public partial class BadgeMatrix : UserControl
{
    /// <summary>加载示例。</summary>
    public BadgeMatrix() => InitializeComponent();
}

/// <summary>Badge/shapes：尺寸、形状与图标。</summary>
public partial class BadgeShapes : UserControl
{
    /// <summary>加载示例。</summary>
    public BadgeShapes() => InitializeComponent();
}
