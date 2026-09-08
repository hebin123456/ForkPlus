// 决定性诊断（2026-09-04，"二进制对比显示一片空白"）：
// 前三个测试（纯 HexDiffUserControl / 普通 Window 里的 FileDiffControl / 大文件路径）
// 全部通过，与真实应用的唯一剩余差异是窗口模板——真实主窗口模板里内容被
// LayoutTransformControl 包裹（窗口缩放特性）。本测试把 FileDiffControl 放进
// CustomWindow（默认主题即含 LayoutTransformControl + VisualLayerManager），
// 像素级验证 HexDiff 是否渲染。
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
using ForkPlus.UI;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class BinaryDiffCustomWindowEndToEndTests
	{
		private static string CreateBinaryTestRepo()
		{
			string root = Path.Combine(Path.GetTempPath(), "fpbindiff3_" + Guid.NewGuid().ToString("N").Substring(0, 8));
			Directory.CreateDirectory(root);
			string oldDir = Directory.GetCurrentDirectory();
			try
			{
				Directory.SetCurrentDirectory(root);
				Run("git", "init -q");
				Run("git", "config user.email test@example.com");
				Run("git", "config user.name Test");
				byte[] v1 = new byte[512];
				new Random(42).NextBytes(v1);
				File.WriteAllBytes(Path.Combine(root, "data.bin"), v1);
				Run("git", "add data.bin");
				Run("git", "commit -q -m base");
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
		public void BinaryDiff_InCustomWindowTemplate_RendersNonBlankPixels()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string repoRoot = CreateBinaryTestRepo();
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

				int[] result = new int[8];
				Dispatcher.UIThread.InvokeAsync(delegate
				{
					var repoControl = new RepositoryUserControl();
					typeof(RepositoryUserControl).GetProperty("GitModule")!
						.SetValue(repoControl, module);

					var control = new FileDiffControl();
					control.RepositoryUserControl = repoControl;

					// 真实窗口模板：CustomWindow 默认主题含 LayoutTransformControl（窗口缩放）+ VisualLayerManager
					var window = new CustomWindow { Width = 900, Height = 500, Content = control };
					window.Show();
					Dispatcher.UIThread.RunJobs();

					control.Content = diffResult;
					Task.Delay(500).GetAwaiter().GetResult();
					Dispatcher.UIThread.RunJobs();
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
					Task.Delay(200).GetAwaiter().GetResult();
					Dispatcher.UIThread.RunJobs();

					object sub = control.CurrentSubView;
					result[0] = sub is ForkPlus.UI.Controls.Editor.Hex.HexDiffUserControl ? 1 : 0;
					result[1] = sub == null ? -1 : sub.GetType().Name.Length; // 占位：非 null

					// 模板里确实存在 LayoutTransformControl（验证测试环境与真实主窗口结构一致）
					var ltc = window.GetVisualDescendants().OfType<Avalonia.Controls.LayoutTransformControl>().ToArray();
					result[2] = ltc.Length;

					// 像素级验证
					var frame = Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);
					int nonBlank = 0;
					using (var l = frame.Lock())
					{
						for (int row = 0; row < frame.PixelSize.Height; row++)
						{
							IntPtr rowPtr = l.Address + row * l.RowBytes;
							for (int x = 0; x < frame.PixelSize.Width; x++)
							{
								byte b = System.Runtime.InteropServices.Marshal.ReadByte(rowPtr, x * 4);
								byte g = System.Runtime.InteropServices.Marshal.ReadByte(rowPtr, x * 4 + 1);
								byte r = System.Runtime.InteropServices.Marshal.ReadByte(rowPtr, x * 4 + 2);
								if (r < 230 || g < 230 || b < 230)
								{
									nonBlank++;
								}
							}
						}
					}
					result[3] = nonBlank;
					// 子视图 bounds（布局塌缩检测）
					var subVisual = sub as Avalonia.Visual;
					result[4] = (int)(subVisual?.Bounds.Width ?? -1);
					result[5] = (int)(subVisual?.Bounds.Height ?? -1);
					result[6] = frame.PixelSize.Width;
					result[7] = frame.PixelSize.Height;
					window.Close();
					return 0;
				}).GetAwaiter().GetResult();

				Assert.True(result[0] == 1, "二进制 diff 应显示 HexDiff 视图（CustomWindow 模板下）");
				Assert.True(result[2] >= 1, "模板里应存在 LayoutTransformControl，实际 " + result[2]);
				Assert.True(result[3] > 500,
					"CustomWindow 模板下渲染近乎全空白（非白像素=" + result[3]
					+ "，子视图 bounds=" + result[4] + "x" + result[5]
					+ "，帧=" + result[6] + "x" + result[7] + "）");
			}
			finally
			{
				try { Directory.Delete(repoRoot, true); } catch { }
			}
		}
	}
}
