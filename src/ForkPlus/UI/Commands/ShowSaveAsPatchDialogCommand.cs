using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Commands
{
	public class ShowSaveAsPatchDialogCommand : IUICommand, IForkPlusCommand
	{
		public string Title => "Save as Patch…";

		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		public void Execute(RepositoryUserControl repositoryUserControl, GitModule gitModule, ChangedFile[] changedFiles, bool amend)
		{
			HashSet<ChangedFile> source = new HashSet<ChangedFile>(changedFiles.Where((ChangedFile x) => !x.IsDirectory));
			if (source.Count == 0)
			{
				return;
			}
			string initialDirectory = ForkPlusSettings.Default.RecentPatchDirectory ?? global::ForkPlus.RepositoryManager.Instance.DefaultSourceDir();
			string defaultFileName = gitModule.RepositoryName + "-" + DateTime.Now.ToString("HH-mm-ss");
			if (OpenDialog.SelectPatchSaveLocation(MainWindow.Instance, "Save patch as...", initialDirectory, defaultFileName, out var filePath))
			{
				SavePatchAsync(repositoryUserControl, gitModule, source.ToArray(), amend, filePath);
			}
		}

		// 修复（2026-09-14，"另存为补丁内容多时界面卡住"）：原实现 UI 线程同步执行
		// CreatePatchGitCommand（逐文件各起一次 git 进程，N 个文件 = N 次串行 git 调用）
		// 再同步写盘，大改动时界面长时间冻结。改为：模态进度弹窗（PatchProgressWindow，
		// 含旋转 spinner 与 i/N 进度文本）+ Task.Run 后台生成与写盘，逐文件经 Dispatcher
		// 回报进度，完成后回 UI 线程收尾（记录 RecentPatchDirectory / 报错）。
		private static async void SavePatchAsync(RepositoryUserControl repositoryUserControl, GitModule gitModule, ChangedFile[] changedFiles, bool amend, string filePath)
		{
			PatchProgressWindow progressWindow = new PatchProgressWindow();
			progressWindow.SetFilePath(filePath);
			Window owner = (TopLevel.GetTopLevel(repositoryUserControl) as Window) ?? MainWindow.Instance;
			Task dialogTask = progressWindow.ShowDialog(owner);
			GitCommandResult<string> gitCommandResult;
			try
			{
				gitCommandResult = await Task.Run(delegate
				{
					return new CreatePatchGitCommand().Execute(gitModule, changedFiles, amend, delegate (int index, int total)
					{
						Dispatcher.UIThread.Post(delegate
						{
							progressWindow.UpdateProgress(index, total);
						});
					});
				});
			}
			catch (Exception ex)
			{
				Log.Error("Failed to create patch for '" + filePath + "'", ex);
				gitCommandResult = GitCommandResult<string>.Failure(ex);
			}
			if (!gitCommandResult.Succeeded)
			{
				progressWindow.Close();
				new ErrorWindow(repositoryUserControl, gitCommandResult.Error).ShowDialog();
			}
			else
			{
				try
				{
					await Task.Run(delegate
					{
						File.WriteAllText(filePath, gitCommandResult.Result);
					});
					ForkPlusSettings.Default.RecentPatchDirectory = Path.GetDirectoryName(filePath);
				}
				catch (Exception ex2)
				{
					Log.Error("Failed to write patch to '" + filePath + "'", ex2);
				}
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
