// E2E 模块9（2026-09-05）：二进制 Diff（图片对比 + Hex 视图）。
// 覆盖：Commit 视图图片 diff（FileDiffControl 二进制+IsImagePath 分发 → BinaryDiffUserControl）、
//   四视图切换（Side-by-Side/Swipe/Onion Skin/Hex RadioButton → ImageDiffSelectedItem_Changed）、
//   Swipe 分割线真实拖拽（window 级指针事件 → GridSplitter → 列宽 → SizeChanged → RefreshClipX →
//   OverlayImage.ClipX）、OnionSkin 透明度滑块（Slider.Value → ValueChanged → NewOpacity）、
//   HighlightPixels 像素差异高亮（header 开关真实点击序 → 设置 + NotificationCenter 通知 →
//   OverlayImage.HighlightImageDiff）、图片切 Hex 视图（懒创建 HexDiffUserControl + 双 HexEditor）、
//   非图片二进制默认 Hex 视图（FileDiffControl 直发 HexDiffUserControl 路径）。
// 截图 → docs/evidence/e2e/09-binarydiff/。
// 测试经验（模块7/8 遗产）：改 ForkPlusSettings 后 finally 恢复 + Save() 落盘防污染；
//   ToggleButton 生产点击序 = 先设 IsChecked 再 raise Click（UiClick.Click 只发事件）。
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Controls.Editor.Hex;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.BinaryDiff;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e09BinaryDiffTests
	{
		/// <summary>Commit 视图选中 unstaged 文件并等待二进制子视图（BinaryDiffUserControl）装配。</summary>
		private static BinaryDiffUserControl OpenCommitViewAndWaitBinaryDiff(string repo, string filePath, out MainWindow outWindow)
		{
			RepositoryUserControl repoControl = E2eMainWindowHarness.OpenRepository(repo, out MainWindow window);
			outWindow = window;
			repoControl.ActivateCommitView();
			Dispatcher.UIThread.RunJobs();
			CommitUserControl commit = repoControl.Content.CommitUserControl;
			StageFileUserControl stage = commit.StageFileUserControl;
			Assert.True(UiClick.WaitFor(delegate
			{
				return stage.AllUnstagedFiles.Any(f => f.Path == filePath);
			}), "工作区状态未装配（未找到未暂存文件 " + filePath + "）");
			stage.UnstagedFilesFileListUserControl.SelectFile(filePath);
			Dispatcher.UIThread.RunJobs();
			BinaryDiffUserControl binaryDiff = null;
			Assert.True(UiClick.WaitFor(delegate
			{
				binaryDiff = UiClick.FindAll<BinaryDiffUserControl>(window).FirstOrDefault();
				return binaryDiff != null;
			}), "选中 " + filePath + " 后应出现 BinaryDiffUserControl（二进制/图片分发路径）");
			return binaryDiff;
		}

		[Fact]
		public void ImageDiff_CommitView_ViewSwitch_SwipeDrag_OnionSkinSlider_HighlightPixels_AndHex()
		{
			string repo = TestRepoFactory.CreateImageDiff();
			bool originalHighlight = ForkPlusSettings.Default.ImageDiffHighlightPixels;
			try
			{
				ForkPlusSettings.Default.ImageDiffHighlightPixels = false;
				HeadlessAppBootstrap.Run(delegate
				{
					BinaryDiffUserControl binaryDiff = OpenCommitViewAndWaitBinaryDiff(repo, "img.png", out var window);
					try
					{
						// ===== 1) 双侧图片装配完成：视图切换按钮出现（RefreshViewModes）=====
						Assert.True(UiClick.WaitFor(delegate
						{
							return binaryDiff.ViewModeButtonsContainer.IsVisible;
						}), "old/new 双图装配后 ViewModeButtonsContainer 应可见（工厂 360x240 绿→橙）");
						// UpdateContent 强制 SideBySide 默认视图
						Assert.True(binaryDiff.SideBySideRadioButton.IsChecked.GetValueOrDefault(),
							"初始应为 Side-by-Side 视图（UpdateContent 重置）");
						Assert.True(binaryDiff.SrcFileContentUserControl.IsVisible
							&& binaryDiff.DstFileContentUserControl.IsVisible, "Side-by-Side 应显示 old/new 两个内容控件");
						Assert.False(binaryDiff.SwipeImageDiffView.IsVisible, "初始 Swipe 视图应隐藏");
						// 同尺寸双图 → DiffImageSource（品红差异图）生成 → header 高亮开关启用
						Assert.True(binaryDiff.DiffImageSource != null,
							"同尺寸双图应生成像素差异图（DiffImageSource）");
						FileControlHeaderUserControl header = UiClick.FindAll<FileControlHeaderUserControl>(window).First();
						Assert.True(header.HighlightPixelsToggleButtonEnabled,
							"差异图存在时 header 的 HighlightPixels 开关应启用（DiffImageSourceChanged）");
						ScreenshotHelper.Snap(window, "01-image-side-by-side", "09-binarydiff");

						// ===== 2) Swipe 视图：分割线裁剪（ClipX = 左列占位宽度）=====
						binaryDiff.SwipeRadioButton.IsChecked = true; // IsCheckedChanged → ImageDiffSelectedItem_Changed
						Dispatcher.UIThread.RunJobs();
						Assert.True(binaryDiff.SwipeImageDiffView.IsVisible, "Swipe 单选后 SwipeImageDiffView 应显示");
						OverlayImageControl overlay = binaryDiff.SwipeImageDiffView.OverlayImage;
						Assert.True(UiClick.WaitFor(delegate
						{
							return overlay.ClipX.HasValue;
						}), "Swipe 视图应装配 ClipX（分割线初始位置）");
						double clipXBefore = overlay.ClipX.GetValueOrDefault();
						Assert.True(clipXBefore > 50.0, "初始 ClipX 应约在半宽处（实际 " + clipXBefore.ToString("F0") + "）");
						ScreenshotHelper.Snap(window, "02-image-swipe", "09-binarydiff");

						// ===== 3) Swipe 分割线真实拖拽：window 级指针事件 → GridSplitter → 列宽 → ClipX =====
						GridSplitter splitter = binaryDiff.SwipeImageDiffView.GridSplitter;
						Point? splitterCenter = splitter.TranslatePoint(
							new Point(splitter.Bounds.Width / 2.0, splitter.Bounds.Height / 2.0), window);
						Assert.True(splitterCenter.HasValue, "GridSplitter 未布局（无法换算窗口坐标）");
						// 真实拖拽序：按下 → 移动（左移 90px）→ 抬起
						HeadlessWindowExtensions.MouseDown(window,
							splitterCenter.GetValueOrDefault(), MouseButton.Left, RawInputModifiers.None);
						HeadlessWindowExtensions.MouseMove(window,
							new Point(splitterCenter.GetValueOrDefault().X - 90.0, splitterCenter.GetValueOrDefault().Y),
							RawInputModifiers.None);
						Dispatcher.UIThread.RunJobs();
						HeadlessWindowExtensions.MouseUp(window,
							new Point(splitterCenter.GetValueOrDefault().X - 90.0, splitterCenter.GetValueOrDefault().Y),
							MouseButton.Left, RawInputModifiers.None);
						Assert.True(UiClick.WaitFor(delegate
						{
							return overlay.ClipX.HasValue
								&& overlay.ClipX.GetValueOrDefault() < clipXBefore - 60.0;
						}), "分割线左拖后 ClipX 应显著减小（拖前 " + clipXBefore.ToString("F0")
							+ " 拖后 " + (overlay.ClipX ?? -1.0).ToString("F0") + "）");
						ScreenshotHelper.Snap(window, "03-image-swipe-dragged", "09-binarydiff");

						// ===== 4) Onion Skin 视图：透明度滑块 → NewOpacity =====
						binaryDiff.OnionSkinRadioButton.IsChecked = true;
						Dispatcher.UIThread.RunJobs();
						Assert.True(binaryDiff.OnionSkinImageDiffView.IsVisible, "OnionSkin 单选后应显示");
						OverlayImageControl onionOverlay = binaryDiff.OnionSkinImageDiffView.OverlayImage;
						Assert.True(onionOverlay.ClipX == null, "OnionSkin 无分割线裁剪（ClipX 应为 null）");
						Assert.True(UiClick.WaitFor(delegate
						{
							return onionOverlay.NewOpacity.HasValue;
						}), "滑块初始 Value=1 应已设置 NewOpacity");
						Assert.Equal(1.0, onionOverlay.NewOpacity.GetValueOrDefault(), 2);
						ScreenshotHelper.Snap(window, "04-image-onionskin-opaque", "09-binarydiff");
						// 滑块拖到半透明：Slider.Value → ValueChanged → NewOpacity
						binaryDiff.OnionSkinImageDiffView.Slider.Value = 0.5;
						Dispatcher.UIThread.RunJobs();
						Assert.True(Math.Abs(onionOverlay.NewOpacity.GetValueOrDefault() - 0.5) < 0.01,
							"滑块 0.5 应设置 NewOpacity=0.5（实际 " + onionOverlay.NewOpacity.ToString() + "）");
						ScreenshotHelper.Snap(window, "05-image-onionskin-half", "09-binarydiff");

						// ===== 5) HighlightPixels 像素差异高亮（header 开关生产点击序）=====
						// 模块7 教训：ToggleButton 生产点击先切 IsChecked 再 raise Click（UiClick.Click 只发事件）
						ToggleButton highlightBtn = header.HighlightPixelsToggleButton;
						highlightBtn.IsChecked = true;
						UiClick.Click(highlightBtn);
						Dispatcher.UIThread.RunJobs();
						Assert.True(ForkPlusSettings.Default.ImageDiffHighlightPixels,
							"点击高亮开关应写入设置 ImageDiffHighlightPixels");
						Assert.True(UiClick.WaitFor(delegate
						{
							return onionOverlay.HighlightImageDiff;
						}), "ImageDiffHighlightPixelsChanged 通知应传导到 OverlayImage.HighlightImageDiff");
						Assert.True(binaryDiff.SwipeImageDiffView.OverlayImage.HighlightImageDiff,
							"Swipe 视图 OverlayImage 也应同步高亮状态（构造订阅）");
						ScreenshotHelper.Snap(window, "06-image-highlightpixels", "09-binarydiff");

						// ===== 6) Hex 视图（图片内切换）：懒创建 HexDiffUserControl + 双 HexEditor =====
						binaryDiff.HexRadioButton.IsChecked = true;
						Dispatcher.UIThread.RunJobs();
						Assert.True(UiClick.WaitFor(delegate
						{
							return binaryDiff.HexDiffViewContainer.IsVisible;
						}), "Hex 单选后 HexDiffViewContainer 应显示");
						Assert.True(UiClick.WaitFor(delegate
						{
							return UiClick.FindAll<HexEditor>(window).Count >= 2;
						}), "Hex 视图应懒创建至少 2 个 HexEditor（old/new 字节并排）");
						HexEditor[] hexEditors = UiClick.FindAll<HexEditor>(window).ToArray();
						Assert.True(hexEditors[0].Text != null && hexEditors[0].Text.Length > 0,
							"old 侧 HexEditor 应装配字节文本");
						Assert.True(hexEditors[1].Text != null && hexEditors[1].Text.Length > 0,
							"new 侧 HexEditor 应装配字节文本");
						Assert.True(hexEditors[0].Text != hexEditors[1].Text, "两侧字节应不同（绿→橙）");
						ScreenshotHelper.Snap(window, "07-image-hex-view", "09-binarydiff");
					}
					finally
					{
						E2eMainWindowHarness.CloseRepositoryTab(window, repo);
					}
				});
			}
			finally
			{
				ForkPlusSettings.Default.ImageDiffHighlightPixels = originalHighlight;
				ForkPlusSettings.Default.Save();
				TestRepoFactory.Cleanup(repo);
			}
		}

		[Fact]
		public void BinaryFile_CommitView_NonImageBinary_ShowsHexViewDirectly()
		{
			string repo = TestRepoFactory.CreateBinary();
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

						// 非图片小二进制（256→512 字节 < 阈值）→ FileDiffControl 直发 HexDiffUserControl
						Assert.True(UiClick.WaitFor(delegate
						{
							return UiClick.FindAll<HexDiffUserControl>(window).Count == 1;
						}), "非图片二进制应直发 HexDiffUserControl（CanLoadHexDiff 小文件路径）");
						Assert.True(UiClick.WaitFor(delegate
						{
							return UiClick.FindAll<HexEditor>(window).Count >= 2;
						}), "Hex 视图应有双 HexEditor");
						HexEditor[] hexEditors = UiClick.FindAll<HexEditor>(window).ToArray();
						Assert.True(hexEditors[0].Text != null && hexEditors[1].Text != null
							&& hexEditors[0].Text.Length > 0 && hexEditors[1].Text.Length > 0,
							"两侧 HexEditor 应装配字节文本");
						Assert.True(hexEditors[0].Text != hexEditors[1].Text, "256 字节 vs 512 字节两侧应不同");
						// 无 BinaryDiffUserControl（图片视图控件不应出现）
						Assert.True(UiClick.FindAll<BinaryDiffUserControl>(window).Count == 0,
							"非图片二进制不应出现 BinaryDiffUserControl");
						ScreenshotHelper.Snap(window, "08-binary-hex-view", "09-binarydiff");
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
