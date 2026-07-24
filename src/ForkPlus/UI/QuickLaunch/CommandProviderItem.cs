using Avalonia.Controls;
using Theme = ForkPlus.UI.Theme;
// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia
// - using System.Windows.Media → using Avalonia.Media
// - IImage → IImage（Avalonia.Media）
// - Visibility → bool（Avalonia IsVisible 接受 bool，不再使用 Visibility 枚举）
// 注：本类无 TryFindResource 调用，Icon/SelectedIcon 仅声明为虚属性，由派生类（PaletteCommandItem 等）通过 Theme.FindImage 提供。
using System.ComponentModel;
using Avalonia.Media;

namespace ForkPlus.UI.QuickLaunch
{
	public class CommandProviderItem : INotifyPropertyChanged
	{
		private string _fuzzySearchString;

		public virtual IImage Icon { get; }

		public virtual IImage SelectedIcon { get; }

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
	}
}
