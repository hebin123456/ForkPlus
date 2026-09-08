using System.ComponentModel;
using Avalonia;
using Avalonia.Media;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.QuickLaunch
{
	public class CommandProviderItem : INotifyPropertyChanged
	{
		private string _fuzzySearchString;

		public virtual global::Avalonia.Media.IImage Icon { get; }

		public virtual global::Avalonia.Media.IImage SelectedIcon { get; }

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
			DescriptionVisibility = (string.IsNullOrEmpty(SecondaryTitle) ? false : true);
		}
	}
}
