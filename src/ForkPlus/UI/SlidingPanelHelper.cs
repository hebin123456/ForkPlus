using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using global::Avalonia.Animation;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI
{
	public static class SlidingPanelHelper
	{
		private static TimeSpan AnimationDuration = TimeSpan.FromSeconds(0.3);

		public static bool ShowPanel(Grid placeholder, TranslateTransform transform, double height)
		{
			if (placeholder == null || transform == null)
			{
				return false;
			}
			if (transform.Y == 0.0 && placeholder.Height == height)
			{
				return false;
			}
			// Migration note：WPF DoubleAnimation(from, to, duration) 三参构造在 Avalonia/WpfCompat shim 中
			// 不存在（CS1729），改用对象初始化器设置 From/To/Duration（Duration 支持 TimeSpan 隐式转换）。
			DoubleAnimation doubleAnimation = new DoubleAnimation
			{
				From = transform.Y,
				To = 0.0,
				Duration = AnimationDuration
			};
			doubleAnimation.EasingFunction = new QuadraticEase
			{
				EasingMode = EasingMode.EaseOut
			};
			global::ForkPlus.UI.WpfCompat.WpfAnimation.BeginAnimation(transform, TranslateTransform.YProperty, doubleAnimation);
			DoubleAnimation doubleAnimation2 = new DoubleAnimation
			{
				From = 0.0,
				To = height,
				Duration = AnimationDuration
			};
			doubleAnimation2.EasingFunction = new QuadraticEase
			{
				EasingMode = EasingMode.EaseOut
			};
			global::ForkPlus.UI.WpfCompat.WpfAnimation.BeginAnimation(placeholder, global::Avalonia.Controls.Control.HeightProperty, doubleAnimation2);
			return true;
		}

		public static void HidePanel(Grid placeholder, TranslateTransform transform, double height)
		{
			if (placeholder == null || transform == null)
			{
				return;
			}
			if (transform.Y != 0.0 - height || placeholder.Height != 0.0)
			{
				// Migration note：同上，DoubleAnimation 三参构造 → 对象初始化器（CS1729）。
				DoubleAnimation doubleAnimation = new DoubleAnimation
				{
					From = 0.0,
					To = 0.0 - height,
					Duration = AnimationDuration
				};
				doubleAnimation.EasingFunction = new QuadraticEase
				{
					EasingMode = EasingMode.EaseOut
				};
				global::ForkPlus.UI.WpfCompat.WpfAnimation.BeginAnimation(transform, TranslateTransform.YProperty, doubleAnimation);
				DoubleAnimation doubleAnimation2 = new DoubleAnimation
				{
					From = height,
					To = 0.0,
					Duration = AnimationDuration
				};
				doubleAnimation2.EasingFunction = new QuadraticEase
				{
					EasingMode = EasingMode.EaseOut
				};
				global::ForkPlus.UI.WpfCompat.WpfAnimation.BeginAnimation(placeholder, global::Avalonia.Controls.Control.HeightProperty, doubleAnimation2);
			}
		}
	}
}
