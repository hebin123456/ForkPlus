using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.QuickLaunch;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Controls
{
	[TemplatePart(Name = "PART_LabelsStackPanel", Type = typeof(FrameworkElement))]
	[TemplatePart(Name = "PART_PlaceholderTextBox", Type = typeof(FrameworkElement))]
	public class CommandTextBox : TextBox
	{
		public EventHandler<object[]> CommandArgumentsCompleted;

		public EventHandler CommandArgumentChanged;

		private const string PartNameLabelsStackPanel = "PART_LabelsStackPanel";

		private const string PartNameTextBox = "PART_PlaceholderTextBox";

		private int _activeArgumentIndex = -1;

		private Stack<object> _completedArguments = new Stack<object>();

		private StackPanel _labelsStackPanel;

		private PlaceholderTextBox _textBox;

		public Argument CurrentCommandArgument
		{
			get
			{
				if (CommandDescriptor != null && _activeArgumentIndex >= 0 && _activeArgumentIndex <= CommandDescriptor.Arguments.Length)
				{
					return CommandDescriptor.Arguments[_activeArgumentIndex];
				}
				return null;
			}
		}

		public CommandDescriptor CommandDescriptor { get; private set; }

		private string Placeholder
		{
			get
			{
				return _textBox.Placeholder;
			}
			set
			{
				_textBox.Placeholder = value;
			}
		}

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();
			_labelsStackPanel = GetTemplateChild("PART_LabelsStackPanel") as StackPanel;
			_textBox = GetTemplateChild("PART_PlaceholderTextBox") as PlaceholderTextBox;
			Placeholder = Translate("Command");
			_textBox.PreviewKeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Back && _textBox.CaretIndex == 0)
				{
					RemoveArgument();
				}
			};
			_textBox.Focus();
		}

		public void SetCommandDescriptor(CommandDescriptor commandDescriptor)
		{
			if (commandDescriptor == null)
			{
				if (commandDescriptor == CommandDescriptor)
				{
					return;
				}
				_activeArgumentIndex = -1;
			}
			else
			{
				if (commandDescriptor.Arguments.Length == 0)
				{
					throw new Exception("Command doesn't contain arguments and must be run immediately");
				}
				PushSection(Translate(commandDescriptor.Name));
				_activeArgumentIndex = 0;
			}
			CommandDescriptor = commandDescriptor;
			Placeholder = Translate(CurrentCommandArgument?.Name ?? "Command");
			_completedArguments.Clear();
			CommandArgumentChanged?.Invoke(this, EventArgs.Empty);
		}

		public void MoveNextArgument(CommandProviderItem currentItem)
		{
			if (CommandDescriptor != null && _activeArgumentIndex >= 0)
			{
				_completedArguments.Push(currentItem.Argument);
				if (_activeArgumentIndex == CommandDescriptor.Arguments.Length - 1)
				{
					CommandArgumentsCompleted?.Invoke(this, _completedArguments.ToArray());
					return;
				}
				PushSection(currentItem.Title);
				_activeArgumentIndex++;
				Placeholder = Translate(CurrentCommandArgument.Name);
			}
		}

		private void RemoveArgument()
		{
			PopSection();
			if (_activeArgumentIndex != -1 && CurrentCommandArgument != null)
			{
				if (_activeArgumentIndex == 0)
				{
					SetCommandDescriptor(null);
					return;
				}
				_activeArgumentIndex--;
				_completedArguments.Pop();
				Placeholder = Translate(CurrentCommandArgument.Name);
				CommandArgumentChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

		private void PushSection(string text)
		{
			Border border = new Border();
			border.Padding = new Thickness(6.0, 0.0, 6.0, 2.0);
			border.Margin = new Thickness(2.0, 0.0, 0.0, 0.0);
			border.CornerRadius = new CornerRadius(3.0, 3.0, 3.0, 3.0);
			border.Background = Theme.CommandTextBox.LabelBackgroundBrush;
			TextBlock child = new TextBlock
			{
				VerticalAlignment = VerticalAlignment.Center,
				Text = text,
				Foreground = Theme.CommandTextBox.LabelForegroundBrush
			};
			border.Child = child;
			_labelsStackPanel.Children.Add(border);
			_textBox.Text = null;
		}

		private void PopSection()
		{
			if (_labelsStackPanel.Children.Count > 0)
			{
				int index = _labelsStackPanel.Children.Count - 1;
				_labelsStackPanel.Children.RemoveAt(index);
			}
		}
	}
}
