using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	/// <summary>
	/// 把贮藏另存为补丁文件。与谱系（ShowSaveAsPatchDialogCommand，工作区改动另存为补丁）
	/// 保持一致的交互：系统保存对话框选路径 → 模态进度弹窗 → 后台线程生成并写盘 →
	/// 记录 RecentPatchDirectory；出错弹 ErrorWindow。
	/// </summary>
	public class SaveStashAsPatchCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Save as Patch…";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, StashRevision stash)
		{
			GitModule gitModule = repositoryUserControl?.GitModule;
			if (gitModule == null || stash == null)
			{
				return;
			}
			string initialDirectory = ForkPlusSettings.Default.RecentPatchDirectory ?? global::ForkPlus.RepositoryManager.Instance.DefaultSourceDir();
			string defaultFileName = gitModule.RepositoryName + "-" + DateTime.Now.ToString("HH-mm-ss");
			if (OpenDialog.SelectPatchSaveLocation(MainWindow.Instance, "Save patch as...", initialDirectory, defaultFileName, out var filePath))
			{
				SavePatchAsync(repositoryUserControl, gitModule, stash, filePath);
			}
		}

		// 贮藏补丁可能很大，UI 线程同步执行会冻结界面；沿用 ShowSaveAsPatchDialogCommand
		// 的进度弹窗 + Task.Run 后台生成模式（此处为单次 git 调用，不回报逐文件进度）。
		private static async void SavePatchAsync(RepositoryUserControl repositoryUserControl, GitModule gitModule, StashRevision stash, string filePath)
		{
			PatchProgressWindow progressWindow = new PatchProgressWindow();
			progressWindow.SetFilePath(filePath);
			Window owner = (TopLevel.GetTopLevel(repositoryUserControl) as Window) ?? MainWindow.Instance;
			Task dialogTask = progressWindow.ShowDialog(owner);
			GitCommandResult gitCommandResult;
			try
			{
				gitCommandResult = await Task.Run(delegate
				{
					return new ExportStashPatchGitCommand().Execute(gitModule, stash.ReflogName, filePath);
				});
			}
			catch (Exception ex)
			{
				Log.Error("Failed to create stash patch for '" + filePath + "'", ex);
				gitCommandResult = GitCommandResult.Failure(ex);
			}
			if (!gitCommandResult.Succeeded)
			{
				progressWindow.Close();
				new ErrorWindow(repositoryUserControl, gitCommandResult.Error).ShowDialog();
			}
			else
			{
				ForkPlusSettings.Default.RecentPatchDirectory = Path.GetDirectoryName(filePath);
				progressWindow.Close();
			}
			try
			{
				await dialogTask;
			}
			catch
			{
			}
		}
	}
}