using System.Windows;
using Rune.Wpf.Examples;

namespace Rune.Wpf.Gallery;

/// <summary>示例的主窗口：左侧选主题与示例，右侧显示示例。</summary>
public partial class MainWindow : Window
{
    /// <summary>列出主题与示例，选中第一个示例。</summary>
    public MainWindow()
    {
        InitializeComponent();
        var theme = Application.Current.Resources.MergedDictionaries.OfType<RuneTheme>().Single();
        Themes.ItemsSource = RuneTheme.BuiltinThemes;
        Themes.SelectedItem = RuneTheme.BuiltinThemes.Single(entry => entry.Name == theme.Theme);
        Themes.SelectionChanged += (_, _) => theme.Theme = ((BuiltinTheme)Themes.SelectedItem).Name;
        ExampleList.ItemsSource = ExampleCatalog.All.Select(example => $"{example.Component} · {example.Title}").ToList();
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
