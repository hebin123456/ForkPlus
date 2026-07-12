using System;
using System.ComponentModel;
using ForkPlus.UI.CustomCommands;

namespace ForkPlus.UI.UserControls.Preferences
{
	public class CustomCommandActionViewModel : INotifyPropertyChanged
	{
		private CustomCommandAction _action;

		public string Title { get; private set; }

		public string Details { get; private set; }

		public string ToolTip { get; private set; }

		public CustomCommandAction Action
		{
			get
			{
				return _action;
			}
			set
			{
				_action = value;
				if (Action is ProcessCustomCommandAction processCustomCommandAction)
				{
					Title = PreferencesLocalization.Current("Start Process");
					Details = processCustomCommandAction.Path + " " + processCustomCommandAction.Parameters;
				}
				else if (Action is ShCustomCommandAction)
				{
					Title = PreferencesLocalization.Current("Bash Command");
					Details = PreferencesLocalization.Current("Run bash Script");
				}
				else if (Action is UrlCustomCommandAction urlCustomCommandAction)
				{
					Title = PreferencesLocalization.Current("Open Url");
					Details = urlCustomCommandAction.Url;
				}
				else
				{
					if (!(Action is CancelCustomCommandAction))
					{
						throw new InvalidOperationException();
					}
					Title = PreferencesLocalization.Current("Cancel");
					Details = PreferencesLocalization.Current("close dialog");
				}
				ToolTip = Title + "\n" + Details;
				NotifyPropertyChanged("Title");
				NotifyPropertyChanged("Details");
				NotifyPropertyChanged("ToolTip");
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public CustomCommandActionViewModel(CustomCommandAction action)
		{
			Action = action;
		}

		private void NotifyPropertyChanged(string propertyName = "")
		{
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
