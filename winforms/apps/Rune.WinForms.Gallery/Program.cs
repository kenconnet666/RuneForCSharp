using Rune.WinForms.Examples;

namespace Rune.WinForms.Gallery;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

/// <summary>
/// 示例的主窗口：左侧选主题与示例（Examples 项目的登记表，inspect 用同一张），右侧显示示例。
/// 换主题时写 <see cref="RuneTheme.Current"/>：组件按绘制 effect 自己重画，示例里的普通控件随示例重建。组件做出来后换成组件库自己的导航。
/// </summary>
sealed class MainForm : Form
{
    readonly ComboBox themes = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    readonly ListBox examples = new() { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, IntegralHeight = false };
    readonly Label title = new() { Dock = DockStyle.Top, AutoSize = true };
    readonly Label description = new() { Dock = DockStyle.Top, AutoSize = true };
    readonly Panel stage = new() { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };

    public MainForm()
    {
        Text = "Rune.WinForms 组件示例";
        ClientSize = LogicalToDeviceUnits(new Size(1200, 800));
        Font = new Font(SystemFonts.MessageBoxFont!.FontFamily, Tokens.FontSize.Md * 72 / 96);
        title.Font = new Font(Font.FontFamily, Tokens.FontSize.Xxl * 72 / 96, FontStyle.Bold);

        var content = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(LogicalToDeviceUnits(32)) };
        // Dock = Top 后加的在上面：倒着加
        content.Controls.AddRange([stage, Spacer(24), description, Spacer(8), title]);
        var side = new Panel { Dock = DockStyle.Left, Width = LogicalToDeviceUnits(260), Padding = new Padding(LogicalToDeviceUnits(12)) };
        side.Controls.AddRange([examples, Spacer(12), themes]);
        Controls.AddRange([content, side]);

        themes.Items.AddRange([.. BuiltinThemes.All.Select(theme => theme.Label)]);
        themes.SelectedIndexChanged += (_, _) =>
        {
            RuneTheme.Current.Value = BuiltinThemes.All[themes.SelectedIndex];
            ApplyTheme();
        };
        examples.Items.AddRange([.. ExampleCatalog.All.Select(example => $"{example.Component} · {example.Title}")]);
        examples.SelectedIndexChanged += (_, _) => Show(ExampleCatalog.All[examples.SelectedIndex]);
        themes.SelectedIndex = 0;
        examples.SelectedIndex = 0;
    }

    Control Spacer(int height) => new() { Dock = DockStyle.Top, Height = LogicalToDeviceUnits(height) };

    void ApplyTheme()
    {
        var colors = RuneTheme.Current.Peek().Colors;
        BackColor = examples.BackColor = colors.Background;
        ForeColor = examples.ForeColor = colors.Text;
        description.ForeColor = colors.TextMuted;
        if (examples.SelectedIndex >= 0) Show(ExampleCatalog.All[examples.SelectedIndex]);
    }

    void Show(Example example)
    {
        title.Text = $"{example.Component} · {example.Title}";
        description.Text = $"{example.Description}（{example.Key}）";
        description.MaximumSize = new Size(stage.Parent!.ClientSize.Width - stage.Parent.Padding.Horizontal, 0);
        foreach (Control old in stage.Controls) old.Dispose();
        stage.Controls.Clear();
        var view = example.Create();
        view.Dock = DockStyle.Top;
        stage.Controls.Add(view);
    }
}
