using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using global::Avalonia.Animation;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class ModernTabControl : TabControl
	{
		private const string IndicatorBorder = "PART_IndicatorBorder";

		private Border _indicatorBorder;

		private bool _isTabIndicatorInitialized;

		private int _previousTabIndex;

		private double _indicatorWidth;

		// 模板重建竞态防护（2026-09-07，"切换主题导致 UI 崩溃"）：
		// 跟踪当前 PART_SelectedContentHost，重建时兜底释放旧 presenter 持有的选中内容。
		// 根因与机制详见 TabControlContentHostGuard 类注释。
		private ContentPresenter _trackedContentHost;

		/// <summary>皮肤字典换装 → ControlTheme 换新实例 → 模板重建时，旧
		/// PART_SelectedContentHost 的 Host 已被清 null，Avalonia TabControl 内部的
		/// ClearOwningContentPresenter（依赖 Host==this）失效——旧 presenter 仍把选中
		/// 内容持为视觉子级，新 presenter 测量时抛 "already has a visual parent"。
		/// 这里在新 presenter 注册时显式释放（RepositoryDetailsUserControl 的
		/// Theme="{DynamicResource RepositoryManagerTabControl}" 实例切主题必现）。</summary>
		protected override bool RegisterContentPresenter(ContentPresenter presenter)
		{
			bool handled = base.RegisterContentPresenter(presenter);
			if (handled)
			{
				_trackedContentHost = TabControlContentHostGuard.OnSelectedContentHostRegistered(
					_trackedContentHost, presenter);
			}
			return handled;
		}

		// Migration note：WPF `OnSelectionChanged` 是框架调用的虚方法重写（TabControl 基类回调）。
		// 实证 Avalonia 12 的 SelectingItemsControl/TabControl **没有**该虚方法
		// （写 `protected override` 报 CS0115 no suitable method found to override），
		// 转换工具丢掉 `override` 后成方法隐藏 → 死代码，指示条（PART_IndicatorBorder 下划线）
		// 只在 OnSizeChanged 初始化一次、切 Tab 永不移动（偏好设置切页下划线卡在首个 Tab 下）。
		// 修复：显式订阅 SelectionChanged 路由事件转发回原方法（同 ClosableTabControl 修复链 3 模式）。
		public ModernTabControl()
		{
			base.SelectionChanged += delegate (object sender, SelectionChangedEventArgs e)
			{
				OnSelectionChanged(e);
			};
		}

		protected override void OnApplyTemplate(global::Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			_indicatorBorder = this.GetTemplateChild("PART_IndicatorBorder") as Border;
		}

		protected override void OnSizeChanged(global::Avalonia.Controls.SizeChangedEventArgs sizeInfo)
		{
			base.OnSizeChanged(sizeInfo);
			if (base.SelectedItem is TabItem nextTabItem && !_isTabIndicatorInitialized)
			{
				_isTabIndicatorInitialized = true;
				UpdateTabIndicatorPosition(withAnimation: false);
				UpdateTabIndicatorWidth(nextTabItem, withAnimation: false);
			}
		}

		protected void OnSelectionChanged(SelectionChangedEventArgs e)
		{
			// Migration note：WPF 原版此处 `e.Handled = true` 位于 base.OnSelectionChanged(e)（同步
			// RaiseEvent，全部处理器已跑完）之后，实际不影响该次广播，仅语义残留；Avalonia 的
			// EventRoute 里 Handled=true 会跳过同元素后续订阅者（EventRoute.cs 实证：
			// `!e.Handled || entry.HandledEventsToo`），保留会吞掉 ServiceTabItem 等 XAML
			// 订阅的 SelectionChanged 处理器 → 删除。
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
					global::ForkPlus.UI.WpfCompat.WpfAnimation.BeginAnimation(translateTransform,TranslateTransform.XProperty,animation);
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
					DoubleAnimation animation = new DoubleAnimation(_indicatorWidth, nextTabItem.Bounds.Width, TimeSpan.FromMilliseconds(200.0))
					{
						EasingFunction = new QuadraticEase
						{
							EasingMode = EasingMode.EaseOut
						}
					};
					global::ForkPlus.UI.WpfCompat.WpfAnimation.BeginAnimation(_indicatorBorder,global::Avalonia.Controls.Control.WidthProperty,animation);
				}
				else
				{
					_indicatorBorder.Width = nextTabItem.Bounds.Width;
				}
				_indicatorWidth = nextTabItem.Bounds.Width;
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
					num += tabItem.Bounds.Width;
				}
			}
			return num;
		}
	}
}
