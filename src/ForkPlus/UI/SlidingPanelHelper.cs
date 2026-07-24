using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Media;

namespace ForkPlus.UI
{
	public static class SlidingPanelHelper
	{
		// 阶段 4 里程碑 4.7-a：WPF BeginAnimation + DoubleAnimation → Avalonia Transitions。
		// Avalonia 动画模型是声明式的：把 DoubleTransition 加入控件的 Transitions 集合，
		// 之后设置属性值时会自动触发过渡。这里首次 ShowPanel/HidePanel 时安装 transition，
		// 之后直接设置目标值。WPF QuadraticEase EaseOut → Avalonia Easing.ParseType("QuadraticEaseOut")
		// （Avalonia 用 Easing 类型的属性，QuadraticEaseOut 是内置 Easing）。
		private static readonly TimeSpan AnimationDuration = TimeSpan.FromSeconds(0.3);

		private static readonly Easing EaseOut = new QuadraticEaseOut();

		public static bool ShowPanel(Grid placeholder, TranslateTransform transform, double height)
		{
			EnsureTransitions(placeholder, transform);
			if (transform.Y == 0.0 && placeholder.Height == height)
			{
				return false;
			}
			transform.Y = 0.0;
			placeholder.Height = height;
			return true;
		}

		public static void HidePanel(Grid placeholder, TranslateTransform transform, double height)
		{
			EnsureTransitions(placeholder, transform);
			if (transform.Y != 0.0 - height || placeholder.Height != 0.0)
			{
				transform.Y = 0.0 - height;
				placeholder.Height = 0.0;
			}
		}

		/// <summary>
		/// 阶段 4 里程碑 4.7-a：为 Grid.Height 和 TranslateTransform.Y 安装 DoubleTransition。
		/// 首次调用时添加，已存在则跳过。Avalonia 的 Transitions 是一次配置后自动生效的声明式动画。
		/// </summary>
		private static void EnsureTransitions(Grid placeholder, TranslateTransform transform)
		{
			if (!placeholder.Transitions.HasAny)
			{
				placeholder.Transitions = new Transitions
				{
					new DoubleTransition
					{
						Property = Grid.HeightProperty,
						Duration = AnimationDuration,
						Easing = EaseOut
					}
				};
			}
			if (transform.Transitions == null || !transform.Transitions.HasAny)
			{
				transform.Transitions = new Transitions
				{
					new DoubleTransition
					{
						Property = TranslateTransform.YProperty,
						Duration = AnimationDuration,
						Easing = EaseOut
					}
				};
			}
		}
	}
}
