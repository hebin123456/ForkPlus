using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;

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
			base.DialogTitle = Translate("Create Patch");
			base.DialogDescription = Translate("Save commit as patch");
			base.SubmitButtonTitle = Translate("Save");
			RevisionsTextBlock.Text = Translate(dst.HasValue ? "Revisions:" : "Revision:");
			// Migration note（根因，模块19 探针实证）：WPF 原仓在 OnInitialized override 里加载
			// revisions——WPF 的 Initialized 在构造完成后触发，字段已就绪。Avalonia 12 的
			// Initialized 在 TopLevel 基类构造链中触发（PresentationSource..ctor →
			// OnAttachedToVisualTreeCore → InitializeIfNeeded → OnInitialized），此刻派生类
			// 构造器字段全未赋值（_gitModule=null → GitRequest NRE → 列表永远装配不上）。
			// Avalonia 等价时机 = 构造器尾部（InitializeComponent 之后），逻辑与原版逐字节一致。
			LoadRevisions();
		}

		private async void LoadRevisions()
		{
			try
			{
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
			RefreshCommandPreview();
			}
			catch (Exception ex)
			{
				Log.Error("SaveAsPatchWindow load revisions failed", ex);
			}
		}

		protected override string GetCommandPreview()
	{
		if (_src == Sha.Zero)
		{
			return null;
		}
		string range = _dst.HasValue ? (_src.ToAbbreviatedString() + ".." + _dst.Value.ToAbbreviatedString()) : _src.ToAbbreviatedString();
		string outputDir = ForkPlusSettings.Default.RecentPatchDirectory;
		if (!string.IsNullOrEmpty(outputDir))
		{
			string quotedDir = outputDir.IndexOf(' ') >= 0 ? ("\"" + outputDir + "\"") : outputDir;
			return "git format-patch " + range + " -o " + quotedDir;
		}
		return "git format-patch " + range;
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
				// 修复（2026-09-10，"另存为补丁点保存后取消文件选择窗口，补丁弹窗也被关掉"）：
				// 原先 Close() 在 if 块外无条件执行——用户在系统文件保存对话框点取消（SelectPatchSaveLocation
				// 返回 false）时，补丁弹窗仍被关闭，无法重试。改为仅在用户确实选了保存位置（未取消）时
				// 才关闭补丁弹窗；取消则保留弹窗，用户可重新点保存另选位置。
				Close();
			}
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
