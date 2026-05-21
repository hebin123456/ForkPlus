using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;

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
			DataObject.AddPastingHandler(CommitSubjectTextBox, OnCommitSubjectPaste);
		}

		public void Refresh(string subject, string description)
		{
			CommitSubjectTextBox.Text = subject;
			CommitDescriptionTextBox.Text = description;
		}

		private void OnCommitSubjectPaste(object sender, DataObjectPastingEventArgs e)
		{
			if (e.DataObject.GetDataPresent(DataFormats.UnicodeText) && e.DataObject.GetData(DataFormats.UnicodeText) is string text)
			{
				string[] array = text.Split(new string[1] { Environment.NewLine }, 2, StringSplitOptions.RemoveEmptyEntries);
				if (array.Length == 2)
				{
					e.CancelCommand();
					CommitSubjectTextBox.Text = array[0];
					CommitDescriptionTextBox.Text = array[1];
				}
			}
		}

		private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Return && Keyboard.IsKeyDown(Key.LeftCtrl))
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
			if (e.Key == Key.Return && !Keyboard.IsKeyDown(Key.LeftCtrl))
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
			if (length > ForkPlusSettings.Default.CommitSubjectHighLimit)
			{
				SubjectLengthLimitTextBlock.Foreground = Application.Current.TryFindResource("CommitSublectLength.Error.ForegroundBrush") as Brush;
			}
			else if (length > commitSubjectLowLimit)
			{
				SubjectLengthLimitTextBlock.Foreground = Application.Current.TryFindResource("CommitSublectLength.Warning.ForegroundBrush") as Brush;
			}
			else
			{
				SubjectLengthLimitTextBlock.Foreground = Application.Current.TryFindResource("CommitSublectLength.OK.ForegroundBrush") as Brush;
			}
			int num = commitSubjectLowLimit - length;
			SubjectLengthLimitTextBlock.Show();
			SubjectLengthLimitTextBlock.Text = num.ToString();
		}

	}
}
