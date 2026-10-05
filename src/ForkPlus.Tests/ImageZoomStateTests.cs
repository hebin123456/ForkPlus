// v4.3.1（图片对比视图：滚轮缩放 / 拖动平移 / 左右中心点对齐）核心布局数学的单元测试。
// ImageZoomState 同时承载"共享缩放状态"与"纯静态布局计算"两部分，这里只跑纯逻辑
// （Size/Rect/Point 均为 Avalonia.Base 值类型，无需 headless 平台）：
//   - 贴合基准 FitScale（不放大超过原始像素，等价原 Viewbox 首屏行为）
//   - 目标矩形 ComputeDstRect（居中 / 归一化中心点平移）
//   - 中心点钳制 ClampCenter（未超出视口的维度强制居中，超出时夹到边缘不内缩）
//   - 滚轮缩放 ZoomAt（指针锚点保持不动 + 缩放上下限钳制）
//   - 拖动平移 PanTo（按指针位移换算，起点为基准不累积误差）
//   - 共享状态语义：同一实例下不同尺寸图片在视口中心对应同一归一化图像坐标（中心点对齐）
using System;
using Avalonia;
using ForkPlus.Plugins.BuiltIn.ImageDiff;
using Xunit;

namespace ForkPlus.Tests
{
	public class ImageZoomStateTests
	{
		private const double Tol = 1e-9;

		/// <summary>视口中心处的归一化图像 X 坐标（= (视口中心 - 矩形左) / 矩形宽）。</summary>
		private static double NormalizedXAtViewportCenter(Rect rect, double viewportWidth)
		{
			return (viewportWidth / 2.0 - rect.X) / rect.Width;
		}

		/// <summary>视口中心处的归一化图像 Y 坐标。</summary>
		private static double NormalizedYAtViewportCenter(Rect rect, double viewportHeight)
		{
			return (viewportHeight / 2.0 - rect.Y) / rect.Height;
		}

		// ============================ FitScale ============================

		[Fact]
		public void FitScale_ImageLargerThanViewport_ScalesDownToFit()
		{
			// 400x300 图放进 200x150 视口 → 半尺寸（0.5），恰好铺满
			Assert.Equal(0.5, ImageZoomState.FitScale(new Size(200.0, 150.0), new Size(400.0, 300.0)), 12);
		}

		[Fact]
		public void FitScale_ImageSmallerThanViewport_DoesNotUpscale()
		{
			// 小图不放大（上限 1）——复刻原 Viewbox(MaxHeight=像素高) 的首屏行为
			Assert.Equal(1.0, ImageZoomState.FitScale(new Size(800.0, 600.0), new Size(400.0, 300.0)), 12);
		}

		[Theory]
		[InlineData(0.0, 300.0, 400.0, 300.0)]
		[InlineData(400.0, 0.0, 400.0, 300.0)]
		[InlineData(200.0, 150.0, 0.0, 300.0)]
		[InlineData(200.0, 150.0, 400.0, 0.0)]
		public void FitScale_DegenerateInputs_ReturnsOne(double vw, double vh, double iw, double ih)
		{
			Assert.Equal(1.0, ImageZoomState.FitScale(new Size(vw, vh), new Size(iw, ih)), 12);
		}

		// ============================ ComputeDstRect ============================

		[Fact]
		public void ComputeDstRect_Default_ZoomOne_CentersImage()
		{
			// 200x100 图（fit=1）放进 400x300 视口，默认缩放/居中 → 水平垂直都居中
			Rect rect = ImageZoomState.ComputeDstRect(new Size(400.0, 300.0), new Size(200.0, 100.0), 1.0, 0.5, 0.5);
			Assert.Equal(100.0, rect.X, 9);
			Assert.Equal(100.0, rect.Y, 9);
			Assert.Equal(200.0, rect.Width, 9);
			Assert.Equal(100.0, rect.Height, 9);
		}

		[Fact]
		public void ComputeDstRect_LargerThanViewport_UsesNormalizedCenterPoint()
		{
			// 400x300 图（fit=0.5）缩放 2 倍 → 显示 400x300（超出 200x150 视口）
			// 中心点 (0.7, 0.6) → 左上角 = 视口中心 - 中心点 * 显示尺寸
			Rect rect = ImageZoomState.ComputeDstRect(new Size(200.0, 150.0), new Size(400.0, 300.0), 2.0, 0.7, 0.6);
			Assert.Equal(100.0 - 0.7 * 400.0, rect.X, 9);
			Assert.Equal(75.0 - 0.6 * 300.0, rect.Y, 9);
			Assert.Equal(400.0, rect.Width, 9);
			Assert.Equal(300.0, rect.Height, 9);
		}

		// ============================ ZoomAt ============================

		[Fact]
		public void ZoomAt_ScalesByStep()
		{
			ImageZoomState state = new ImageZoomState();
			// 400x300 图放进 200x150 视口（fit=0.5），在正中滚一档
			state.ZoomAt(new Size(200.0, 150.0), new Size(400.0, 300.0), new Point(100.0, 75.0), 1.0);
			Assert.Equal(ImageZoomState.ZoomStep, state.Zoom, 12);
			Assert.True(state.IsZoomed);
		}

		[Fact]
		public void ZoomAt_KeepsPointUnderPointerAnchored()
		{
			ImageZoomState state = new ImageZoomState();
			Size viewport = new Size(200.0, 150.0);
			Size image = new Size(400.0, 300.0);
			Point pointer = new Point(150.0, 100.0);
			// 缩放前指针下的归一化图像坐标
			Rect before = ImageZoomState.ComputeDstRect(viewport, image, state.Zoom, state.CenterX, state.CenterY);
			double u = (pointer.X - before.X) / before.Width;
			double v = (pointer.Y - before.Y) / before.Height;

			state.ZoomAt(viewport, image, pointer, 1.0);

			// 缩放后指针下仍是同一归一化图像坐标（锚点保持不动）
			Rect after = ImageZoomState.ComputeDstRect(viewport, image, state.Zoom, state.CenterX, state.CenterY);
			Assert.Equal(u, (pointer.X - after.X) / after.Width, 6);
			Assert.Equal(v, (pointer.Y - after.Y) / after.Height, 6);
		}

		[Fact]
		public void ZoomAt_ClampsToMaxAndMinZoom()
		{
			ImageZoomState state = new ImageZoomState();
			Size viewport = new Size(200.0, 150.0);
			Size image = new Size(400.0, 300.0);
			// 极大正增量 → 上限
			state.ZoomAt(viewport, image, new Point(100.0, 75.0), 100.0);
			Assert.Equal(ImageZoomState.MaxZoom, state.Zoom, 12);
			// 极大负增量 → 下限
			state.ZoomAt(viewport, image, new Point(100.0, 75.0), -100.0);
			Assert.Equal(ImageZoomState.MinZoom, state.Zoom, 12);
		}

		// ============================ ClampCenter ============================

		[Fact]
		public void ClampCenter_ForcesCenterWhenImageFitsViewport()
		{
			// 200x100 图放进 400x300 视口（两维都没超出）→ 中心点强制回 (0.5, 0.5)
			double centerX = 0.9;
			double centerY = 0.1;
			ImageZoomState.ClampCenter(new Size(400.0, 300.0), new Size(200.0, 100.0), 1.0, ref centerX, ref centerY);
			Assert.Equal(0.5, centerX, 9);
			Assert.Equal(0.5, centerY, 9);
		}

		[Fact]
		public void ClampCenter_LimitsCenterWhenImageExceedsViewport()
		{
			// 400x300 图（fit=0.5）缩放 2 倍 → 显示 400x300（超出 200x150）
			// 半视口占图像的比例 = 200/(2*400)=0.25 / 150/(2*300)=0.25 → 中心点夹到 [0.25, 0.75]
			double centerX = 0.9;
			double centerY = 0.1;
			ImageZoomState.ClampCenter(new Size(200.0, 150.0), new Size(400.0, 300.0), 2.0, ref centerX, ref centerY);
			Assert.Equal(0.75, centerX, 9);
			Assert.Equal(0.25, centerY, 9);
		}

		// ============================ PanTo ============================

		[Fact]
		public void PanTo_ShiftsCenterOppositeToDragDirection()
		{
			// 拖动起点与当前点相比向左移 40px（内容左移）→ 中心点归一化坐标 +40/显示宽
			ImageZoomState state = new ImageZoomState();
			Size viewport = new Size(200.0, 150.0);
			Size image = new Size(400.0, 300.0);
			state.PanTo(viewport, image, 2.0, 0.5, 0.5, new Point(100.0, 75.0), new Point(60.0, 75.0));
			Assert.Equal(0.6, state.CenterX, 9); // 0.5 - (-40)/400
			Assert.Equal(0.5, state.CenterY, 9); // 纵向未动
		}

		[Fact]
		public void PanTo_ClampsAtImageEdges()
		{
			// 拖出远超边界 → 中心点夹到 0.75（图像右边缘贴视口左边缘，不露白）
			ImageZoomState state = new ImageZoomState();
			Size viewport = new Size(200.0, 150.0);
			Size image = new Size(400.0, 300.0);
			state.PanTo(viewport, image, 2.0, 0.5, 0.5, new Point(100.0, 75.0), new Point(-100.0, 75.0));
			Assert.Equal(0.75, state.CenterX, 9);
		}

		// ============================ Reset / 状态通知 ============================

		[Fact]
		public void Reset_RestoresInitialFitState()
		{
			ImageZoomState state = new ImageZoomState();
			state.Set(2.0, 0.2, 0.8);
			Assert.False(state.IsDefault);
			state.Reset();
			Assert.True(state.IsDefault);
			Assert.Equal(1.0, state.Zoom, 12);
			Assert.Equal(0.5, state.CenterX, 12);
			Assert.Equal(0.5, state.CenterY, 12);
			Assert.False(state.IsZoomed);
		}

		[Fact]
		public void Set_NoRealChange_DoesNotRaiseChanged()
		{
			ImageZoomState state = new ImageZoomState();
			int changes = 0;
			state.Changed += delegate { changes++; };
			state.Set(2.0, 0.3, 0.3);
			Assert.Equal(1, changes);
			// 同值再设 → 无通知
			state.Set(2.0, 0.3, 0.3);
			Assert.Equal(1, changes);
			// 差异小于 epsilon → 无通知
			state.Set(2.0 + 1e-12, 0.3, 0.3);
			Assert.Equal(1, changes);
			// 真实变化 → 通知
			state.Set(2.0, 0.3, 0.4);
			Assert.Equal(2, changes);
		}

		[Fact]
		public void Set_ClampsOutOfRangeValues()
		{
			ImageZoomState state = new ImageZoomState();
			state.Set(1000.0, 2.0, -1.0);
			Assert.Equal(ImageZoomState.MaxZoom, state.Zoom, 12);
			Assert.Equal(1.0, state.CenterX, 12);
			Assert.Equal(0.0, state.CenterY, 12);
		}

		[Fact]
		public void ZoomPercent_ReportsRoundedPercent()
		{
			ImageZoomState state = new ImageZoomState();
			state.Set(1.15, 0.5, 0.5);
			Assert.Equal(115, state.ZoomPercent);
			state.Reset();
			Assert.Equal(100, state.ZoomPercent);
		}

		// ============================ 共享状态：中心点对齐 ============================

		[Fact]
		public void SharedState_DifferentSizedImages_AlignSameNormalizedPointAtViewportCenter()
		{
			// 同一 ImageZoomState 被两张尺寸不同的图共用：只要中心点相同，
			// 视口中心处就对应同一归一化图像坐标 —— 即"左右中心点对齐"。
			ImageZoomState state = new ImageZoomState();
			Size viewport = new Size(200.0, 150.0);
			state.Set(2.31, 0.7, 0.6);
			// A：400x300（fit=0.5），B：600x600（fit=0.25）——显示尺寸不同（462x346.5 / 462x462）
			Rect rectA = ImageZoomState.ComputeDstRect(viewport, new Size(400.0, 300.0), state.Zoom, state.CenterX, state.CenterY);
			Rect rectB = ImageZoomState.ComputeDstRect(viewport, new Size(600.0, 600.0), state.Zoom, state.CenterX, state.CenterY);

			double ax = NormalizedXAtViewportCenter(rectA, viewport.Width);
			double bx = NormalizedXAtViewportCenter(rectB, viewport.Width);
			double ay = NormalizedYAtViewportCenter(rectA, viewport.Height);
			double by = NormalizedYAtViewportCenter(rectB, viewport.Height);

			Assert.Equal(0.7, ax, 6);
			Assert.Equal(0.7, bx, 6);
			Assert.Equal(0.6, ay, 6);
			Assert.Equal(0.6, by, 6);
			Assert.Equal(ax, bx, 12); // 两张图在同一屏幕位置显示同一图像位置
			Assert.Equal(ay, by, 12);
		}
	}
}