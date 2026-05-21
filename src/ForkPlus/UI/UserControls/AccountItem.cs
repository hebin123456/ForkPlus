using System.ComponentModel;

namespace ForkPlus.UI.UserControls
{
	public abstract class AccountItem : INotifyPropertyChanged
	{
		public string Title { get; }

		public event PropertyChangedEventHandler PropertyChanged;

		public AccountItem(string title)
		{
			Title = title;
		}
	}
}
