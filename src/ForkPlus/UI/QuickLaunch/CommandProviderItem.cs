// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia
// - using System.Windows.Media → using Avalonia.Media
// - ImageSource → IImage（Avalonia.Media）
// - Visibility → Avalonia.Visibility（命名空间迁移，枚举值兼容）
// 注：本类无 TryFindResource 调用，Icon/SelectedIcon 仅声明为虚属性，由派生类（PaletteCommandItem 等）通过 Theme.FindImage 提供。
using System.ComponentModel;
using Avalonia;
using Avalonia.Media;

namespace ForkPlus.UI.QuickLaunch
{
	public class CommandProviderItem : INotifyPropertyChanged
	{
		private string _fuzzySearchString;

		public virtual IImage Icon { get; }

		public virtual IImage SelectedIcon { get; }

		public Visibility DescriptionVisibility { get; }

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
			DescriptionVisibility = (string.IsNullOrEmpty(SecondaryTitle) ? Visibility.Hidden : Visibility.Visible);
		}
	}
}
