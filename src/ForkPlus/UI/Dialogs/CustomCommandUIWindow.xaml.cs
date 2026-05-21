using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.UI.Controls;
using ForkPlus.UI.CustomCommands;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Dialogs
{
	public partial class CustomCommandUIWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly string _customCommandName;

		private readonly CustomCommandUI _customCommandUI;

		private readonly CustomCommandEnvironment _env;

		private readonly CustomCommandUI.Button button1;

		private readonly CustomCommandUI.Button button2;

		public CustomCommandUIWindow(RepositoryUserControl repositoryUserControl, string customCommandName, CustomCommandUI customCommandUI, CustomCommandEnvironment env)
		{
			_repositoryUserControl = repositoryUserControl;
			_customCommandName = customCommandName;
			_customCommandUI = customCommandUI;
			_env = env;
			InitializeComponent();
			base.DialogTitle = _env.ReplaceVariablesWithValues(customCommandUI.Title);
			base.DialogDescription = _env.ReplaceVariablesWithValues(customCommandUI.Description);
			base.ShowSubmitButton = false;
			base.ShowCancelButton = false;
			if (customCommandUI.Controls.Length != 0)
			{
				CreateControls(customCommandUI.Controls);
			}
			if (customCommandUI.Buttons.Length != 0)
			{
				button1 = customCommandUI.Buttons.FirstItem();
				base.SubmitButtonTitle = _env.ReplaceVariablesWithValues(button1.Title);
				base.ShowSubmitButton = true;
			}
			if (customCommandUI.Buttons.Length > 1)
			{
				button2 = customCommandUI.Buttons.LastItem();
				base.CancelButtonTitle = _env.ReplaceVariablesWithValues(button2.Title);
				base.ShowCancelButton = true;
			}
		}

		protected override void OnSubmit()
		{
			if (button1 == null)
			{
				return;
			}
			if (button1.Action is CancelCustomCommandAction)
			{
				base.OnCancel();
				return;
			}
			List<CustomCommandEnvironment.Parameter> list = new List<CustomCommandEnvironment.Parameter>(_env.Parameters);
			int num = 0;
			for (int i = 0; i < _customCommandUI.Controls.Length; i++)
			{
				CustomCommandUI.Control control = _customCommandUI.Controls[i];
				if (control is CustomCommandUI.Control.GenericTextBox)
				{
					num++;
					TextBox textBox = ControlsContainer.Children[num] as TextBox;
					list.Add(new CustomCommandEnvironment.TextParameter(textBox.Text));
					num++;
				}
				else if (control is CustomCommandUI.Control.PathTextBox)
				{
					num++;
					PathTextBoxUserControl pathTextBoxUserControl = ControlsContainer.Children[num] as PathTextBoxUserControl;
					list.Add(new CustomCommandEnvironment.PathParameter(pathTextBoxUserControl.StringValue));
					num++;
				}
				else if (control is CustomCommandUI.Control.Dropdown)
				{
					num++;
					ReferenceDropdownUserControl referenceDropdownUserControl = ControlsContainer.Children[num] as ReferenceDropdownUserControl;
					list.Add(new CustomCommandEnvironment.ReferenceParameter(referenceDropdownUserControl.SelectedReference));
					num++;
				}
				else if (control is CustomCommandUI.Control.CheckBox)
				{
					CustomCommandCheckBox customCommandCheckBox = ControlsContainer.Children[num] as CustomCommandCheckBox;
					list.Add(customCommandCheckBox.IsChecked.GetValueOrDefault() ? new CustomCommandEnvironment.OptionalTextParameter(customCommandCheckBox.CheckedValue) : new CustomCommandEnvironment.OptionalTextParameter(customCommandCheckBox.UncheckedValue));
					num++;
				}
			}
			CustomCommandEnvironment env = new CustomCommandEnvironment(_env.GitModule, list.ToArray());
			button1.Action.Execute(_repositoryUserControl, _customCommandName, env);
			base.OnSubmit();
		}

		protected override void OnCancel()
		{
			if (button2 == null)
			{
				return;
			}
			if (button2.Action is CancelCustomCommandAction)
			{
				base.OnCancel();
				return;
			}
			List<CustomCommandEnvironment.Parameter> list = new List<CustomCommandEnvironment.Parameter>(_env.Parameters);
			int num = 0;
			for (int i = 0; i < _customCommandUI.Controls.Length; i++)
			{
				CustomCommandUI.Control control = _customCommandUI.Controls[i];
				if (control is CustomCommandUI.Control.GenericTextBox)
				{
					num++;
					TextBox textBox = ControlsContainer.Children[num] as TextBox;
					list.Add(new CustomCommandEnvironment.TextParameter(textBox.Text));
					num++;
				}
				else if (control is CustomCommandUI.Control.PathTextBox)
				{
					num++;
					PathTextBoxUserControl pathTextBoxUserControl = ControlsContainer.Children[num] as PathTextBoxUserControl;
					list.Add(new CustomCommandEnvironment.PathParameter(pathTextBoxUserControl.StringValue));
					num++;
				}
				else if (control is CustomCommandUI.Control.Dropdown)
				{
					num++;
					ReferenceDropdownUserControl referenceDropdownUserControl = ControlsContainer.Children[num] as ReferenceDropdownUserControl;
					list.Add(new CustomCommandEnvironment.ReferenceParameter(referenceDropdownUserControl.SelectedReference));
					num++;
				}
				else if (control is CustomCommandUI.Control.CheckBox)
				{
					CustomCommandCheckBox customCommandCheckBox = ControlsContainer.Children[num] as CustomCommandCheckBox;
					list.Add(customCommandCheckBox.IsChecked.GetValueOrDefault() ? new CustomCommandEnvironment.OptionalTextParameter(customCommandCheckBox.CheckedValue) : new CustomCommandEnvironment.OptionalTextParameter(customCommandCheckBox.UncheckedValue));
					num++;
				}
			}
			CustomCommandEnvironment env = new CustomCommandEnvironment(_env.GitModule, list.ToArray());
			button2.Action.Execute(_repositoryUserControl, _customCommandName, env);
			Close();
		}

		private void CreateControls(CustomCommandUI.Control[] controls)
		{
			int num = 0;
			foreach (CustomCommandUI.Control control in controls)
			{
				if (control is CustomCommandUI.Control.GenericTextBox genericTextBox)
				{
					TextBlock element = CreateTitleTextBlock(genericTextBox.Title, num);
					PlaceholderTextBox element2 = CreateGenericTextBox(genericTextBox, num);
					ControlsContainer.RowDefinitions.Add(new RowDefinition
					{
						Height = GridLength.Auto
					});
					ControlsContainer.Children.Add(element);
					ControlsContainer.Children.Add(element2);
				}
				else if (control is CustomCommandUI.Control.PathTextBox pathTextBox)
				{
					TextBlock element3 = CreateTitleTextBlock(pathTextBox.Title, num);
					PathTextBoxUserControl element4 = CreatePathTextBox(pathTextBox, num);
					ControlsContainer.RowDefinitions.Add(new RowDefinition
					{
						Height = GridLength.Auto
					});
					ControlsContainer.Children.Add(element3);
					ControlsContainer.Children.Add(element4);
				}
				else if (control is CustomCommandUI.Control.Dropdown dropdown)
				{
					TextBlock element5 = CreateTitleTextBlock(dropdown.Title, num);
					ReferenceDropdownUserControl element6 = CreateReferenceDropdown(dropdown, num);
					ControlsContainer.RowDefinitions.Add(new RowDefinition
					{
						Height = GridLength.Auto
					});
					ControlsContainer.Children.Add(element5);
					ControlsContainer.Children.Add(element6);
				}
				else if (control is CustomCommandUI.Control.CheckBox checkBox)
				{
					CustomCommandCheckBox element7 = CreateCheckBox(checkBox, num);
					ControlsContainer.RowDefinitions.Add(new RowDefinition
					{
						Height = GridLength.Auto
					});
					ControlsContainer.Children.Add(element7);
				}
				num++;
			}
		}

		private TextBlock CreateTitleTextBlock(string title, int rowIndex)
		{
			TextBlock obj = new TextBlock
			{
				Text = title + ":",
				TextAlignment = TextAlignment.Right,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0.0, 4.0, 0.0, 4.0),
				FontSize = 13.0
			};
			Grid.SetColumn(obj, 0);
			Grid.SetRow(obj, rowIndex);
			return obj;
		}

		private PlaceholderTextBox CreateGenericTextBox(CustomCommandUI.Control.GenericTextBox genericTextBox, int rowIndex)
		{
			PlaceholderTextBox obj = new PlaceholderTextBox
			{
				Text = genericTextBox.Text,
				Placeholder = genericTextBox.Placeholder,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(8.0, 4.0, 0.0, 4.0),
				Padding = new Thickness(4.0, 2.0, 4.0, 2.0)
			};
			Grid.SetColumn(obj, 1);
			Grid.SetRow(obj, rowIndex);
			return obj;
		}

		private PathTextBoxUserControl CreatePathTextBox(CustomCommandUI.Control.PathTextBox pathTextBox, int rowIndex)
		{
			PathTextBoxUserControl obj = new PathTextBoxUserControl(this, pathTextBox.PathDialogType)
			{
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(8.0, 4.0, 0.0, 4.0)
			};
			string text = "";
			string text2 = pathTextBox.DefaultDirectory;
			if (text2 != null)
			{
				if (!Path.IsPathRooted(text2))
				{
					text2 = Path.Combine(_env.GitModule.Path, text2);
				}
				text = ((pathTextBox.PathDialogType == CustomCommandUI.Control.PathTextBox.DialogType.OpenDirectory) ? text2 : Path.Combine(text2, pathTextBox.FileName));
			}
			else
			{
				string fileName = pathTextBox.FileName;
				text = ((fileName == null || pathTextBox.PathDialogType == CustomCommandUI.Control.PathTextBox.DialogType.OpenDirectory) ? "" : Path.Combine(_env.GitModule.Path, fileName));
			}
			obj.StringValue = text;
			Grid.SetColumn(obj, 1);
			Grid.SetRow(obj, rowIndex);
			return obj;
		}

		private ReferenceDropdownUserControl CreateReferenceDropdown(CustomCommandUI.Control.Dropdown dropdown, int rowIndex)
		{
			ReferenceDropdownUserControl obj = new ReferenceDropdownUserControl(_repositoryUserControl.RepositoryData, dropdown)
			{
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(8.0, 4.0, 0.0, 4.0)
			};
			Grid.SetColumn(obj, 1);
			Grid.SetRow(obj, rowIndex);
			return obj;
		}

		private CustomCommandCheckBox CreateCheckBox(CustomCommandUI.Control.CheckBox checkBox, int rowIndex)
		{
			CustomCommandCheckBox obj = new CustomCommandCheckBox(checkBox)
			{
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(8.0, 4.0, 0.0, 4.0),
				FontSize = 13.0
			};
			Grid.SetColumn(obj, 1);
			Grid.SetRow(obj, rowIndex);
			return obj;
		}

	}
}
