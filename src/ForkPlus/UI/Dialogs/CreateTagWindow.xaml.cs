using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class CreateTagWindow : ForkPlusDialogWindow
	{
		private readonly GitModule _gitModule;

		private readonly Tag[] _tags;

		private readonly Remote[] _remotes;

		private readonly IGitPoint _gitPoint;

	// 阶段 3：承接 tag 名校验（ReferenceNameValidator + 重名）+ 命令预览（含 push 多远端）。
	// RefreshButtonTitle / autocomplete / PushCheckBox 持久化留 View。
	private CreateTagWindowViewModel _viewModel;

		protected override bool IsSubmitAllowed
		{
			get
			{
				_viewModel.TagName = TagNameTextBox.Text;
				(bool isAllowed, ForkPlusDialogStatus status, string statusMessage) = _viewModel.Validate();
				SetStatus(status, statusMessage ?? string.Empty);
				return isAllowed;
			}
		}

		protected override string GetCommandPreview()
		{
			_viewModel.TagName = TagNameTextBox.Text;
			_viewModel.TagMessage = TagMessageTextBox.Text;
			_viewModel.Push = PushCheckBox.IsChecked.GetValueOrDefault();
			return _viewModel.CommandPreview;
		}

		public CreateTagWindow(GitModule gitModule, RepositoryReferences refs, Remote[] remotes, IGitPoint startPoint)
		{
			InitializeComponent();
			_gitModule = gitModule;
			_tags = refs.Tags;
			_remotes = remotes;
			_gitPoint = startPoint;
			_viewModel = new CreateTagWindowViewModel(_tags, _remotes, _gitPoint);
			GitPointView.Value = startPoint;
			base.DialogTitle = Translate("Create Tag");
			base.DialogDescription = Translate("Create annotated tag at the selected point");
			ReferenceTextBox tagNameTextBox = TagNameTextBox;
			ForkPlus.Git.Reference[] tags = _tags;
			tagNameTextBox.SetAutocompleteProvider(new ReferenceNameAutocompleteProvider(tags));
			PushCheckBox.Content = Translate((remotes.Length > 1) ? "Push to all remotes" : "Push");
			PushCheckBox.IsChecked = ForkPlusSettings.Default.CreateTag_Push;
			RefreshButtonTitle();
			// InitializeComponent 期间 AddCommandPreview 已执行，但此时 TagNameTextBox 等控件尚未赋值，
			// 导致首次 RefreshCommandPreview 返回 null 折叠了预览。此处补刷一次以显示默认命令。
			RefreshCommandPreview();
		}

		protected override void OnSubmit()
		{
			RepositoryUserControl activeRepositoryUserControl = MainWindow.ActiveRepositoryUserControl;
			if (activeRepositoryUserControl == null)
			{
				return;
			}
			string tagName = TagNameTextBox.Text;
			string tagMessage = TagMessageTextBox.Text;
			bool push = PushCheckBox.IsChecked.GetValueOrDefault();
			ForkPlusSettings.Default.CreateTag_Push = push;
			ForkPlusSettings.Default.Save();
			DisableEditableControls();
			string name = Translate(push ? ("Create and push tag '" + tagName + "'") : ("Create tag '" + tagName + "'"));
			activeRepositoryUserControl.AddUndoable(name, delegate(JobMonitor monitor)
		{
			GitCommandResult result = PerformCreateTag(tagName, tagMessage, push, monitor);
			base.Dispatcher.Async(delegate
			{
				Close(result);
			});
			return result;
		}, JobFlags.SaveToLog);
	}

		private void TagNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateSubmitButton();
			RefreshCommandPreview();
		}

		private void TagMessageTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			RefreshCommandPreview();
		}

		private void PushCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			RefreshButtonTitle();
			RefreshCommandPreview();
		}

		private GitCommandResult PerformCreateTag(string tagName, string tagMessage, bool push, JobMonitor monitor)
		{
			base.Dispatcher.Async(delegate
			{
				SetStatus(ForkPlusDialogStatus.InProgress, "Creating '" + tagName + "'...");
			});
			GitCommandResult gitCommandResult = new CreateTagGitCommand().Execute(_gitModule, tagName, tagMessage, _gitPoint, monitor);
			if (!gitCommandResult.Succeeded)
			{
				return gitCommandResult;
			}
			if (!push)
			{
				return gitCommandResult;
			}
			string tagFullReference = "refs/tags/" + tagName;
			Remote[] remotes = _remotes;
			foreach (Remote remote in remotes)
			{
				base.Dispatcher.Async(delegate
				{
					SetStatus(ForkPlusDialogStatus.InProgress, "Pushing '" + tagName + "' to '" + remote.Name + "'...");
				});
				GitCommandResult gitCommandResult2 = new PushTagGitCommand().Execute(_gitModule, remote.Name, tagFullReference, monitor);
				if (!gitCommandResult2.Succeeded)
				{
					return gitCommandResult2;
				}
			}
			return gitCommandResult;
		}

		private void RefreshButtonTitle()
		{
			base.SubmitButtonTitle = Translate(PushCheckBox.IsChecked.GetValueOrDefault() ? "Create and Push" : "Create");
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
