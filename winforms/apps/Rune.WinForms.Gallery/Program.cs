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

/// <summary>示例的主窗口：组件做出来后按 vue-tsx-pro 的分类（base、display、feedback……）列在左侧导航里。</summary>
sealed class MainForm : Form
{
    public MainForm()
    {
        Text = "Rune.WinForms 组件示例";
        ClientSize = new Size(1200, 800);
        Controls.Add(new Label { Text = "Rune.WinForms", Font = new Font(Font.FontFamily, 18f), AutoSize = true, Location = new Point(24, 24) });
    }
}
