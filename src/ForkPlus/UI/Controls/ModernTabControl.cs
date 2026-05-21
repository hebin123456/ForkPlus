using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ForkPlus.UI.Controls
{
	public class ModernTabControl : TabControl
	{
		private const string IndicatorBorder = "PART_IndicatorBorder";

		private Border _indicatorBorder;

		private bool _isTabIndicatorInitialized;

		private int _previousTabIndex;

		private double _indicatorWidth;

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();
			_indicatorBorder = GetTemplateChild("PART_IndicatorBorder") as Border;
		}

		protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
		{
			base.OnRenderSizeChanged(sizeInfo);
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
			if (_isTabIndicatorInitialized && _indicatorBorder != null)
			{
				TranslateTransform translateTransform = new TranslateTransform();
				_indicatorBorder.RenderTransform = translateTransform;
				double tabXCoordinate = GetTabXCoordinate(base.SelectedIndex);
				if (withAnimation)
				{
					DoubleAnimation animation = new DoubleAnimation(GetTabXCoordinate(_previousTabIndex), tabXCoordinate, TimeSpan.FromMilliseconds(200.0))
					{
						EasingFunction = new QuadraticEase
						{
							EasingMode = EasingMode.EaseOut
						}
					};
					translateTransform.BeginAnimation(TranslateTransform.XProperty, animation);
				}
				else
				{
					translateTransform.X = tabXCoordinate;
				}
				_previousTabIndex = Math.Max(0, Math.Min(base.SelectedIndex, base.Items.Count - 1));
			}
		}

		private void UpdateTabIndicatorWidth(TabItem nextTabItem, bool withAnimation)
		{
			if (_isTabIndicatorInitialized && _indicatorBorder != null && nextTabItem != null)
			{
				if (withAnimation)
				{
					DoubleAnimation animation = new DoubleAnimation(_indicatorWidth, nextTabItem.ActualWidth, TimeSpan.FromMilliseconds(200.0))
					{
						EasingFunction = new QuadraticEase
						{
							EasingMode = EasingMode.EaseOut
						}
					};
					_indicatorBorder.BeginAnimation(FrameworkElement.WidthProperty, animation);
				}
				else
				{
					_indicatorBorder.Width = nextTabItem.ActualWidth;
				}
				_indicatorWidth = nextTabItem.ActualWidth;
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
					num += tabItem.ActualWidth;
				}
			}
			return num;
		}
	}
}
