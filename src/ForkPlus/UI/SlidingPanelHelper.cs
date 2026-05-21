using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ForkPlus.UI
{
	public static class SlidingPanelHelper
	{
		private static TimeSpan AnimationDuration = TimeSpan.FromSeconds(0.3);

		public static bool ShowPanel(Grid placeholder, TranslateTransform transform, double height)
		{
			if (transform.Y == 0.0 && placeholder.Height == height)
			{
				return false;
			}
			DoubleAnimation doubleAnimation = new DoubleAnimation(transform.Y, 0.0, AnimationDuration);
			doubleAnimation.EasingFunction = new QuadraticEase
			{
				EasingMode = EasingMode.EaseOut
			};
			transform.BeginAnimation(TranslateTransform.YProperty, doubleAnimation);
			DoubleAnimation doubleAnimation2 = new DoubleAnimation(0.0, height, AnimationDuration);
			doubleAnimation2.EasingFunction = new QuadraticEase
			{
				EasingMode = EasingMode.EaseOut
			};
			placeholder.BeginAnimation(FrameworkElement.HeightProperty, doubleAnimation2);
			return true;
		}

		public static void HidePanel(Grid placeholder, TranslateTransform transform, double height)
		{
			if (transform.Y != 0.0 - height || placeholder.Height != 0.0)
			{
				DoubleAnimation doubleAnimation = new DoubleAnimation(0.0, 0.0 - height, AnimationDuration);
				doubleAnimation.EasingFunction = new QuadraticEase
				{
					EasingMode = EasingMode.EaseOut
				};
				transform.BeginAnimation(TranslateTransform.YProperty, doubleAnimation);
				DoubleAnimation doubleAnimation2 = new DoubleAnimation(height, 0.0, AnimationDuration);
				doubleAnimation2.EasingFunction = new QuadraticEase
				{
					EasingMode = EasingMode.EaseOut
				};
				placeholder.BeginAnimation(FrameworkElement.HeightProperty, doubleAnimation2);
			}
		}
	}
}
