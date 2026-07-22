// CommandProviderItem.cs：命令面板条目基类（POCO + INotifyPropertyChanged）。
//
// WPF 源对照：src/ForkPlus/UI/QuickLaunch/CommandProviderItem.cs（namespace ForkPlus.UI.QuickLaunch）
//   - class CommandProviderItem : INotifyPropertyChanged
//   - virtual ImageSource Icon / SelectedIcon
//   - Visibility DescriptionVisibility
//   - string Title / SecondaryTitle / Argument / FuzzySearchString
//   - event PropertyChangedEventHandler PropertyChanged
//
// Avalonia 版差异（spike 简化策略）：
//   1. namespace ForkPlus.UI.QuickLaunch → ForkPlus.Avalonia.QuickLaunch
//   2. System.Windows.Media.ImageSource → Avalonia.Media.IImage
//   3. System.Windows.Visibility → bool（DescriptionVisibility）
//      （Avalonia 用 IsVisible bool 绑定，不用 Visibility 枚举）
//   4. System.ComponentModel.INotifyPropertyChanged → 同（Avalonia 兼容）
//   5. 新增 protected static GetIconResource(string) 辅助方法：
//      Application.Current.TryFindResource(key) 返回 Avalonia.Controls.Image 控件，
//      需提取其 Source（IImage）返回。对照 WPF Application.Current.TryFindResource(key) as ImageSource。

using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ForkPlus.Avalonia.QuickLaunch
{
    public class CommandProviderItem : INotifyPropertyChanged
    {
        private string _fuzzySearchString;

        // 对照 WPF: public virtual ImageSource Icon
        // spike: ImageSource → IImage（Avalonia.Media.IImage）
        public virtual IImage Icon { get; }

        // 对照 WPF: public virtual ImageSource SelectedIcon
        // spike: ImageSource → IImage
        public virtual IImage SelectedIcon { get; }

        // 对照 WPF: public Visibility DescriptionVisibility
        // spike: Visibility → bool（Avalonia 用 IsVisible 绑定）
        public bool DescriptionVisibility { get; }

        public string Title { get; }

        public string SecondaryTitle { get; }

        public object Argument { get; }

        public string FuzzySearchString
        {
            get
            {
                return _fuzzySearchString;
            }
            set
            {
                if (!(_fuzzySearchString == value))
                {
                    _fuzzySearchString = value;
                    this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FuzzySearchString"));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public CommandProviderItem(object value, string title, string secondaryTitle)
        {
            Argument = value;
            Title = title;
            SecondaryTitle = secondaryTitle;
            DescriptionVisibility = !string.IsNullOrEmpty(SecondaryTitle);
        }

        // spike 辅助：从 Application 资源中提取 IImage 图标。
        // 对照 WPF: Application.Current.TryFindResource(key) as ImageSource
        // Avalonia: 资源是 Avalonia.Controls.Image 控件，需提取 Source（IImage）。
        protected static IImage GetIconResource(string key)
        {
            if (Application.Current != null && Application.Current.TryGetResource(key, null, out object value))
            {
                if (value is IImage image)
                {
                    return image;
                }
                if (value is Image control && control.Source != null)
                {
                    return control.Source;
                }
            }
            return null;
        }
    }
}
