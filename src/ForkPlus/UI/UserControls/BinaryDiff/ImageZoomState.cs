using System;
using Avalonia;

namespace ForkPlus.UI.UserControls.BinaryDiff
{
	/// <summary>
	/// v4.3.1：图片对比视图的共享缩放/平移状态。
	/// 缩放 Zoom 以“贴合视图”（fit）为 100% 基准，1.0 即原始自适应尺寸；
	/// 平移用归一化中心点（CenterX/CenterY ∈ [0,1]，表示视口正中显示的图像坐标）表达，
	/// 因此左右两侧图片尺寸不一致时，只要中心点相同即“中心点对齐”。
	/// 并排（左右各一）、Swipe、洋葱皮三种视图共用同一个实例，滚轮缩放/拖动自然同步。
	/// 该类同时承载布局数学（纯静态方法，便于单测）。
	/// </summary>
	public class ImageZoomState
	{
		public const double MinZoom = 0.05;

		public const double MaxZoom = 64.0;

		/// <summary>每档滚轮的缩放倍率。</summary>
		public const double ZoomStep = 1.15;

		private const double Epsilon = 1e-9;

		private double _zoom = 1.0;

		private double _centerX = 0.5;

		private double _centerY = 0.5;

		/// <summary>状态变化通知（缩放或中心点任一改变时触发）。</summary>
		public event EventHandler Changed;

		public double Zoom => _zoom;

		public double CenterX => _centerX;

		public double CenterY => _centerY;

		/// <summary>是否已缩放（非 100%）。</summary>
		public bool IsZoomed => Math.Abs(_zoom - 1.0) > Epsilon;

		/// <summary>是否完全处于初始状态（100% 且居中）。</summary>
		public bool IsDefault => !IsZoomed && Math.Abs(_centerX - 0.5) < Epsilon && Math.Abs(_centerY - 0.5) < Epsilon;

		/// <summary>缩放百分比（100 表示贴合视图）。</summary>
		public int ZoomPercent => (int)Math.Round(_zoom * 100.0);

		public void Set(double zoom, double centerX, double centerY)
		{
			zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
			centerX = Math.Clamp(centerX, 0.0, 1.0);
			centerY = Math.Clamp(centerY, 0.0, 1.0);
			if (Math.Abs(zoom - _zoom) < Epsilon && Math.Abs(centerX - _centerX) < Epsilon && Math.Abs(centerY - _centerY) < Epsilon)
			{
				return;
			}
			_zoom = zoom;
			_centerX = centerX;
			_centerY = centerY;
			Changed?.Invoke(this, EventArgs.Empty);
		}

		/// <summary>还原为贴合视图的初始状态。</summary>
		public void Reset()
		{
			Set(1.0, 0.5, 0.5);
		}

		/// <summary>图像“贴合视图”的基准缩放：取宽高较小者且不超过 1（不放大）。
		/// 与原 Viewbox(MaxHeight=像素高) 的首屏行为一致。</summary>
		public static double FitScale(Size viewport, Size image)
		{
			if (image.Width <= 0.0 || image.Height <= 0.0 || viewport.Width <= 0.0 || viewport.Height <= 0.0)
			{
				return 1.0;
			}
			double scale = Math.Min(viewport.Width / image.Width, viewport.Height / image.Height);
			return Math.Min(1.0, scale);
		}

		/// <summary>计算图像在视口中的目标矩形（含缩放与归一化中心点平移）。
		/// 某一维度小于视口时该维度居中（避免露出多余空白）。</summary>
		public static Rect ComputeDstRect(Size viewport, Size image, double zoom, double centerX, double centerY)
		{
			double fit = FitScale(viewport, image);
			double width = image.Width * fit * zoom;
			double height = image.Height * fit * zoom;
			double x = width <= viewport.Width
				? (viewport.Width - width) / 2.0
				: viewport.Width / 2.0 - centerX * width;
			double y = height <= viewport.Height
				? (viewport.Height - height) / 2.0
				: viewport.Height / 2.0 - centerY * height;
			return new Rect(x, y, width, height);
		}

		/// <summary>把归一化中心点夹到图像边缘不内缩的位置；某维度未超出视口时强制居中。</summary>
		public static void ClampCenter(Size viewport, Size image, double zoom, ref double centerX, ref double centerY)
		{
			double fit = FitScale(viewport, image);
			double width = image.Width * fit * zoom;
			double height = image.Height * fit * zoom;
			if (width <= viewport.Width + 0.5)
			{
				centerX = 0.5;
			}
			else
			{
				double half = viewport.Width / (2.0 * width);
				centerX = Math.Clamp(centerX, half, 1.0 - half);
			}
			if (height <= viewport.Height + 0.5)
			{
				centerY = 0.5;
			}
			else
			{
				double half = viewport.Height / (2.0 * height);
				centerY = Math.Clamp(centerY, half, 1.0 - half);
			}
		}

		/// <summary>滚轮缩放：保持指针下的图像点不动（以该点为锚点）。
		/// wheelDelta 为 PointerWheelEventArgs.Delta.Y（正值放大）。</summary>
		public void ZoomAt(Size viewport, Size image, Point pointer, double wheelDelta)
		{
			if (image.Width <= 0.0 || image.Height <= 0.0 || viewport.Width <= 0.0 || viewport.Height <= 0.0 || wheelDelta == 0.0)
			{
				return;
			}
			double newZoom = Math.Clamp(_zoom * Math.Pow(ZoomStep, wheelDelta), MinZoom, MaxZoom);
			if (Math.Abs(newZoom - _zoom) < Epsilon)
			{
				return;
			}
			Rect oldRect = ComputeDstRect(viewport, image, _zoom, _centerX, _centerY);
			// 指针下的归一化图像坐标（夹到图像范围内，避免在留白处滚轮把中心点拽飞）。
			double u = oldRect.Width > 0.0 ? Math.Clamp((pointer.X - oldRect.X) / oldRect.Width, 0.0, 1.0) : 0.5;
			double v = oldRect.Height > 0.0 ? Math.Clamp((pointer.Y - oldRect.Y) / oldRect.Height, 0.0, 1.0) : 0.5;
			double fit = FitScale(viewport, image);
			double width = image.Width * fit * newZoom;
			double height = image.Height * fit * newZoom;
			// 让 (u,v) 缩放后仍落在指针位置，反推新的归一化中心点。
			double centerX = width > 0.0 ? (viewport.Width / 2.0 - (pointer.X - u * width)) / width : 0.5;
			double centerY = height > 0.0 ? (viewport.Height / 2.0 - (pointer.Y - v * height)) / height : 0.5;
			ClampCenter(viewport, image, newZoom, ref centerX, ref centerY);
			Set(newZoom, centerX, centerY);
		}

		/// <summary>拖动平移：以拖动起点的中心点为基准，按指针位移换算，避免逐帧累积误差。
		/// zoom 为当前缩放（拖动过程不变）。</summary>
		public void PanTo(Size viewport, Size image, double zoom, double startCenterX, double startCenterY, Point startPointer, Point currentPointer)
		{
			double fit = FitScale(viewport, image);
			double width = image.Width * fit * zoom;
			double height = image.Height * fit * zoom;
			double centerX = startCenterX;
			double centerY = startCenterY;
			if (width > 0.0)
			{
				// 内容右移 dx 等价于中心点归一化坐标减小 dx/显示宽度。
				centerX = startCenterX - (currentPointer.X - startPointer.X) / width;
			}
			if (height > 0.0)
			{
				centerY = startCenterY - (currentPointer.Y - startPointer.Y) / height;
			}
			ClampCenter(viewport, image, zoom, ref centerX, ref centerY);
			Set(zoom, centerX, centerY);
		}
	}
}