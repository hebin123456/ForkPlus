// 端到端诊断（2026-09-04，"二进制对比显示一片空白"续）：
// 超 MaxHexDiffSize 的二进制文件走无字节回退分支 → BinaryDiffUserControl
// （side-by-side 文件扩展名图标+大小视图）。该路径经过 BinaryContentUserControl.SetContent →
// IconTools / FileHelper / FileSizeFormatter，任一抛异常都会让 initialize delegate
// 中断，ShowSubView 换完子视图却没填内容 → 一片空白。
// v3.7.2：MaxHexDiffSize 10MB→50MB（OTF ~13MB 回归），本文件的回退路径改用 51MB；
// 另增 13MB（OTF 实测尺寸）用例验证卡片视图 + "not LFS" 徽章 + Hex 切换。
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Diff;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Controls.Editor.Hex;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.BinaryDiff;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class BinaryDiffLargeFileEndToEndTests
	{
		private static string CreateLargeBinaryTestRepo(int sizeBytes)
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
				byte[] v1 = new byte[sizeBytes];
				new Random(7).NextBytes(v1);
				File.WriteAllBytes(Path.Combine(root, "blob.bin"), v1);
				Run("git", "add blob.bin");
				Run("git", "commit -q -m base");
				byte[] v2 = new byte[sizeBytes];
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
			// >50MB（新 MaxHexDiffSize）：超出阈值 → CanLoadHexDiff=false → 回退 BinaryDiffUserControl
			string repoRoot = CreateLargeBinaryTestRepo(51 * 1024 * 1024);
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
				// 51MB 双侧 blob 读取较慢，轮询至多 ~30s 等 ShowSubView 完成
				for (int i = 0; i < 300 && control.CurrentSubView == null; i++)
				{
					Task.Delay(100).GetAwaiter().GetResult();
					Dispatcher.UIThread.RunJobs();
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
				}

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
			// >50MB → BinaryDiffUserControl（大小+扩展名图标 side-by-side）
			Assert.True(subView is BinaryDiffUserControl,
				"大文件二进制 diff 应显示 BinaryDiffUserControl，实际: " + diag);
			// 描述文本包含文件大小（如 "51 MB"），非空 → 非空白
			Assert.Contains("MB", diag);
			Assert.True(diag.Contains("51"), "应显示文件大小，实际: " + diag);
		}
		finally
		{
			try { Directory.Delete(repoRoot, true); } catch { }
		}
	}

	[Fact]
	public void OtfSizeBinaryFile_Modified_WorkingDirDiff_ShowsCardsWithLfsBadgeAndHexToggle()
	{
		// 修复回归（2026-09-16，"OTF 变更没有 hex 对比 + 缺 not LFS 徽章"）：实测 OTF 两侧
		// 13,173,128 / 12,737,392 字节。原 MaxHexDiffSize=10MB 导致回退卡片视图且无 Hex
		// 入口（工具栏仅图片显示）、徽章文本在迁移时丢失。修复后 13MB 应为：
		// ① 卡片视图（BinaryDiffUserControl，旧/新 + "not LFS" 徽章文本）
		// ② 底部工具栏可见且 Hex 按钮可用（字节已预载），点击可切 hex 对比。
		HeadlessAppBootstrap.EnsureStarted();
		// 徽章文本随 UiLanguage 本地化（v3.7.2 "not LFS" 国际化）：固定 en 使断言确定性
		string origLang = ForkPlusSettings.Default.UiLanguage;
		ForkPlusSettings.Default.UiLanguage = "en";
		string repoRoot = CreateLargeBinaryTestRepo(13 * 1024 * 1024);
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
				// 13MB 双侧 blob 预载 + 卡片视图装配（轮询至多 ~15s）
				BinaryDiffUserControl cards = null;
				for (int i = 0; i < 150; i++)
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

				string diag = "subView=" + (cards == null ? "<null>" : "BinaryDiffUserControl")
					+ ", tracked=" + binFile.Tracked;
				if (cards != null)
				{
					// ① not LFS 徽章文本（两侧 13MB > 500KB 且 tracked）
					// 诊断：按控件名 dump LfsLabel/NotLfsLabel 的 Text/IsVisible，定位
					// 徽章数=0 时究竟是 Theme 的 Text setter 未生效（text=<null>）
					// 还是 RefreshLfsLabel 未 Show（vis=False）。
					global::Avalonia.Controls.TextBlock[] lfsLabels = cards.GetVisualDescendants()
						.OfType<global::Avalonia.Controls.TextBlock>()
						.Where(t => t.Name == "LfsLabel" || t.Name == "NotLfsLabel")
						.ToArray();
					int notLfsCount = lfsLabels.Count(t => t.Text == "not LFS" && t.IsVisible);
					diag += ", notLfs 徽章数=" + notLfsCount
						+ ", 标签明细=[" + string.Join(";", lfsLabels.Select(t =>
							t.Name + ":text=" + (t.Text ?? "<null>") + "/vis=" + t.IsVisible)) + "]";

					// ② 工具栏可见 + Hex 按钮可见，点击切换到 hex 对比
					bool toolbarVisible = cards.ViewModeButtonsContainer.IsVisible;
					bool hexVisible = cards.HexRadioButton.IsVisible;
					diag += ", 工具栏可见=" + toolbarVisible + ", Hex按钮可见=" + hexVisible;
					if (hexVisible)
					{
						cards.HexRadioButton.IsChecked = true;
						Dispatcher.UIThread.RunJobs();
						for (int i = 0; i < 50; i++)
						{
							Task.Delay(100).GetAwaiter().GetResult();
							Dispatcher.UIThread.RunJobs();
							var editors = cards.GetVisualDescendants().OfType<HexEditor>().ToArray();
							if (editors.Length >= 2 && editors.All(e => !string.IsNullOrEmpty(e.Text)))
							{
								break;
							}
						}
						var hexEditors = cards.GetVisualDescendants().OfType<HexEditor>().ToArray();
						diag += ", Hex容器可见=" + cards.HexDiffViewContainer.IsVisible
							+ ", HexEditor 数=" + hexEditors.Length
							+ ", 有文本=" + hexEditors.Count(e => !string.IsNullOrEmpty(e.Text));
					}
				}
				holder[0] = cards;
				diagHolder[0] = diag;
				window.Close();
				return 0;
			}).GetAwaiter().GetResult();

			string diag2 = diagHolder[0] ?? "<未执行>";
			Assert.True(holder[0] is BinaryDiffUserControl,
				"13MB（OTF 尺寸）二进制 diff 应显示卡片视图（旧/新），实际: " + diag2);
			Assert.True(diag2.Contains("notLfs 徽章数=2"),
				"两侧都应显示 'not LFS' 徽章文本（迁移时模板文本丢失的回归），实际: " + diag2);
			Assert.True(diag2.Contains("工具栏可见=True"), "非图片二进制也应显示视图切换工具栏，实际: " + diag2);
			Assert.True(diag2.Contains("Hex按钮可见=True"), "13MB 预载字节后 Hex 按钮应可用，实际: " + diag2);
			Assert.True(diag2.Contains("Hex容器可见=True"), "点击 Hex 后应切换到 hex 对比视图，实际: " + diag2);
			Assert.True(diag2.Contains("HexEditor 数=2") && diag2.Contains("有文本=2"),
				"hex 对比应装配双 HexEditor 字节文本，实际: " + diag2);
		}
		finally
		{
			ForkPlusSettings.Default.UiLanguage = origLang;
			try { Directory.Delete(repoRoot, true); } catch { }
		}
	}
	}
}

