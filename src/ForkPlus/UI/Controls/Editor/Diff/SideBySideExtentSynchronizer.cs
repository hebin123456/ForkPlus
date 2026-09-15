using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;

namespace ForkPlus.UI.Controls.Editor.Diff
{
	/// <summary>
	/// SideBySide 左右编辑器水平滚动范围（extent 宽度）共享同步器。
	/// 根因（2026-09-14，反编译 AvaloniaEdit 12.0.0 TextView / Avalonia 12.1.1
	/// ScrollContentPresenter 实证）：
	///   1) TextView.MeasureOverride 里 _scrollExtent.Width = 可见行最大宽度 + 3
	///      （CreateAndMeasureVisualLines 只构建视口内的行），左右两侧水平范围天然
	///      不等（一侧有长行另一侧没有时窄侧 max≈0，像素同步被钳制后两栏列错位），
	///      且随垂直滚动实时塌缩（长行进出视口）；
	///   2) SetScrollData 检测到 extent/viewport/offset 变化即 RaiseScrollInvalidated，
	///      presenter 的处理器 UpdateFromScrollable 会 Extent=scrollable.Extent 并
	///      CoerceValue(OffsetProperty)——一旦 extent 回落到真实小值，超出的水平偏移
	///      立即被钳回并推回 TextView。单纯在 VisualLinesChanged 里反射改 _scrollExtent
	///      字段没用：下一轮 measure 的 SetScrollData 会把字段重置回真实值并触发该
	///      钳制（运行时测试证实：窄侧水平偏移恒为 0，垂直滚动也会踩丢水平位置）。
	/// 修复（拦截转发）：把 presenter 挂在 TextView（经 TextArea 转发）上的
	/// ScrollInvalidated 订阅换成我们的——
	///   1) 反射摘除 presenter 的处理器；
	///   2) 我们订阅同一事件：每次 raise 先把 _scrollExtent.Width 抬到两侧出现过的
	///      最大宽度（单调不缩，窄侧可滚入空白区，VS Code 同款行为），再反射调用
	///      presenter.UpdateFromScrollable 转发，让 presenter/ScrollViewer/滚动条
	///      只见到补宽后的 extent，CoerceValue(Offset) 永不把水平偏移钳回；
	///   3) 对侧 eager 补宽转发（无需等对侧自身 measure）；
	///   4) SetDiff 换文件时 Reset() 清零共享宽度，下轮 measure 按新内容重建；
	///   5) 2026-09-15 追加：宽视口侧 extent 补偿视口差（左 vbar Hidden / 右侧可见 →
	///      两侧内容区宽差 = 竖滚动条宽），使两侧 ScrollBarMaximum 恒等——否则拖窄侧
	///      thumb 到末端时宽侧被钳，两栏视图永久错位、两 thumb 末端视觉不齐
	///     （详见 RefreshViewportWidths 注释）。
	/// 兼容性：反射成员缺失（包版本变更）→ 同步器退化为无操作，保持原生行为。
	/// </summary>
	internal sealed class SideBySideExtentSynchronizer : IDisposable
	{
		[Null]
		private static readonly FieldInfo ScrollExtentField = typeof(TextView).GetField("_scrollExtent", BindingFlags.NonPublic | BindingFlags.Instance);

		[Null]
		private static readonly MethodInfo PresenterHandlerMethod = typeof(ScrollContentPresenter).GetMethod("ScrollInvalidated", BindingFlags.NonPublic | BindingFlags.Instance);

		[Null]
		private static readonly MethodInfo UpdateFromScrollableMethod = typeof(ScrollContentPresenter).GetMethod("UpdateFromScrollable", BindingFlags.NonPublic | BindingFlags.Instance);

		private sealed class Side
		{
			[Null]
			public TextView TextView;

			[Null]
			public EventHandler BootstrapHandler;

			[Null]
			public ScrollContentPresenter Presenter;

			[Null]
			public ILogicalScrollable ScrollableChild;

			[Null]
			public EventHandler PresenterHandler;

			[Null]
			public EventHandler OurHandler;

			public bool Intercepted;

			// 2026-09-15：该侧内容区宽（presenter.Viewport.Width），用于宽视口侧 extent
			// 补偿（两侧 ScrollBarMaximum 恒等，见 RefreshViewportWidths 注释）。
			public double ViewportWidth;
		}

		private readonly List<Side> _sides = new List<Side>();

		private double _sharedWidth;

		// 2026-09-15：两侧内容区宽的最小值（窄侧），ScrollBarMaximum 补偿基准（见 RefreshViewportWidths 注释）。
		private double _minViewportWidth;

		private bool _updating;

		private bool _disposed;

		public SideBySideExtentSynchronizer(params TextEditor[] editors)
		{
			// 反射成员缺失（包版本变更）→ 整个同步器退化为无操作
			if (ScrollExtentField == null || PresenterHandlerMethod == null || UpdateFromScrollableMethod == null)
			{
				return;
			}
			foreach (TextEditor editor in editors)
			{
				if (editor?.TextArea?.TextView == null)
				{
					continue;
				}
				Side side = new Side
				{
					TextView = editor.TextArea.TextView
				};
				_sides.Add(side);
				// 模板/presenter 就绪前用 VisualLinesChanged（首测时模板必已应用）引导拦截；
				// 拦截成功后由 ScrollInvalidated 驱动，引导订阅自解。
				side.BootstrapHandler = delegate
				{
					EnsureIntercepted(side);
					if (side.Intercepted)
					{
						side.TextView.VisualLinesChanged -= side.BootstrapHandler;
						Handle(side);
					}
				};
				side.TextView.VisualLinesChanged += side.BootstrapHandler;
			}
		}

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;
			foreach (Side side in _sides)
			{
				if (side.Intercepted)
				{
					side.ScrollableChild.ScrollInvalidated -= side.OurHandler;
					// 还原 presenter 的原生订阅：此后 SetScrollData 的 raise 重新由
					// presenter 自行处理（extent 回落真实值，行为恢复原生）。
					side.ScrollableChild.ScrollInvalidated += side.PresenterHandler;
					side.Intercepted = false;
				}
				if (side.BootstrapHandler != null)
				{
					side.TextView.VisualLinesChanged -= side.BootstrapHandler;
				}
			}
			_sides.Clear();
		}

		/// <summary>换文件（SetDiff）时调用：清零共享宽度，按新内容在下轮 measure 重建。</summary>
		public void Reset()
		{
			_sharedWidth = 0.0;
		}

		private void EnsureIntercepted(Side side)
		{
			if (side.Intercepted || _disposed)
			{
				return;
			}
			// TextEditor 模板：ScrollViewer.Content = TextArea，ScrollContentPresenter
			// 位于 ScrollViewer 模板内，是 TextView 的视觉祖先；TextArea 实现
			// ILogicalScrollable 并全部转发给 TextView（含 ScrollInvalidated 事件）。
			ScrollContentPresenter presenter = FindPresenter(side);
			ILogicalScrollable scrollable = presenter?.Child as ILogicalScrollable;
			if (presenter == null || scrollable == null)
			{
				return;
			}
			EventHandler presenterHandler = (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), presenter, PresenterHandlerMethod);
			EventHandler ourHandler = delegate
			{
				Handle(side);
			};
			scrollable.ScrollInvalidated -= presenterHandler;
			scrollable.ScrollInvalidated += ourHandler;
			side.Presenter = presenter;
			side.ScrollableChild = scrollable;
			side.PresenterHandler = presenterHandler;
			side.OurHandler = ourHandler;
			side.Intercepted = true;
		}

		[Null]
		private static ScrollContentPresenter FindPresenter(Side side)
		{
			ScrollContentPresenter presenter = side.TextView.GetVisualAncestors().OfType<ScrollContentPresenter>().FirstOrDefault();
			if (presenter != null)
			{
				return presenter;
			}
			TextEditor editor = side.TextView.GetVisualAncestors().OfType<TextEditor>().FirstOrDefault();
			return editor?.GetVisualDescendants().OfType<ScrollContentPresenter>().FirstOrDefault();
		}

		private void Handle(Side side)
		{
			if (_updating || _disposed)
			{
				return;
			}
			_updating = true;
			try
			{
				double width = GetExtentWidth(side.TextView);
				// 单调扩张：measure 引发的 raise 时字段刚被 SetScrollData 重置为真实值，
				// 此处读到的即自然宽度；offset/viewport-only raise 时字段仍为补宽值，
				// max 不变。不随塌缩回缩，滚动条范围稳定（垂直滚动不引发水平跳动）。
				if (width > _sharedWidth)
				{
					_sharedWidth = width;
				}
				RefreshViewportWidths();
				PatchAndForward(side);
				foreach (Side other in _sides)
				{
					if (other != side)
					{
						PatchAndForward(other);
					}
				}
			}
			finally
			{
				_updating = false;
			}
		}

		// 修复（2026-09-15，"两水平滚动条拖到末端即失步"）：左编辑器竖滚动条 Hidden、
		// 右侧可见 → 两侧内容区（viewport）宽差 = 竖滚动条宽（探针实测 13px）→
		// ScrollBarMaximum.X = Extent − Viewport 两侧不等（左 1172.7 / 右 1185.7）。
		// 拖窄视口侧 thumb 到末端时，同步写宽视口侧的目标偏移被钳到更小 max → 两栏视图
		// 永久错位 13px（列失步）；两 thumb 的 Maximum 不等也让末端位置视觉不齐。修复：
		// 宽视口侧的 extent 额外补上视口差，使两侧 ScrollBarMaximum 恒等（= 共享宽 − 最窄
		// 视口），宽侧可滚入等量空白（与既有"窄侧滚入空白区"对称，VS Code 同款行为），
		// 整个 [0, max] 区间像素同步零钳制。
		private void RefreshViewportWidths()
		{
			double min = double.PositiveInfinity;
			foreach (Side side in _sides)
			{
				// presenter.Viewport 即该侧内容区（含/不含竖滚动条占位）；未布局完成时为 0，
				// 不参与最窄视口计算（避免把补偿误放大）。
				side.ViewportWidth = ((side.Presenter != null) ? side.Presenter.Viewport.Width : 0.0);
				if (side.ViewportWidth > 1.0 && side.ViewportWidth < min)
				{
					min = side.ViewportWidth;
				}
			}
			_minViewportWidth = (double.IsInfinity(min) ? 0.0 : min);
		}

		private void PatchAndForward(Side side)
		{
			if (!side.Intercepted)
			{
				return;
			}
			// 目标 extent = 共享宽 + 该侧视口超出最窄视口的差（宽视口侧补偿，使
			// ScrollBarMaximum 两侧恒等）；窄视口侧（含两侧等宽时）补偿为 0，行为同前。
			double targetWidth = _sharedWidth + Math.Max(0.0, side.ViewportWidth - _minViewportWidth);
			double width = GetExtentWidth(side.TextView);
			if (Math.Abs(width - targetWidth) > 0.5)
			{
				ScrollExtentField.SetValue(side.TextView, ((Size)ScrollExtentField.GetValue(side.TextView)).WithWidth(targetWidth));
			}
			// 转发（等效 presenter 原 ScrollInvalidated 处理器）：把 Extent/Viewport/Offset
			// 同步给 ScrollViewer 与滚动条。extent 已补宽 → CoerceValue(Offset) 永不钳回，
			// TextView.ArrangeOverride 的自身钳制（offset+viewport>extent）同样不触发。
			UpdateFromScrollableMethod.Invoke(side.Presenter, new object[]
			{
				side.ScrollableChild
			});
		}

		private static double GetExtentWidth(TextView textView)
		{
			return ((Size)ScrollExtentField.GetValue(textView)).Width;
		}
	}
}
