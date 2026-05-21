using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class SaveAsPatchWindow : ForkPlusDialogWindow
	{
		private readonly RepositoryUserControl _repositoryUserControl;

		private readonly GitModule _gitModule;

		private Revision _revision;

		private Sha _src;

		private Sha? _dst;

		public SaveAsPatchWindow(RepositoryUserControl repositoryUserControl, GitModule gitModule, Revision revision, Sha? dst)
		{
			_repositoryUserControl = repositoryUserControl;
			_gitModule = gitModule;
			_revision = revision;
			_src = revision.Sha;
			_dst = dst;
			InitializeComponent();
			base.DialogTitle = "Create Patch";
			base.DialogDescription = "Save commit as patch";
			base.SubmitButtonTitle = "Save";
			RevisionsTextBlock.Text = Translate(dst.HasValue ? "Revisions:" : "Revision:");
		}

		protected override async void OnInitialized(EventArgs e)
		{
			base.OnInitialized(e);
			SetStatus(ForkPlusDialogStatus.InProgress, "Loading...");
			GitCommandResult<GetRevisionsInRangeGitCommand.Result> gitCommandResult = await Task.Run(() => new GetRevisionsInRangeGitCommand().Execute(_gitModule, _src, _dst));
			if (!gitCommandResult.Succeeded)
			{
				Close();
				return;
			}
			SetStatus(ForkPlusDialogStatus.None, string.Empty);
			GetRevisionsInRangeGitCommand.Result result = gitCommandResult.Result;
			RevisionsItemsControl.ItemsSource = result.Revisions;
			_src = result.Src;
			_dst = result.Dst;
		}

		protected override void OnSubmit()
		{
			string initialDirectory = ForkPlusSettings.Default.RecentPatchDirectory ?? RepositoryManager.Instance.DefaultSourceDir();
			string repositoryName = _gitModule.RepositoryName;
			string text = (_dst.HasValue ? (repositoryName + "-" + _src.ToAbbreviatedString() + "-" + _dst.Value.ToAbbreviatedString() + Consts.Git.PatchFileExtension) : (repositoryName + "-" + _src.ToAbbreviatedString() + "-" + _revision.Message));
			text = CutInvalidCharacters(text);
			if (OpenDialog.SelectPatchSaveLocation(this, Translate("Save patch as..."), initialDirectory, text, out var filePath))
			{
				ForkPlusSettings.Default.RecentPatchDirectory = Path.GetDirectoryName(filePath);
				GitCommandResult gitCommandResult = new ExportPatchGitCommand().Execute(_gitModule, _src, _dst, filePath);
				if (!gitCommandResult.Succeeded)
				{
					new ErrorWindow(_repositoryUserControl, gitCommandResult.Error).ShowDialog();
				}
			}
			Close();
		}

		private static string CutInvalidCharacters(string text)
		{
			StringBuilder stringBuilder = new StringBuilder(text);
			stringBuilder.Replace(":", "");
			return stringBuilder.ToString();
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
