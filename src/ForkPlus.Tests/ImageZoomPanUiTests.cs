// v4.3.1（图片对比视图：滚轮缩放 / 拖动平移 / 左右中心点对齐）的 headless UI 测试。
// 覆盖三层：
//   1) 控件层：ZoomPanImageControl / OverlayImageControl 的 IsZoomable 与共享状态注入；
//   2) 并排语义：两个控件共享同一 ImageZoomState 时，视口中心对应同一归一化图像坐标（中心点对齐）；
//   3) 装配层：插件 BinaryDiffView 把同一 ImageZoomState 实例注入四个图片视图，
//      并在缩放后于对比视图中间偏下悬浮显示"还原大小 (N%)"按钮（Hex 模式隐藏，不塞进底部工具条）、
//      点击后还原为初始状态。
// 纯布局数学见 ImageZoomStateTests（不依赖 headless）。
// v5.0.0：ZoomPanImageControl / OverlayImageControl / ImageZoomState / BinaryDiffView
//   均迁移至插件工程（ForkPlus.Plugins.BuiltIn.ImageDiff）。
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Plugins.BuiltIn.ImageDiff;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class ImageZoomPanUiTests
	{
		private static WriteableBitmap NewBitmap(int width, int height)
		{
			return new WriteableBitmap(new PixelSize(width, height), new Vector(96.0, 96.0),
				PixelFormat.Bgra8888, AlphaFormat.Premul);
		}

		private static double NormalizedXAtViewportCenter(Rect rect, double viewportWidth)
		{
			return (viewportWidth / 2.0 - rect.X) / rect.Width;
		}

		[Fact]
		public void ZoomPanImageControl_ZoomableOnlyWhenSourcePresent()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				ZoomPanImageControl control = new ZoomPanImageControl();
				Assert.False(control.IsZoomable); // 无图 → 滚轮应回落为滚动页面
				WriteableBitmap bitmap = NewBitmap(64, 48);
				try
				{
					control.Source = bitmap;
					Assert.True(control.IsZoomable);
				}
				finally
				{
					bitmap.Dispose();
				}
			});
		}

		[Fact]
		public void OverlayImageControl_ZoomableOnlyWhenBothImagesPresent_AndSharesState()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				ImageZoomState state = new ImageZoomState();
				OverlayImageControl overlay = new OverlayImageControl { ZoomState = state };
				Assert.Same(state, overlay.ZoomState);
				Assert.False(overlay.IsZoomable);

				WriteableBitmap oldBitmap = NewBitmap(120, 90);
				WriteableBitmap newBitmap = NewBitmap(120, 90);
				try
				{
					overlay.SetContent(oldBitmap, newBitmap, null);
					Assert.True(overlay.IsZoomable);
				}
				finally
				{
					oldBitmap.Dispose();
					newBitmap.Dispose();
				}
			});
		}

		[Fact]
		public void SideBySideControls_SharedState_StayCenterAligned()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				ImageZoomState state = new ImageZoomState();
				ZoomPanImageControl left = new ZoomPanImageControl { ZoomState = state };
				ZoomPanImageControl right = new ZoomPanImageControl { ZoomState = state };
				WriteableBitmap leftBitmap = NewBitmap(400, 300);
				WriteableBitmap rightBitmap = NewBitmap(600, 600);
				left.Source = leftBitmap;
				right.Source = rightBitmap;

				Grid grid = new Grid();
				grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
				grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
				Grid.SetColumn(left, 0);
				Grid.SetColumn(right, 1);
				grid.Children.Add(left);
				grid.Children.Add(right);
				Window window = new Window { Width = 400.0, Height = 300.0, Content = grid };
				try
				{
					window.Show();
					Dispatcher.UIThread.RunJobs();
					Assert.True(left.Bounds.Width > 0.0 && right.Bounds.Width > 0.0, "并排两栏应完成布局");

					// 从左栏滚轮放大（生产路径：控件把滚轮转成共享状态的 ZoomAt）；6 档后两图都超出视口
					Size leftImage = new Size(leftBitmap.PixelSize.Width, leftBitmap.PixelSize.Height);
					state.ZoomAt(left.Bounds.Size, leftImage,
						new Point(left.Bounds.Width / 2.0, left.Bounds.Height / 2.0), 6.0);
					Dispatcher.UIThread.RunJobs();
					Assert.True(state.IsZoomed);

					// 两栏各自的图像矩形在视口中心处对应同一归一化图像坐标 → 中心点对齐
					Size rightImage = new Size(rightBitmap.PixelSize.Width, rightBitmap.PixelSize.Height);
					Rect leftRect = ImageZoomState.ComputeDstRect(left.Bounds.Size, leftImage, state.Zoom, state.CenterX, state.CenterY);
					Rect rightRect = ImageZoomState.ComputeDstRect(right.Bounds.Size, rightImage, state.Zoom, state.CenterX, state.CenterY);
					double leftCenter = NormalizedXAtViewportCenter(leftRect, left.Bounds.Width);
					double rightCenter = NormalizedXAtViewportCenter(rightRect, right.Bounds.Width);
					Assert.Equal(state.CenterX, leftCenter, 6);
					Assert.Equal(state.CenterX, rightCenter, 6);
					Assert.Equal(leftCenter, rightCenter, 12);
				}
				finally
				{
					window.Close();
					leftBitmap.Dispose();
					rightBitmap.Dispose();
				}
			});
		}

		[Fact]
		public void BinaryDiffView_InjectsSameZoomStateIntoAllImageViewModes()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				BinaryDiffView control = new BinaryDiffView();
				Window window = new Window { Width = 900.0, Height = 500.0, Content = control };
				try
				{
					window.Show();
					Dispatcher.UIThread.RunJobs();
					// 并排左右两栏 + Swipe + 洋葱皮共用同一个缩放/平移状态实例
					ImageZoomState state = control.SrcFileContentUserControl.ZoomState;
					Assert.NotNull(state);
					Assert.Same(state, control.DstFileContentUserControl.ZoomState);
					Assert.Same(state, control.SwipeImageDiffView.ZoomState);
					Assert.Same(state, control.OnionSkinImageDiffView.ZoomState);
					// 初始 100% 无缩放 → 不显示"还原大小"
					Assert.False(control.ResetZoomButton.IsVisible);
				}
				finally
				{
					window.Close();
				}
			});
		}

		[Fact]
		public void ResetZoomButton_AppearsWhenZoomed_AndResetsSharedStateOnClick()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				BinaryDiffView control = new BinaryDiffView();
				Window window = new Window { Width = 900.0, Height = 500.0, Content = control };
				try
				{
					window.Show();
					Dispatcher.UIThread.RunJobs();
					ImageZoomState state = control.SrcFileContentUserControl.ZoomState;
					Assert.False(control.ResetZoomButton.IsVisible);

					// 任一处缩放（此处直接驱动共享状态）→ 按钮出现，文案含当前百分比
					state.Set(2.0, 0.5, 0.5);
					Dispatcher.UIThread.RunJobs();
					Assert.True(control.ResetZoomButton.IsVisible);
					Assert.Contains("200", (string)control.ResetZoomButton.Content);

					// 悬浮在对比视图内、不在底部视图模式工具条里（对齐冲突页合并按钮的口径）：
					// 水平居中，且整体位于底部工具条上方。
					Point? pillTopLeft = control.ResetZoomButton.TranslatePoint(new Point(0.0, 0.0), control);
					Point? toolbarTopLeft = control.ViewModeButtonsContainer.TranslatePoint(new Point(0.0, 0.0), control);
					Assert.NotNull(pillTopLeft);
					Assert.NotNull(toolbarTopLeft);
					Assert.Equal(control.Bounds.Width / 2.0,
						pillTopLeft.Value.X + control.ResetZoomButton.Bounds.Width / 2.0, 0);
					Assert.True(pillTopLeft.Value.Y + control.ResetZoomButton.Bounds.Height < toolbarTopLeft.Value.Y,
						"「还原大小」按钮应悬浮在图片区域内（底部工具条之上）");

					// Hex 视图不参与图片缩放 → 选中 Hex 时隐藏；切回并排（缩放态仍在）重新出现
					control.HexRadioButton.IsChecked = true;
					Dispatcher.UIThread.RunJobs();
					Assert.False(control.ResetZoomButton.IsVisible);
					control.SideBySideRadioButton.IsChecked = true;
					Dispatcher.UIThread.RunJobs();
					Assert.True(control.ResetZoomButton.IsVisible);

					// 点击还原 → 共享状态回初始、按钮隐藏（各视图同步还原）
					UiClick.Click(control.ResetZoomButton);
					Assert.True(state.IsDefault);
					Assert.False(control.ResetZoomButton.IsVisible);
				}
				finally
				{
					window.Close();
				}
			});
		}
	}
}