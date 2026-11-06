using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Rune.Avalonia.Examples;

namespace Rune.Avalonia.Gallery;

/// <summary>示例的主窗口：左侧选主题与示例，右侧显示示例。</summary>
public partial class MainWindow : Window
{
    /// <summary>列出主题与示例，选中第一个示例。</summary>
    public MainWindow()
    {
        InitializeComponent();
        Themes.ItemsSource = RuneThemeVariants.All.Select(variant => variant.Key.ToString());
        Themes.SelectedIndex = 0;
        Themes.SelectionChanged += (_, _) => Application.Current!.RequestedThemeVariant = RuneThemeVariants.All[Themes.SelectedIndex];
        ExampleList.ItemsSource = ExampleCatalog.All.Select(example => $"{example.Component} · {example.Title}");
        ExampleList.SelectionChanged += (_, _) => Show(ExampleCatalog.All[ExampleList.SelectedIndex]);
        ExampleList.SelectedIndex = 0;
    }

    void Show(Example example)
    {
        ExampleTitle.Text = $"{example.Component} · {example.Title}";
        ExampleDescription.Text = $"{example.Description}（{example.Key}）";
        Stage.Content = example.Create();
    }
}
