using System.ComponentModel;
using System.Runtime.CompilerServices;
using ForkPlus.Settings;
using ForkPlus.UI.CustomCommands;

namespace ForkPlus.UI.UserControls.Preferences
{
	public class CustomCommandUIControlViewModel : INotifyPropertyChanged
	{
		private CustomCommandUI.Control _control;

		private int _rowIndex;

		private string _title;

		private string _details;

		public CustomCommandUI.Control Control
		{
			get
			{
				return _control;
			}
			set
			{
				_control = value;
				if (value is CustomCommandUI.Control.GenericTextBox genericTextBox)
				{
					Title = genericTextBox.Title;
					Details = PreferencesLocalization.Translate("Text Box", ForkPlusSettings.Default.UiLanguage);
				}
				else if (value is CustomCommandUI.Control.PathTextBox pathTextBox)
				{
					Title = pathTextBox.Title;
					Details = PreferencesLocalization.Translate("Text Box", ForkPlusSettings.Default.UiLanguage);
				}
				else if (value is CustomCommandUI.Control.Dropdown dropdown)
				{
					Title = PreferencesLocalization.Translate(dropdown.Title, ForkPlusSettings.Default.UiLanguage);
					Details = PreferencesLocalization.Translate("Branch Selector", ForkPlusSettings.Default.UiLanguage);
				}
				else if (value is CustomCommandUI.Control.CheckBox checkBox)
				{
					Title = checkBox.Title;
					Details = PreferencesLocalization.Translate("Check Box", ForkPlusSettings.Default.UiLanguage);
				}
			}
		}

		public string Variable => $"${RowIndex + 1}";

		public int RowIndex
		{
			get
			{
				return _rowIndex;
			}
			set
			{
				_rowIndex = value;
				NotifyPropertyChanged("Variable");
			}
		}

		public string Title
		{
			get
			{
				return _title;
			}
			set
			{
				_title = value;
				NotifyPropertyChanged("Title");
			}
		}

		public string Details
		{
			get
			{
				return _details;
			}
			set
			{
				_details = value;
				NotifyPropertyChanged("Details");
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public CustomCommandUIControlViewModel(int rowIndex, CustomCommandUI.Control control)
		{
			RowIndex = rowIndex;
			Control = control;
		}

		private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
		{
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
