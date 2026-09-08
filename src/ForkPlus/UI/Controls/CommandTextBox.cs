using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.QuickLaunch;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	[global::Avalonia.Controls.Metadata.TemplatePartAttribute(Name = "PART_LabelsStackPanel", Type = typeof(global::Avalonia.Controls.Control))]
	[global::Avalonia.Controls.Metadata.TemplatePartAttribute(Name = "PART_PlaceholderTextBox", Type = typeof(global::Avalonia.Controls.Control))]
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

		public CommandTextBox()
		{
			// Migration note：WPF TextBox.Text 默认 ""，Avalonia 12 默认 null——同 PlaceholderTextBox 的构造期回填，
			// 防止 `.Text.Trim()` 等 C# 调用在未输入时 NRE（剪贴板读取崩溃同根）。
			if (base.Text == null)
			{
				base.Text = string.Empty;
			}
		}

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

		protected override void OnApplyTemplate(global::Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			_labelsStackPanel = this.GetTemplateChild("PART_LabelsStackPanel") as StackPanel;
			_textBox = this.GetTemplateChild("PART_PlaceholderTextBox") as PlaceholderTextBox;
			Placeholder = Translate("Command");
			_textBox.AddHandler(global::Avalonia.Input.InputElement.KeyDownEvent,delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Back && _textBox.CaretIndex == 0)
				{
					RemoveArgument();
				}
			},global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
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
			border.Background = global::ForkPlus.UI.Theme.CommandTextBox.LabelBackgroundBrush;
			TextBlock child = new TextBlock
			{
				VerticalAlignment = VerticalAlignment.Center,
				Text = text,
				Foreground = global::ForkPlus.UI.Theme.CommandTextBox.LabelForegroundBrush
			};
			border.Child = child;
			_labelsStackPanel.Children.Add(border);
			// Migration note：WPF TextBox.Text 对 null 有 CoerceText(null→"")，Avalonia 12 没有——
			// 这里写 null 会经模板 TwoWay 绑定上传把外层 CommandTextBox.Text 也变成 null，
			// 随后 QuickLaunchWindow.RefreshCommandList 的 `.Text.Trim()` 直接 NRE
			// （Linux 用户实测：快速启动点击"切换工作区"必崩，2026-09-07）。清空请写 string.Empty。
			_textBox.Text = string.Empty;
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
