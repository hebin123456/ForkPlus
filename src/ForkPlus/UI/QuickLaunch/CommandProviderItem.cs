using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ForkPlus.UI.QuickLaunch
{
	public class CommandProviderItem : INotifyPropertyChanged
	{
		private string _fuzzySearchString;

		public virtual ImageSource Icon { get; }

		public virtual ImageSource SelectedIcon { get; }

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
