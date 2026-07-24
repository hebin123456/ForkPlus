// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Interactivity（RoutedEventArgs）
// - using System.Windows.Controls → using Avalonia.Controls
// - using System.Windows.Markup → 移除
// - TextChangedEventArgs → Avalonia.Controls 同名类型
using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.Controls;

namespace ForkPlus.UI.UserControls.RepositorySettings
{
	public partial class CommitTemplateUserControl : UserControl
	{
		private GitModule _gitModule;

		private bool _saveCommitTemplateRequired;

		private bool _updateInProgress;

		public CommitTemplateUserControl()
		{
			InitializeComponent();
		}

		public void Initialize(GitModule gitModule)
		{
			_gitModule = gitModule;
			Refresh();
		}

		public void Save()
		{
			if (_saveCommitTemplateRequired)
			{
				SaveCommitTemplate();
			}
		}

		private void CommitTemplateTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (!_updateInProgress)
			{
				_saveCommitTemplateRequired = true;
			}
		}

		private void UseGlobalCommitTemplateCheckbox_Changed(object sender, RoutedEventArgs e)
		{
			if (!_updateInProgress)
			{
				SaveCommitTemplate();
				Refresh();
			}
		}

		private void AddSignedOffMessageCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			if (!_updateInProgress)
			{
				_gitModule.Settings.SignOff = AddSignedOffMessageCheckBox.IsChecked.GetValueOrDefault();
				_gitModule.Settings.Save();
			}
		}

		private void SkipCommitMessageCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			if (!_updateInProgress)
			{
				bool skip = SkipCommitMessageCheckBox.IsChecked.GetValueOrDefault();
				_gitModule.Settings.SkipCommitMessage = skip;
				_gitModule.Settings.Save();
				CommitMessageRegexTextBox.IsEnabled = !skip;
			}
		}

		private void CommitMessageRegexTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (!_updateInProgress && _gitModule != null)
			{
				_gitModule.Settings.CommitMessageRegex = CommitMessageRegexTextBox.Text ?? "";
				_gitModule.Settings.Save();
			}
		}

		private void Refresh()
		{
			_updateInProgress = true;
			AddSignedOffMessageCheckBox.IsChecked = _gitModule.Settings.SignOff;
			bool skip = _gitModule.Settings.SkipCommitMessage;
			SkipCommitMessageCheckBox.IsChecked = skip;
			CommitMessageRegexTextBox.Text = _gitModule.Settings.CommitMessageRegex ?? "";
			CommitMessageRegexTextBox.IsEnabled = !skip;
			GitCommandResult<CommitTemplate> gitCommandResult = _gitModule == null ? GitCommandResult<CommitTemplate>.Failure(new GitCommandError.GenericError("Module is null")) : new GetCommitTemplateGitCommand().Execute(_gitModule, GitConfigLocation.Local);
			if (gitCommandResult.Succeeded)
			{
				UseGlobalCommitTemplateCheckBox.IsChecked = false;
				CommitTemplatePathTextBlock.Text = PathHelper.NormalizeUnix(gitCommandResult.Result.Path);
				CommitTemplateTextBox.Text = gitCommandResult.Result.StringValue;
				CommitTemplateTextBox.IsEnabled = true;
				_updateInProgress = false;
				return;
			}
			UseGlobalCommitTemplateCheckBox.IsChecked = true;
			CommitTemplateTextBox.IsEnabled = false;
			GitCommandResult<CommitTemplate> gitCommandResult2 = new GetCommitTemplateGitCommand().Execute(_gitModule, GitConfigLocation.Global);
			if (gitCommandResult2.Succeeded)
			{
				CommitTemplatePathTextBlock.Text = PathHelper.NormalizeUnix(gitCommandResult2.Result.Path);
				CommitTemplateTextBox.Text = gitCommandResult2.Result.StringValue;
			}
			else
			{
				CommitTemplatePathTextBlock.Text = "";
				CommitTemplateTextBox.Text = "";
			}
			_updateInProgress = false;
		}

		private void SaveCommitTemplate()
		{
			if (UseGlobalCommitTemplateCheckBox.IsChecked.GetValueOrDefault())
			{
				new UnsetLocalCommitTemplateGitCommand().Execute(_gitModule);
				return;
			}
			string text = CommitTemplatePathTextBlock.Text;
			string commitTemplate = CommitTemplateTextBox.Text.Replace(Environment.NewLine, "\n");
			new SetLocalCommitTemplateGitCommand().Execute(_gitModule, text, commitTemplate);
		}

	}
}
