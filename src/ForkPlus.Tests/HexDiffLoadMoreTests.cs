// 回归测试（2026-09-12，v4.0.12，"FileDiff 二进制 Hex 视图：加载更多点了没反应 + 文案未国际化"）：
// 1) 单元（独立 Window 直挂 HexDiffUserControl）：>16KB 内容触发截断，点击"加载更多"后
//    编辑器文本增长、渲染字节数推进——修复前该路径在真实 FileDiff 场景下点击无任何效果；
// 2) E2E（真实 FileDiffControl 路径 + 真实鼠标点击）：48KB→56KB 大二进制选中后出现
//    "加载更多"按钮，坐标级 MouseDown/Up 点击（完整 hit-test 路由，暴露遮挡/命中失效），
//    断言内容增长、追加段 offset 连续（0x4000 起）、按钮文案更新。
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.Controls.Editor.Hex;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class HexDiffLoadMoreTests
	{
		private static HexDiffContent MakeBigContent(int srcLen, int dstLen)
		{
			byte[] src = Enumerable.Range(0, srcLen).Select(i => (byte)(i % 251)).ToArray();
			byte[] dst = Enumerable.Range(0, dstLen).Select(i => (byte)((i * 7 + 13) % 253)).ToArray();
			return new HexDiffContent(null, new MemoryStream(src), new MemoryStream(dst));
		}

		private static async Task PumpAsync(int ms)
		{
			await Task.Delay(ms);
			Dispatcher.UIThread.RunJobs();
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			await Task.Delay(ms);
			Dispatcher.UIThread.RunJobs();
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
		}

		private static object GetField(object target, string name)
		{
			return target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(target);
		}

		/// <summary>在控件视觉树里找"加载更多"按钮（排除工具栏 CheckBox——Avalonia 中它也派生自 Button）。</summary>
		private static Button FindLoadMoreButton(Visual root)
		{
			foreach (var b in root.GetVisualDescendants().OfType<Button>())
			{
				if (b is Avalonia.Controls.Primitives.ToggleButton) continue;
				if (b.Content is string s && s.Length > 0)
				{
					return b;
				}
			}
			return null;
		}

		[Fact]
		public async Task HexDiff_LoadMore_Click_AppendsContent()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string failure = null;
			string detail = "";
			await Dispatcher.UIThread.InvokeAsync(async delegate
			{
				var hex = new HexDiffUserControl();
				var window = new Window { Width = 1000, Height = 600, Content = hex };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				// 40KB src / 48KB dst：均 > 16KB 首屏截断阈值
				hex.SetContent(MakeBigContent(40 * 1024, 48 * 1024));
				await PumpAsync(600);

				var editors = hex.GetVisualDescendants().OfType<HexEditor>().ToArray();
				if (editors.Length != 2)
				{
					failure = "应有 2 个 HexEditor，实际 " + editors.Length;
					return;
				}
				var srcEditor = editors[0];
				var dstEditor = editors[1];
				int srcLenBefore = srcEditor.Text.Length;
				int dstLenBefore = dstEditor.Text.Length;

				// 内部状态诊断
				var srcFull = GetField(hex, "_srcFull") as Array;
				int srcRendered = (int)(GetField(hex, "_srcRenderedLen") ?? -1);
				detail = $"state: srcFull={srcFull?.Length ?? -1} srcRendered={srcRendered}";

				Button button = FindLoadMoreButton(hex);
				if (button == null)
				{
					failure = "未找到加载更多按钮（截断后应可见）| " + detail;
					return;
				}
				string contentBefore = button.Content as string;
				detail += $"; buttonVisible={button.IsVisible}, buttonBefore=\"{contentBefore}\"";

				// 模拟点击
				button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
				await PumpAsync(1000);

				int srcLenAfter = srcEditor.Text.Length;
				int dstLenAfter = dstEditor.Text.Length;
				string contentAfter = button.Content as string;
				int srcRenderedAfter = (int)(GetField(hex, "_srcRenderedLen") ?? -1);
				detail += $"; after: srcText {srcLenBefore}->{srcLenAfter}, dstText {dstLenBefore}->{dstLenAfter}, srcRendered={srcRendered}->{srcRenderedAfter}, buttonAfter=\"{contentAfter}\"";
				if (srcLenAfter <= srcLenBefore && dstLenAfter <= dstLenBefore)
				{
					failure = "点击加载更多后两侧内容均未增长";
					return;
				}
				// v4.0.12：追加段 offset 列应接续前段（16384 = 0x4000），不得从 00000000 重新计数
				string srcTextAfter = srcEditor.Text ?? "";
				bool has4000 = srcTextAfter.Split('\n').Any(l => l.StartsWith("00004000  ", StringComparison.Ordinal));
				if (!has4000)
				{
					failure = "追加段 offset 未从 00004000 连续编号";
					return;
				}
				// v4.0.12：点击后视口应滚到新段（即时视觉反馈），不再是点击后界面纹丝不动
				double scrollY = srcEditor.TextArea.TextView.ScrollOffset.Y;
				detail += $", scrollY={scrollY:F1}";
				if (scrollY <= 0.0)
				{
					failure = "追加后视口未滚动到新段（点击后无视觉反馈）";
					return;
				}
				window.Close();
			});
			Assert.True(failure == null, failure + " | " + detail);
		}

		/// <summary>v4.0.12 国际化回归：加载更多按钮文案走语言文件
		/// （此前 "Load more" 键在 8 个语言文件中全部缺失 + "剩余" 硬编码中文）。</summary>
		[Fact]
		public void HexDiff_LoadMoreButton_Text_IsLocalized()
		{
			string original = ForkPlusSettings.Default.UiLanguage;
			try
			{
				ForkPlusSettings.Default.UiLanguage = "zh-Hans";
				string zh = PreferencesLocalization.Current("Load more (+{0} / {1} remaining)");
				Assert.Contains("加载更多", zh);

				ForkPlusSettings.Default.UiLanguage = "en";
				string en = PreferencesLocalization.Current("Load more (+{0} / {1} remaining)");
				Assert.Equal("Load more (+{0} / {1} remaining)", en);

				ForkPlusSettings.Default.UiLanguage = "zh-Hans";
				Assert.Contains("部分显示", PreferencesLocalization.Current("Partially shown"));
			}
			finally
			{
				ForkPlusSettings.Default.UiLanguage = original;
				ForkPlusSettings.Default.Save();
			}
		}

		[Fact]
		public void HexDiff_LoadMore_E2E_RealFileDiffControl_RealMouseClick()
		{
			string repo = TestRepoFactory.CreateLargeBinary();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out var window);
					try
					{
						repoControl.ActivateCommitView();
						Dispatcher.UIThread.RunJobs();
						CommitUserControl commit = repoControl.Content.CommitUserControl;
						StageFileUserControl stage = commit.StageFileUserControl;
						Assert.True(UiClick.WaitFor(delegate
						{
							return stage.AllUnstagedFiles.Any(f => f.Path == "data.bin");
						}), "工作区状态未装配（未找到 data.bin）");
						stage.UnstagedFilesFileListUserControl.SelectFile("data.bin");
						Dispatcher.UIThread.RunJobs();

						// 非图片大二进制 → FileDiffControl 直发 HexDiffUserControl
						HexDiffUserControl hexDiff = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							hexDiff = UiClick.FindAll<HexDiffUserControl>(window).FirstOrDefault();
							return hexDiff != null;
						}), "大二进制应直发 HexDiffUserControl");

						// 首屏 16KB 异步加载完成后，"加载更多"按钮出现
						Button loadMore = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							loadMore = FindLoadMoreButton(hexDiff);
							return loadMore != null && loadMore.IsVisible && loadMore.Bounds.Width > 10 && loadMore.Bounds.Height > 10;
						}), "截断后应出现可见的加载更多按钮");

						HexEditor[] editors = UiClick.FindAll<HexEditor>(window).ToArray();
						Assert.True(editors.Length >= 2, "Hex 视图应有双 HexEditor");
						string srcTextBefore = editors[0].Text ?? "";
						string dstTextBefore = editors[1].Text ?? "";
						Assert.True(srcTextBefore.Length > 0 && dstTextBefore.Length > 0,
							"首屏 16KB 应已装配文本");

						// 真实鼠标点击（坐标级 hit-test：按钮中心 MouseDown → MouseUp）
						string contentBeforeAtClick = loadMore.Content as string;
						Point? center = loadMore.TranslatePoint(
							new Point(loadMore.Bounds.Width / 2.0, loadMore.Bounds.Height / 2.0), window);
						Assert.True(center.HasValue, "加载更多按钮未布局（无法换算窗口坐标）");
						HeadlessWindowExtensions.MouseDown(window, center.GetValueOrDefault(), MouseButton.Left, RawInputModifiers.None);
						HeadlessWindowExtensions.MouseUp(window, center.GetValueOrDefault(), MouseButton.Left, RawInputModifiers.None);

						// 点击后内容应增长
						string failure = null;
						string detail = "";
						Assert.True(UiClick.WaitFor(delegate
						{
							string srcNow = editors[0].Text ?? "";
							string dstNow = editors[1].Text ?? "";
							detail = "srcText " + srcTextBefore.Length + "->" + srcNow.Length
								+ ", dstText " + dstTextBefore.Length + "->" + dstNow.Length;
							return srcNow.Length > srcTextBefore.Length || dstNow.Length > dstTextBefore.Length;
						}), "真实点击加载更多后内容应增长（" + detail + "）");

						// 追加段 offset 列应连续：src 第二段从 0x4000（16384）开始，
						// 而不是从 00000000 重新计数（v4.0.12 修复项）
						string srcTextAfter = editors[0].Text ?? "";
						int lineAt4000 = srcTextAfter.Split('\n')
							.Count(line => line.StartsWith("00004000  ", StringComparison.Ordinal));
						Assert.True(lineAt4000 == 1,
							"追加段 offset 应从 00004000 连续编号（实际含 00004000 行数 " + lineAt4000 + "）");

						// v4.0.12：点击后视口应滚到新段（即时视觉反馈，修复"点了没用"的感知根源）
						Assert.True(editors[0].TextArea.TextView.ScrollOffset.Y > 0.0,
							"追加后 src 视口应滚动到新段首行（实际 ScrollOffset.Y="
							+ editors[0].TextArea.TextView.ScrollOffset.Y.ToString("F1") + "）");

						// 按钮文案应更新（剩余字节减少）或全部加载完隐藏
						Assert.True(UiClick.WaitFor(delegate
						{
							string now = loadMore.Content as string;
							bool refreshed = now != null && now != contentBeforeAtClick;
							return refreshed || !loadMore.IsVisible;
						}), "点击后按钮文案应刷新（剩余量变化）");
						ScreenshotHelper.Snap(window, "09-bigbinary-loadmore-clicked", "09-binarydiff");
						Assert.True(failure == null, failure);
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}
	}
}
