using System.Windows;

namespace Rune.Wpf.Docs;

/// <summary>文档应用。</summary>
public partial class App : Application
{
    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        new MainWindow().Show();
    }
}
