// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Interactivity（RoutedEventArgs）
// - using System.Windows.Controls → using Avalonia.Controls（UserControl/TextChangedEventArgs）
// - using System.Windows.Input → using Avalonia.Input（Key/KeyEventArgs）
// - using System.Windows.Markup → 移除
// - using System.Windows.Media → using Avalonia.Media（Brush）
// - 新增 using ForkPlus.UI.Helpers（KeyboardHelper）
// - Keyboard.IsKeyDown(Key.LeftCtrl) → KeyboardHelper.IsCtrlDown（参考 KeyboardHelper）
// - Application.Current.TryFindResource("X") as Brush → Theme.FindBrush("X")（参考 CommitUserControl）
// - DataObject.AddPastingHandler + DataObjectPastingEventArgs 无 Avalonia 等价，移除并注释（参考 ReferenceTextBox）
using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.UserControls
{
	public partial class RewordUserControl : UserControl
	{
		private bool _focused;

		public string Message => CommitMessageHelper.CreateCommitBody(CommitSubjectTextBox.Text, CommitDescriptionTextBox.Text);

		public event EventHandler<EventArgs> RewordCancelled;

		public event EventHandler<EventArgs> MessageChanged;

		public void RaiseRewordCancelled()
		{
			this.RewordCancelled?.Invoke(this, EventArgs.Empty);
		}

		public void RaiseMessageChanged()
		{
			this.MessageChanged?.Invoke(this, EventArgs.Empty);
		}

		public RewordUserControl(string subject, string description)
		{
			InitializeComponent();
			Refresh(subject, description);
			CommitSubjectTextBox.CaretIndex = CommitSubjectTextBox.Text.Length;
			base.LayoutUpdated += delegate
			{
				if (!_focused)
				{
					Focus();
					CommitSubjectTextBox.Focus();
					_focused = true;
				}
			};
			// TODO(4.5): Avalonia 无 DataObject.AddPastingHandler 等价 API，需自定义粘贴处理（参考 ReferenceTextBox）。
			// DataObject.AddPastingHandler(CommitSubjectTextBox, OnCommitSubjectPaste);
		}

		public void Refresh(string subject, string description)
		{
			CommitSubjectTextBox.Text = subject;
			CommitDescriptionTextBox.Text = description;
		}

		// TODO(4.5): Avalonia 无 DataObjectPastingEventArgs 等价类型，需自定义粘贴处理（参考 ReferenceTextBox）。
		// 以下 OnCommitSubjectPaste 逻辑保留以便后续实现粘贴过滤时参考。
		// private void OnCommitSubjectPaste(object sender, DataObjectPastingEventArgs e)
		// {
		// 	if (e.DataObject.GetDataPresent(DataFormats.UnicodeText) && e.DataObject.GetData(DataFormats.UnicodeText) is string text)
		// 	{
		// 		string[] array = text.Split(new string[1] { Environment.NewLine }, 2, StringSplitOptions.RemoveEmptyEntries);
		// 		if (array.Length == 2)
		// 		{
		// 			e.CancelCommand();
		// 			CommitSubjectTextBox.Text = array[0];
		// 			CommitDescriptionTextBox.Text = array[1];
		// 		}
		// 	}
		// }

		private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			// 阶段 4.5：WPF Keyboard.IsKeyDown(Key.LeftCtrl) → Avalonia KeyboardHelper.IsCtrlDown（参考 KeyboardHelper）。
			if (e.Key == Key.Return && KeyboardHelper.IsCtrlDown)
			{
				RaiseMessageChanged();
				e.Handled = true;
			}
			if (e.Key == Key.Escape)
			{
				RaiseRewordCancelled();
				e.Handled = true;
			}
		}

		private void CommitSubjectTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			// 阶段 4.5：WPF Keyboard.IsKeyDown(Key.LeftCtrl) → Avalonia KeyboardHelper.IsCtrlDown（参考 KeyboardHelper）。
			if (e.Key == Key.Return && !KeyboardHelper.IsCtrlDown)
			{
				CommitDescriptionTextBox.Focus();
				CommitDescriptionTextBox.CaretIndex = CommitDescriptionTextBox.Text.Length;
				e.Handled = true;
			}
		}

		private void CommitDescriptionTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Back && CommitDescriptionTextBox.CaretIndex == 0 && CommitDescriptionTextBox.SelectionLength == 0)
			{
				CommitSubjectTextBox.Focus();
				CommitSubjectTextBox.CaretIndex = CommitSubjectTextBox.Text.Length;
				e.Handled = true;
			}
		}

		private void CommitSubjectTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubjectLengthLimit();
		}

		private void CommitMessageCancelButton_Click(object sender, RoutedEventArgs e)
		{
			RaiseRewordCancelled();
		}

		private void CommitMessageOkButton_Click(object sender, RoutedEventArgs e)
		{
			RaiseMessageChanged();
		}

		private void UpdateSubjectLengthLimit()
		{
			int commitSubjectLowLimit = ForkPlusSettings.Default.CommitSubjectLowLimit;
			int length = CommitSubjectTextBox.Text.Length;
			if (length == 0)
			{
				SubjectLengthLimitTextBlock.Hide();
				return;
			}
			// 阶段 4.5：WPF Application.Current.TryFindResource("X") as Brush → Theme.FindBrush("X")（参考 CommitUserControl）。
			if (length > ForkPlusSettings.Default.CommitSubjectHighLimit)
			{
				SubjectLengthLimitTextBlock.Foreground = Theme.FindBrush("CommitSublectLength.Error.ForegroundBrush");
			}
			else if (length > commitSubjectLowLimit)
			{
				SubjectLengthLimitTextBlock.Foreground = Theme.FindBrush("CommitSublectLength.Warning.ForegroundBrush");
			}
			else
			{
				SubjectLengthLimitTextBlock.Foreground = Theme.FindBrush("CommitSublectLength.OK.ForegroundBrush");
			}
			int num = commitSubjectLowLimit - length;
			SubjectLengthLimitTextBlock.Show();
			SubjectLengthLimitTextBlock.Text = num.ToString();
		}

	}
}
