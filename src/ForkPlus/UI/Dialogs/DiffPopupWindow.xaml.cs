using Avalonia.Input;
using System;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class DiffPopupWindow : CustomWindow
	{
		public EventHandler SelectNext;

		public EventHandler SelectPrevious;

		public EventHandler ShowLargeUntrackedChanges;

		private bool _closing;

		public FileDiffControl FileDiffControl { get; }

		public static DiffPopupWindow CreateRevisionDiff(RepositoryUserControl repositoryUserControl)
		{
			return new DiffPopupWindow(new FileDiffControl
			{
				RepositoryUserControl = repositoryUserControl
			});
		}

		public static DiffPopupWindow CreateCommitDiff(RepositoryUserControl repositoryUserControl)
		{
			return new DiffPopupWindow(new CommitFileDiffControl
			{
				RepositoryUserControl = repositoryUserControl
			});
		}

		private DiffPopupWindow(FileDiffControl fileDiffControl)
		{
			InitializeComponent();
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			base.HideMinimizeMaximizeButtons = true;
			base.ResizeMode = ResizeMode.CanResizeWithGrip;
			FileDiffControl = fileDiffControl;
			FileDiffControl.Target = FileDiffControlTarget.Popup;
			VisualTreeAttachmentHelper.TrySetChild(FileDiffControlContainer, FileDiffControl, GetType().Name + ".FileDiffControlContainer");
			FileDiffControl fileDiffControl2 = FileDiffControl;
			fileDiffControl2.ShowLargeUntrackedChanges = (EventHandler)Delegate.Combine(fileDiffControl2.ShowLargeUntrackedChanges, (EventHandler)delegate
			{
				ShowLargeUntrackedChanges?.Invoke(this, EventArgs.Empty);
			});
			base.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Space && !Keyboard.IsKeyDown(Key.LeftCtrl))
				{
					e.Handled = true;
					Close();
				}
			};
			base.PreviewKeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Escape)
				{
					e.Handled = true;
					Close();
				}
				else if (e.Key == Key.Up)
				{
					e.Handled = true;
					SelectPrevious?.Invoke(this, EventArgs.Empty);
				}
				else if (e.Key == Key.Down)
				{
					e.Handled = true;
					SelectNext?.Invoke(this, EventArgs.Empty);
				}
			};
			base.Deactivated += delegate
			{
				CloseWindow();
			};
		}

		public void UpdateDiff(GitCommandResult<DiffContent> fileContent)
		{
			FileDiffControl.Content = fileContent;
			base.Title = fileContent?.Result?.ChangedFile.Path ?? "File Preview";
		}

		private void CloseWindow()
		{
			if (_closing)
			{
				return;
			}
			_closing = true;
			try
			{
				Close();
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to close window", ex);
			}
			finally
			{
				MainWindow.Instance.PreventRefreshAfterChildDialogClose(GetType().Name);
			}
		}

	}
}
