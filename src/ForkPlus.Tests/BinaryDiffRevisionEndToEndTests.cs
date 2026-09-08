// 端到端诊断（2026-09-04，"二进制对比显示一片空白"续）：
// 场景4：提交历史（All Commits tab）查看改变了二进制文件的提交——
// GetRevisionFileChangesGitCommand → ParsedDiffContent(Binary) → FileDiffControl
// → LoadUnknownBinaryDiffContent + LoadHexDiffContent → HexDiffUserControl。
// 与工作区 diff 的差异：ChangedFile 来自提交树而非 status；dst 走
// BlobTarget.Unstaged（工作区文件）——文件在后续提交中被改过/删过时行为不同。
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
using ForkPlus.Git.Interaction;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class BinaryDiffRevisionEndToEndTests
	{
		private static string RunGit(string args, string cwd = null)
		{
			var psi = new System.Diagnostics.ProcessStartInfo("git", args)
			{
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				WorkingDirectory = cwd ?? Directory.GetCurrentDirectory()
			};
			using var p = System.Diagnostics.Process.Start(psi);
			string err = p.StandardError.ReadToEnd();
			p.WaitForExit();
			if (p.ExitCode != 0)
			{
				throw new Exception("git " + args + " 失败: " + err);
			}
			return p.StandardOutput.ReadToEnd();
		}

		private static string CreateRepoWithBinaryCommit()
		{
			string root = Path.Combine(Path.GetTempPath(), "fpbindiff4_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			Directory.CreateDirectory(root);
			string oldDir = Directory.GetCurrentDirectory();
			try
			{
				Directory.SetCurrentDirectory(root);
				RunGit("init -q");
				RunGit("config user.email test@example.com");
				RunGit("config user.name Test");
				byte[] v1 = new byte[400];
				new Random(11).NextBytes(v1);
				File.WriteAllBytes(Path.Combine(root, "data.bin"), v1);
				RunGit("add data.bin");
				RunGit("commit -q -m base");
				byte[] v2 = new byte[480];
				new Random(22).NextBytes(v2);
				File.WriteAllBytes(Path.Combine(root, "data.bin"), v2);
				RunGit("add data.bin");
				RunGit("commit -q -m modify-binary");
				// 再改一次工作区（模拟"文件在后续被改过"的常见情形）
				byte[] v3 = new byte[500];
				new Random(33).NextBytes(v3);
				File.WriteAllBytes(Path.Combine(root, "data.bin"), v3);
			}
			finally
			{
				Directory.SetCurrentDirectory(oldDir);
			}
			return root;
		}

		[Fact]
		public void CommittedBinaryChange_RevisionDiff_ShowsHexViewWithContent()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string repoRoot = CreateRepoWithBinaryCommit();
			try
			{
				var module = new GitModule(repoRoot, Path.Combine(repoRoot, ".git"), null, null);
				// 拿 HEAD 提交里的 ChangedFile
				string shaStr = RunGit("rev-parse HEAD", repoRoot).Trim();
				GitCommandResult<ChangedFilesCollection> statusResult = new GetChangedFilesGitCommand().Execute(module);
				Assert.True(statusResult.Succeeded, "git status 失败: " + statusResult.Error);
				ChangedFile binFile = statusResult.Result.ChangedFiles.FirstOrDefault(f => f.Path.EndsWith(".bin"));

				var target = new RevisionDiffTarget.Revision(Sha.Parse(shaStr).GetValueOrDefault());
				GitCommandResult<DiffContent> diffResult = new GetRevisionFileChangesGitCommand()
					.Execute(module, target, binFile, 3, 4, false, false);
				Assert.True(diffResult.Succeeded, "revision diff 加载失败: " + diffResult.Error);
				ParsedDiffContent parsed = diffResult.Result as ParsedDiffContent;
				Assert.NotNull(parsed);
				Assert.True(parsed.Diff != null && parsed.Diff.Type == Diff.FileType.Binary,
					"revision diff 应为二进制类型");

				object[] holder = new object[1];
				string[] diagHolder = new string[1];
				int[] textLens = new int[] { -1, -1 };
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
					var hexEditors = (subVisual?.GetVisualDescendants().OfType<ForkPlus.UI.Controls.Editor.Hex.HexEditor>() ?? Enumerable.Empty<ForkPlus.UI.Controls.Editor.Hex.HexEditor>()).ToArray();
					for (int i = 0; i < Math.Min(2, hexEditors.Length); i++)
					{
						textLens[i] = hexEditors[i].Text == null ? -1 : hexEditors[i].Text.Length;
					}
					diagHolder[0] = "subView=" + (sub == null ? "<null>" : sub.GetType().Name)
						+ ", hexEditor数=" + hexEditors.Length
						+ ", 文本长度=[" + textLens[0] + "," + textLens[1] + "]";
					window.Close();
					return 0;
				}).GetAwaiter().GetResult();

				string diag = diagHolder[0] ?? "<未执行>";
				object subView = holder[0];
				Assert.True(subView is ForkPlus.UI.Controls.Editor.Hex.HexDiffUserControl,
					"提交历史的二进制 diff 应显示 HexDiff 视图，实际: " + diag);
				Assert.True(textLens[0] > 0 || textLens[1] > 0,
					"编辑器文本为空（空白渲染）。" + diag);
			}
			finally
			{
				try { Directory.Delete(repoRoot, true); } catch { }
			}
		}
	}
}
