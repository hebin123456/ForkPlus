using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.UI.CustomCommands;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.UserControls
{
	public partial class PathTextBoxUserControl : UserControl
	{
		private ForkPlusDialogWindow _parentWindow;

		private CustomCommandUI.Control.PathTextBox.DialogType _dialogType;

		public string StringValue
		{
			get
			{
				return PathTextBox.Text;
			}
			set
			{
				PathTextBox.Text = value;
			}
		}

		public PathTextBoxUserControl(ForkPlusDialogWindow parentWindow, CustomCommandUI.Control.PathTextBox.DialogType dialogType)
		{
			_parentWindow = parentWindow;
			_dialogType = dialogType;
			InitializeComponent();
		}

		private void BrowseButton_Click(object sender, RoutedEventArgs e)
		{
			switch (_dialogType)
			{
			case CustomCommandUI.Control.PathTextBox.DialogType.SaveFile:
				SaveFile();
				break;
			case CustomCommandUI.Control.PathTextBox.DialogType.OpenFile:
				OpenFile();
				break;
			case CustomCommandUI.Control.PathTextBox.DialogType.OpenDirectory:
				OpenDirectory();
				break;
			}
		}

		private void SaveFile()
		{
			string readableFileName = PathHelper.GetReadableFileName(StringValue);
			string parent = PathHelper.GetParent(StringValue);
			if (OpenDialog.SelectFileSaveLocation(_parentWindow, "Save file", parent, readableFileName, out var resultFilePath))
			{
				PathTextBox.Text = resultFilePath;
			}
		}

		private void OpenFile()
		{
			string parent = PathHelper.GetParent(StringValue);
			if (OpenDialog.SelectFile(_parentWindow, "Open file", parent, "Any File", "*.*", out var filePath))
			{
				PathTextBox.Text = filePath;
			}
		}

		private void OpenDirectory()
		{
			string stringValue = StringValue;
			if (OpenDialog.SelectDirectory(_parentWindow, "Open directory", stringValue, out var directoryPath))
			{
				PathTextBox.Text = directoryPath;
			}
		}

	}
}
