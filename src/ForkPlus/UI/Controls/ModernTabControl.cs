using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace ForkPlus.UI.Controls
{
	// 阶段 4.5：WPF FrameworkElement.BeginAnimation + DoubleAnimation/EasingFunction
	// → Avalonia Transitions + DoubleTransition/Easing。
	// WPF TranslateTransform.BeginAnimation(XProperty, animation)
	// → 在 TranslateTransform 上设置 Transitions，然后修改 X 属性触发过渡。
	// WPF Border.BeginAnimation(WidthProperty, animation)
	// → 在 Border 上设置 Transitions，然后修改 Width 触发过渡。
	// WPF TabItem.ActualWidth → Avalonia TabItem.Bounds.Width（控件已布局后有效）。
	// WPF OnRenderSizeChanged(SizeChangedInfo) → Avalonia SizeChanged(Size)。
	public class ModernTabControl : TabControl
	{
		private const string IndicatorBorderName = "PART_IndicatorBorder";

		private Border _indicatorBorder;

		private bool _isTabIndicatorInitialized;

		private int _previousTabIndex;

		private double _indicatorWidth;

		// 阶段 4.5：Avalonia 过渡时长与 WPF 一致（200ms），缓动函数用 QuadraticEaseOut。
		private static readonly TimeSpan AnimationDuration = TimeSpan.FromMilliseconds(200.0);

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();
			_indicatorBorder = GetTemplateChild("PART_IndicatorBorder") as Border;
			if (_indicatorBorder != null)
			{
				// 阶段 4.5：为 Border.Width 配置过渡动画。
				// Transitions 集合在控件初始化时设置一次，后续修改 Width 自动触发过渡。
				_indicatorBorder.Transitions ??= new Transitions
				{
					new DoubleTransition
					{
						Property = Border.WidthProperty,
						Duration = AnimationDuration,
						Easing = new QuadraticEaseOut()
					}
				};
			}
		}

		protected override void OnSizeChanged(SizeChangedEventArgs e)
		{
			base.OnSizeChanged(e);
			if (base.SelectedItem is TabItem nextTabItem && !_isTabIndicatorInitialized)
			{
				_isTabIndicatorInitialized = true;
				UpdateTabIndicatorPosition(withAnimation: false);
				UpdateTabIndicatorWidth(nextTabItem, withAnimation: false);
			}
		}

		protected override void OnSelectionChanged(SelectionChangedEventArgs e)
		{
			base.OnSelectionChanged(e);
			e.Handled = true;
			if (_isTabIndicatorInitialized && e.AddedItems.Count > 0 && e.AddedItems[0] is TabItem nextTabItem)
			{
				UpdateTabIndicatorPosition(withAnimation: true);
				UpdateTabIndicatorWidth(nextTabItem, withAnimation: true);
			}
		}

		private void UpdateTabIndicatorPosition(bool withAnimation)
		{
			if (!_isTabIndicatorInitialized || _indicatorBorder == null)
			{
				return;
			}
			TranslateTransform translateTransform = new TranslateTransform();
			// 阶段 4.5：为 TranslateTransform.X 配置过渡动画。
			if (withAnimation)
			{
				translateTransform.Transitions = new Transitions
				{
					new DoubleTransition
					{
						Property = TranslateTransform.XProperty,
						Duration = AnimationDuration,
						Easing = new QuadraticEaseOut()
					}
				};
			}
			_indicatorBorder.RenderTransform = translateTransform;
			double tabXCoordinate = GetTabXCoordinate(base.SelectedIndex);
			if (withAnimation)
			{
				// 阶段 4.5：先设置到旧位置，再切换到新位置以触发过渡。
				translateTransform.X = GetTabXCoordinate(_previousTabIndex);
				// 在下一帧设置目标位置，确保过渡触发。
				_ = Dispatcher.UIThread.Post(() => translateTransform.X = tabXCoordinate);
			}
			else
			{
				translateTransform.X = tabXCoordinate;
			}
			_previousTabIndex = Math.Max(0, Math.Min(base.SelectedIndex, base.Items.Count - 1));
		}

		private void UpdateTabIndicatorWidth(TabItem nextTabItem, bool withAnimation)
		{
			if (_isTabIndicatorInitialized && _indicatorBorder != null && nextTabItem != null)
			{
				// 阶段 4.5：WPF TabItem.ActualWidth → Avalonia TabItem.Bounds.Width。
				double num = nextTabItem.Bounds.Width;
				if (withAnimation)
				{
					// Transitions 已在 OnApplyTemplate 中配置；直接修改 Width 触发过渡。
					_indicatorBorder.Width = num;
				}
				else
				{
					_indicatorBorder.Width = num;
				}
				_indicatorWidth = num;
			}
		}

		private double GetTabXCoordinate(int tabIndex)
		{
			double num = 0.0;
			if (tabIndex <= 0 || base.Items.Count == 0)
			{
				return num;
			}
			int safeTabIndex = Math.Min(tabIndex, base.Items.Count);
			for (int i = 0; i < safeTabIndex; i++)
			{
				if (base.Items[i] is TabItem tabItem)
				{
					// 阶段 4.5：WPF TabItem.ActualWidth → Avalonia TabItem.Bounds.Width。
					num += tabItem.Bounds.Width;
				}
			}
			return num;
		}
	}
}
