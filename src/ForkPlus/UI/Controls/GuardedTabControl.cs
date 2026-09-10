using System;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using ForkPlus;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// 带模板重建竞态防护的 TabControl（2026-09-10，"切主题偶发卡死：
	/// Grid already has a visual parent ContentPresenter (PART_SelectedContentHost)"）。
	///
	/// 根因与机制详见 <see cref="TabControlContentHostGuard"/> 类注释。简言之：切主题 =
	/// ControlTheme 换新实例 → 模板重建，旧 PART_SelectedContentHost 的 Host 已被清 null，
	/// Avalonia TabControl 内部 ClearOwningContentPresenter（依赖 Host==this）失效，
	/// 旧 presenter 仍把选中内容（如 Sidebar 各 Tab 的 Grid）持为视觉子级，新 presenter
	/// 测量时 VisualChildren.Add(content) 抛 "already has a visual parent"。
	///
	/// ModernTabControl / ClosableTabControl 已覆写 RegisterContentPresenter 接入兜底释放；
	/// 但 SidebarUserControl 用的是裸 Avalonia <c>TabControl</c>，无覆写 → 切主题必现崩溃
	///（异常虽被 DispatcherUnhandledException 吞掉 Handled=true，但抛在 Measure 阶段导致
	/// 布局 pass 反复重试，UI 表现为卡死）。本类仅给裸 TabControl 加上同样的兜底释放覆写，
	/// 不引入任何视觉/行为变化（无指示条、无动画，与原生 TabControl 外观完全一致）。
	///
	/// 用法：把 XAML 里 <c>&lt;TabControl&gt;</c> 换成 <c>&lt;controls:GuardedTabControl&gt;</c>。
	/// </summary>
	public class GuardedTabControl : TabControl
	{
		// 用基类 TabControl 的 StyleKey，隐式 ControlTheme 解析直接命中 {x:Type TabControl}
		//（与原裸 TabControl 完全相同的主题/模板），无需为本类新增 ControlTheme，外观零变化。
		protected override Type StyleKeyOverride => typeof(TabControl);

		private ContentPresenter _trackedContentHost;

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
	}
}
