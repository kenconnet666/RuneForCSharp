using System.Windows;
using System.Windows.Markup;

// 组件的默认外观在本程序集的 Themes/Generic.xaml（主题样式）：使用方的任何样式 setter 都能盖过其中的触发器
[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]

// XAML 里一个前缀用到库的所有类型：xmlns:rune="https://github.com/kenconnet666/RuneForCSharp/wpf"
[assembly: XmlnsDefinition("https://github.com/kenconnet666/RuneForCSharp/wpf", "Rune.Wpf")]
[assembly: XmlnsPrefix("https://github.com/kenconnet666/RuneForCSharp/wpf", "rune")]
