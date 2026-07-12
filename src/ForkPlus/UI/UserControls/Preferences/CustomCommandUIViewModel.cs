using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using ForkPlus.UI.CustomCommands;

namespace ForkPlus.UI.UserControls.Preferences
{
	public class CustomCommandUIViewModel : INotifyPropertyChanged
	{
		private static readonly string UrlCommandDefaultUrl = "https://hebin.me";

		private readonly CustomCommandViewModel _parent;

		private string _dialogTitle;

		private string _dialogDescription;

		private CustomCommandUI.Control[] _controls;

		private string _controlsButtonTitle;

		private bool _button1Enabled;

		private string _button1Title;

		private CustomCommandActionViewModel _button1ActionViewModel;

		private bool _button2Enabled;

		private string _button2Title;

		private CustomCommandActionViewModel _button2ActionViewModel;

		public string DialogTitle
		{
			get
			{
				return _dialogTitle;
			}
			set
			{
				_dialogTitle = value;
				NotifyPropertyChanged("DialogTitle");
				_parent.RefreshDetails();
			}
		}

		public string DialogDescription
		{
			get
			{
				return _dialogDescription;
			}
			set
			{
				_dialogDescription = value;
				NotifyPropertyChanged("DialogDescription");
			}
		}

		public CustomCommandUI.Control[] Controls
		{
			get
			{
				return _controls;
			}
			set
			{
				_controls = value;
				RefreshControlsButtonTitle(value);
			}
		}

		public string ControlsButtonTitle
		{
			get
			{
				return _controlsButtonTitle;
			}
			set
			{
				_controlsButtonTitle = value;
				NotifyPropertyChanged("ControlsButtonTitle");
			}
		}

		public bool Button1Enabled
		{
			get
			{
				return _button1Enabled;
			}
			set
			{
				_button1Enabled = value;
				NotifyPropertyChanged("Button1Enabled");
			}
		}

		public string Button1Title
		{
			get
			{
				return _button1Title;
			}
			set
			{
				_button1Title = value;
				NotifyPropertyChanged("Button1Title");
			}
		}

		public CustomCommandActionViewModel Button1ActionViewModel
		{
			get
			{
				return _button1ActionViewModel;
			}
			set
			{
				_button1ActionViewModel = value;
				NotifyPropertyChanged("Button1ActionViewModel");
			}
		}

		public bool Button2Enabled
		{
			get
			{
				return _button2Enabled;
			}
			set
			{
				if (value != _button2Enabled)
				{
					_button2Enabled = value;
					NotifyPropertyChanged("Button2Enabled");
				}
			}
		}

		public string Button2Title
		{
			get
			{
				return _button2Title;
			}
			set
			{
				if (!(value == _button2Title))
				{
					_button2Title = value;
					NotifyPropertyChanged("Button2Title");
				}
			}
		}

		public CustomCommandActionViewModel Button2ActionViewModel
		{
			get
			{
				return _button2ActionViewModel;
			}
			set
			{
				_button2ActionViewModel = value;
				NotifyPropertyChanged("Button2ActionViewModel");
			}
		}

		public event PropertyChangedEventHandler PropertyChanged;

		public CustomCommandUIViewModel(CustomCommandUI ui, CustomCommandViewModel parent)
		{
			_parent = parent;
			DialogTitle = ui.Title;
			DialogDescription = ui.Description;
			Controls = ui.Controls;
			if (ui.Buttons.Length != 0)
			{
				Button1Enabled = true;
				CustomCommandUI.Button button = ui.Buttons[0];
				Button1Title = button.Title;
				Button1ActionViewModel = new CustomCommandActionViewModel(button.Action);
			}
			else
			{
				Button1ActionViewModel = new CustomCommandActionViewModel(new UrlCustomCommandAction(UrlCommandDefaultUrl));
			}
			if (ui.Buttons.Length > 1)
			{
				Button2Enabled = true;
				CustomCommandUI.Button button2 = ui.Buttons[1];
				Button2Title = button2.Title;
				Button2ActionViewModel = new CustomCommandActionViewModel(button2.Action);
			}
			else
			{
				Button2ActionViewModel = new CustomCommandActionViewModel(new CancelCustomCommandAction());
			}
		}

		private void RefreshControlsButtonTitle(CustomCommandUI.Control[] controls)
		{
			StringBuilder stringBuilder = new StringBuilder(4);
			for (int i = 0; i < controls.Length; i++)
			{
				stringBuilder.Append(GetTitle(controls[i]));
				if (i != controls.Length - 1)
				{
					stringBuilder.Append(", ");
				}
			}
			if (stringBuilder.Length > 0)
			{
				ControlsButtonTitle = stringBuilder.ToString();
			}
			else
			{
				ControlsButtonTitle = PreferencesLocalization.Current("No controls");
			}
		}

		private string GetTitle(CustomCommandUI.Control control)
		{
			if (control is CustomCommandUI.Control.GenericTextBox genericTextBox)
			{
				return genericTextBox.Title;
			}
			if (control is CustomCommandUI.Control.PathTextBox pathTextBox)
			{
				return pathTextBox.Title;
			}
			if (control is CustomCommandUI.Control.Dropdown dropdown)
			{
				return dropdown.Title;
			}
			if (control is CustomCommandUI.Control.CheckBox checkBox)
			{
				return checkBox.Title;
			}
			return "";
		}

		private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
		{
			this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}
	}
}
