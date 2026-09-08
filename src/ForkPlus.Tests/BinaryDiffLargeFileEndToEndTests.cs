// 端到端诊断（2026-09-04，"二进制对比显示一片空白"续）：
// >10MB 二进制文件走 CanLoadHexDiff=false 分支 → BinaryDiffUserControl（side-by-side
// 文件扩展名图标+大小视图）。该路径经过 BinaryContentUserControl.SetContent →
// IconTools / FileHelper / FileSizeFormatter，任一抛异常都会让 initialize delegate
// 中断，ShowSubView 换完子视图却没填内容 → 一片空白。
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
	public class BinaryDiffLargeFileEndToEndTests
	{
		private static string CreateLargeBinaryTestRepo()
		{
			string root = Path.Combine(Path.GetTempPath(), "fpbindiff2_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			Directory.CreateDirectory(root);
			string oldDir = Directory.GetCurrentDirectory();
			try
			{
				Directory.SetCurrentDirectory(root);
				Run("git", "init -q");
				Run("git", "config user.email test@example.com");
				Run("git", "config user.name Test");
				// >10MB：两个 11MB 文件（超出 MaxHexDiffSize=10MB → CanLoadHexDiff=false）
				byte[] v1 = new byte[11 * 1024 * 1024];
				new Random(7).NextBytes(v1);
				File.WriteAllBytes(Path.Combine(root, "blob.bin"), v1);
				Run("git", "add blob.bin");
				Run("git", "commit -q -m base");
				byte[] v2 = new byte[11 * 1024 * 1024];
				new Random(8).NextBytes(v2);
				File.WriteAllBytes(Path.Combine(root, "blob.bin"), v2);
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
		public void LargeBinaryFile_Modified_WorkingDirDiff_ShowsBinaryDiffViewWithContent()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string repoRoot = CreateLargeBinaryTestRepo();
			try
			{
				var module = new GitModule(repoRoot, Path.Combine(repoRoot, ".git"), null, null);
				GitCommandResult<ChangedFilesCollection> statusResult = new GetChangedFilesGitCommand().Execute(module);
				Assert.True(statusResult.Succeeded, "git status 失败: " + statusResult.Error);
				ChangedFile binFile = statusResult.Result.ChangedFiles.FirstOrDefault(f => f.Path.EndsWith(".bin"));
				Assert.NotNull(binFile);

				GitCommandResult<DiffContent> diffResult = new GetWorkingDirectoryFileChangesGitCommand().Execute(
					module, binFile, null, 3, 4, false, false, false, resolvedConflict: false);
				Assert.True(diffResult.Succeeded, "diff 加载失败: " + diffResult.Error);
				ParsedDiffContent parsed = diffResult.Result as ParsedDiffContent;
				Assert.NotNull(parsed);
				Assert.True(parsed.Diff != null && parsed.Diff.Type == Diff.FileType.Binary, "应为二进制 diff");

				// 走 FileDiffControl（真实宿主）
				object[] holder = new object[1];
				string[] diagHolder = new string[1];
				Dispatcher.UIThread.InvokeAsync(delegate
				{
					var repoControl = new RepositoryUserControl();
					typeof(RepositoryUserControl).GetProperty("GitModule")!
						.SetValue(repoControl, module);

					var control = new FileDiffControl();
					control.RepositoryUserControl = repoControl;
					var window = new Window { Width = 900, Height = 500, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();

					control.Content = diffResult;
					Task.Delay(600).GetAwaiter().GetResult();
					Dispatcher.UIThread.RunJobs();
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
					Task.Delay(200).GetAwaiter().GetResult();
					Dispatcher.UIThread.RunJobs();

					object sub = control.CurrentSubView;
					holder[0] = sub;
					var subVisual = sub as Avalonia.Visual;
					// BinaryDiffUserControl 内应有两个 BinaryContentUserControl，
					// 且 FileContainer 可见、DescriprionTextBlock 有文件大小文本
					var binControls = (subVisual?.GetVisualDescendants().OfType<BinaryContentUserControl>() ?? Enumerable.Empty<BinaryContentUserControl>()).ToArray();
					string[] descs = binControls.Select(b =>
					{
						var tb = b.GetVisualDescendants().OfType<TextBlock>().ToArray();
						return "TextBlocks=[" + string.Join("|", tb.Select(t => (t.Text ?? "<null>").Trim())) + "]";
					}).ToArray();
					diagHolder[0] = "subView=" + (sub == null ? "<null>" : sub.GetType().Name)
						+ ", BinaryContentUserControl 数=" + binControls.Length
						+ ", 内容=" + string.Join(" ;; ", descs);
					window.Close();
					return 0;
				}).GetAwaiter().GetResult();

				string diag = diagHolder[0] ?? "<未执行>";
				object subView = holder[0];
				// >10MB → BinaryDiffUserControl（大小+扩展名图标 side-by-side）
				Assert.True(subView is BinaryDiffUserControl,
					"大文件二进制 diff 应显示 BinaryDiffUserControl，实际: " + diag);
				// 描述文本包含文件大小（如 "11 MB"），非空 → 非空白
				Assert.Contains("MB", diag);
				Assert.True(diag.Contains("11"), "应显示文件大小，实际: " + diag);
			}
			finally
			{
				try { Directory.Delete(repoRoot, true); } catch { }
			}
		}
	}
}
