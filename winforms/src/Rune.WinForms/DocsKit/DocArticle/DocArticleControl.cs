using System.ComponentModel;
using System.Windows.Forms.Layout;

namespace Rune.WinForms;

/// <summary>
/// 文档页的骨架（vue-tsx-pro docs-kit 的 DocArticle）：标题、简介与正文。直接加的子控件是正文（一般是一节一节的 <see cref="DocSectionControl"/>）：
/// <code>
/// var article = new DocArticleControl { Title = "快速开始", Description = "两分钟跑起来", Dock = DockStyle.Top };
/// article.Controls.Add(new DocSectionControl { SectionId = "install", Title = "安装" });
/// </code>
/// 长文阅读：字号 15 像素（比界面文字大一号）；头部与正文之间、正文各节之间 3rem，头部里各项之间 space.lg。
/// 简介是 lg 档、次要色的段落，最宽 48rem。WinForms 的行高按字体算，没有 relaxed 的行距。
/// </summary>
public class DocArticleControl : Panel
{
    readonly VerticalStack layout;
    readonly FlexControl header = new() { Direction = FlexDirection.Column, Gap = Space.Lg, AutoSize = true };
    readonly H1Control title = new() { Visible = false };
    readonly ParagraphControl description = new() { TextSize = TextSize.Lg, Tone = TextTone.Muted, Visible = false };

    /// <summary>新建：没有内容。</summary>
    public DocArticleControl()
    {
        // 头部与正文、正文各节之间都是 3rem：按根字号（16 像素）算，不随文章的字号
        layout = new VerticalStack(() => LogicalToDeviceUnits(3 * 16));
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        // 挂上之前先按全局主题的字体族定好字号：一页文章一般先拼好再挂上，挂上时字体不变，拼的时候量过的文字就不用全部重量
        Font = ArticleFont(Fonts.Installed(RuneTheme.Current.Peek().Fonts.Sans));
        header.Controls.AddRange([title, description]);
        Controls.Add(header);
    }

    /// <summary>标题。</summary>
    [Category("Appearance")]
    [Description("标题（h1）。")]
    [DefaultValue(null)]
    public string? Title { get; set { field = value; title.Text = value ?? ""; title.Visible = value is not null; } }

    /// <summary>简介。</summary>
    [Category("Appearance")]
    [Description("标题下面的简介。")]
    [DefaultValue(null)]
    public string? Description { get; set { field = value; description.Text = value ?? ""; description.Visible = !string.IsNullOrEmpty(value); } }

    /// <summary>简介下面、正文之前的内容：导入语句之类。</summary>
    [Browsable(false)]
    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Control? Lead
    {
        get;
        set
        {
            if (field is { } old) header.Controls.Remove(old);
            field = value;
            if (value is not null) header.Controls.Add(value);
        }
    }

    /// <summary>标题（测试用）。</summary>
    internal H1Control TitlePart => title;

    /// <summary>简介（测试用）。</summary>
    internal ParagraphControl DescriptionPart => description;

    /// <inheritdoc />
    public override LayoutEngine LayoutEngine => layout;

    /// <inheritdoc />
    public override Size GetPreferredSize(Size proposedSize) => layout.Measure(this, proposedSize);

    /// <inheritdoc />
    protected override void OnParentChanged(EventArgs e)
    {
        base.OnParentChanged(e);
        UpdateFont();
    }

    /// <inheritdoc />
    protected override void OnParentFontChanged(EventArgs e)
    {
        base.OnParentFontChanged(e);
        UpdateFont();
    }

    /// <summary>长文阅读：比界面文字（14 像素）大一号；字体族跟着外面。</summary>
    void UpdateFont()
    {
        if (Parent is not { } parent) return;
        Font = ArticleFont(parent.Font.FamilyName());
        description.MaximumSize = new Size(LogicalToDeviceUnits(48 * 16), 0);
    }

    static Font ArticleFont(string family) => Fonts.Get(family, 15 * 72 / 96f, FontStyle.Regular);
}

/// <summary>
/// 文档页的一节（vue-tsx-pro docs-kit 的 DocSection）：带锚点的标题（H2，xl 档）与内容，之间 space.lg。直接加的子控件是内容。
/// <see cref="SectionId"/> 是锚点，也是本页目录（TableOfContents）里这一节的 id。
/// </summary>
public class DocSectionControl : FlexControl
{
    readonly H2Control title = new() { TextSize = TextSize.Xl };

    /// <summary>新建：没有内容。</summary>
    public DocSectionControl()
    {
        Direction = FlexDirection.Column;
        Gap = Space.Lg;
        AutoSize = true;
        Controls.Add(title);
    }

    /// <summary>标题的 id（TS 的 id）：锚点，也进本页目录。</summary>
    [Category("Behavior")]
    [Description("标题的 id：锚点，也进本页目录。")]
    [DefaultValue(null)]
    public string? SectionId { get; set; }

    /// <summary>标题。</summary>
    [Category("Appearance")]
    [Description("标题（h2）。")]
    [DefaultValue(null)]
    public string? Title { get => title.Text; set => title.Text = value ?? ""; }

    /// <summary>标题（本页目录按它定位）。</summary>
    public H2Control TitlePart => title;
}
