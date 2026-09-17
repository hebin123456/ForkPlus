// 端到端诊断（2026-09-04，"二进制对比显示一片空白"）：
// 真实 git 仓库 + 修改过的二进制文件 → 走 CommitUserControl 同款
// GetWorkingDirectoryFileChangesGitCommand → FileDiffControl.Content → 断言最终子视图。
// 若走不到 HexDiff/BinaryDiff 而是空白 Fallback，报错信息会给出 diff 类型与数据细节。
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Diff;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.BinaryDiff;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class BinaryDiffEndToEndTests
	{
		private static string CreateBinaryTestRepo()
		{
			string root = Path.Combine(Path.GetTempPath(), "fpbindiff_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			Directory.CreateDirectory(root);
			string oldDir = Directory.GetCurrentDirectory();
			try
			{
				Directory.SetCurrentDirectory(root);
				Run("git", "init -q");
				Run("git", "config user.email test@example.com");
				Run("git", "config user.name Test");
				// 初始二进制内容
				byte[] v1 = new byte[512];
				new Random(42).NextBytes(v1);
				File.WriteAllBytes(Path.Combine(root, "data.bin"), v1);
				Run("git", "add data.bin");
				Run("git", "commit -q -m base");
				// 修改二进制内容
				byte[] v2 = new byte[640];
				new Random(99).NextBytes(v2);
				File.WriteAllBytes(Path.Combine(root, "data.bin"), v2);
			}
			finally
			{
				Directory.SetCurrentDirectory(oldDir);
			}
			return root;
		}

		private static void Run(string exe, string args)
		{
			var psi = new System.Diagnostics.ProcessStartInfo(exe, args)
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false
			};
			using var p = System.Diagnostics.Process.Start(psi);
			string err = p.StandardError.ReadToEnd();
			p.WaitForExit();
			if (p.ExitCode != 0)
			{
				throw new Exception(exe + " " + args + " 失败: " + err);
			}
		}

		[Fact]
		public void BinaryFile_Modified_WorkingDirDiff_ShowsHexView()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string repoRoot = CreateBinaryTestRepo();
			try
			{
				// ===== 第 1 步：拿 ChangedFile（未暂存修改） =====
				var module = new GitModule(repoRoot, Path.Combine(repoRoot, ".git"), null, null);
				GitCommandResult<ChangedFilesCollection> statusResult = new GetChangedFilesGitCommand().Execute(module);
				Assert.True(statusResult.Succeeded, "git status 失败: " + statusResult.Error);
				ChangedFile binFile = statusResult.Result.ChangedFiles.FirstOrDefault(f => f.Path.EndsWith(".bin"));
				Assert.NotNull(binFile);

				// ===== 第 2 步：走 CommitUserControl.LoadWorkingDirectoryDiff 同款命令 =====
				GitCommandResult<DiffContent> diffResult = new GetWorkingDirectoryFileChangesGitCommand().Execute(
					module, binFile, null, 3, 4, false, false, false, resolvedConflict: false);
				Assert.True(diffResult.Succeeded, "diff 加载失败: " + diffResult.Error);
				ParsedDiffContent parsed = diffResult.Result as ParsedDiffContent;
				Assert.NotNull(parsed);
				Assert.True(parsed.Diff != null, "Diff 对象为 null（文件视为无变更？ChangeType=" + parsed.ChangedFile.ChangeType + "）");
				Assert.Equal(Diff.FileType.Binary, parsed.Diff.Type);

			// ===== 第 3 步：真实 FileDiffControl 展示 =====
			object[] holder = new object[1];
			string[] diagHolder = new string[1];
			int[] textLensHolder = new int[] { -1, -1 };
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var repoControl = new RepositoryUserControl();
				// GitModule 私有 setter，反射注入（OpenRepository 太重，会拉起整个仓库 UI）
				typeof(RepositoryUserControl).GetProperty("GitModule")!
					.SetValue(repoControl, module);

				var control = new FileDiffControl();
				control.RepositoryUserControl = repoControl;
				var window = new Window { Width = 900, Height = 500, Content = control };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				control.Content = diffResult;
				// 二进制路径走 JobQueue + Dispatcher.Post；v3.7.2 起默认卡片视图（简略），
				// 点击底部 Hex 按钮后才懒装配 HexDiffUserControl（内部 Task.Run）
				BinaryDiffUserControl cards = null;
				for (int i = 0; i < 50; i++)
				{
					Task.Delay(100).GetAwaiter().GetResult();
					Dispatcher.UIThread.RunJobs();
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
					if (control.CurrentSubView is BinaryDiffUserControl b)
					{
						cards = b;
						break;
					}
				}

				string diag = "subView=" + (cards == null ? "<null-空白>" : "BinaryDiffUserControl");
				if (cards != null)
				{
					diag += ", Hex按钮可见=" + cards.HexRadioButton.IsVisible;
					if (cards.HexRadioButton.IsVisible)
					{
						cards.HexRadioButton.IsChecked = true;
						Dispatcher.UIThread.RunJobs();
						for (int i = 0; i < 50; i++)
						{
							Task.Delay(100).GetAwaiter().GetResult();
							Dispatcher.UIThread.RunJobs();
							Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
							var eds = cards.GetVisualDescendants().OfType<ForkPlus.UI.Controls.Editor.Hex.HexEditor>().ToArray();
							if (eds.Length >= 2 && eds.All(e => !string.IsNullOrEmpty(e.Text)))
							{
								break;
							}
						}
					}
					var hexEditors = cards.GetVisualDescendants().OfType<ForkPlus.UI.Controls.Editor.Hex.HexEditor>().ToArray();
					for (int i = 0; i < Math.Min(2, hexEditors.Length); i++)
					{
						textLensHolder[i] = hexEditors[i].Text == null ? -1 : hexEditors[i].Text.Length;
					}
					diag += ", Hex容器可见=" + cards.HexDiffViewContainer.IsVisible
						+ ", 编辑器数量=" + hexEditors.Length
						+ ", 编辑器文本长度=[" + textLensHolder[0] + "," + textLensHolder[1] + "]";
				}
				holder[0] = cards;
				diagHolder[0] = diag;
				window.Close();
				return 0;
			}).GetAwaiter().GetResult();

			string diag = diagHolder[0] ?? "<未执行>";
			object subView = holder[0];
			// v3.7.2 行为：二进制默认简略卡片视图（BinaryDiffUserControl），底部 Side-by-Side + Hex
			// 两个切换按钮（图片场景另有 Swipe/Onion Skin）；点击 Hex 后进入 side-by-side 十六进制对比
			Assert.True(subView is BinaryDiffUserControl,
				"二进制 diff 应默认显示简略卡片视图（BinaryDiffUserControl），实际: " + diag);
			Assert.True(diag.Contains("Hex按钮可见=True"),
				"≤50MB 二进制应预载字节并提供 Hex 切换按钮，实际: " + diag);
			Assert.True(diag.Contains("Hex容器可见=True"),
				"点击 Hex 后应切换到十六进制对比视图，实际: " + diag);
			// 且编辑器有内容（非空白）
			Assert.True(textLensHolder[0] > 0 && textLensHolder[1] > 0,
				"编辑器文本为空（空白渲染）。" + diag);
			}
			finally
			{
				try { Directory.Delete(repoRoot, true); } catch { }
			}
		}
	}
}
