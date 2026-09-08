using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace ForkPlus.UI
{
	/// <summary>换肤字典替换前的孤儿 ItemsPresenter 释放（2026-09-07，"切换主题就有可能导致 UI 崩溃，
	/// 参见外观下拉的按钮和菜单栏窗口里面的按钮"）。
	///
	/// 根因一（items 二次托管）：皮肤字典换装 = 每个 ControlTheme/ControlTemplate 都换新实例
	///（含隐式 key 的 <c>{x:Type MenuItem}</c> 与 <c>Template={DynamicResource ...}</c> 解析结果）→
	/// 所有 TemplatedControl 模板重建。Avalonia 模板撕毁（<c>TemplatedControl.ApplyTemplate</c>）靠
	/// <c>GetTemplateDescendants()</c>——视觉树遍历、遇 TemplatedParent==null 停止——给旧模板
	/// 后代置空 TemplatedParent，进而触发 <c>ItemsPresenter.ResetState</c> →
	/// <c>PanelContainerGenerator.Dispose</c> → 旧 Panel 清空、容器释放。但菜单栏二级菜单等内容
	/// 托管在 PopupHost（PopupRoot / PopupOverlayLayer）里——<c>Popup</c> 控件自身没有视觉子级，
	/// 视觉遍历够不到 <c>Popup.Child</c> 子树 → 旧 ItemsPresenter 永远收不到置空 → 旧生成器
	/// 不 Dispose、旧 Panel 一直把菜单项持为视觉子级。新模板的新 ItemsPresenter 建新
	/// PanelContainerGenerator 插入同一批 MenuItem 时抛
	/// "The control MenuItem already has a visual parent StackPanel"。
	///（实证：MainWindow 菜单栏 Window 菜单开二级菜单后切主题必现；下拉菜单项在
	/// ToolbarUserControl.ApplicationThemeChanged 里被整体重建故幸免。上游同类缺陷只修了
	/// Viewbox 场景——Avalonia PR #21879；Popup 场景 12.1.1 无修复。）
	///
	/// 根因二（overlay 枚举中修改）：点击主题项时 Command 先于 MenuBase 关菜单执行（冒泡顺序），
	/// 菜单关闭本身又是异步排队——换肤后的首个布局遍里 popup 内容还在 PopupOverlayLayer 上，
	/// 遍中撕毁旧模板触发 popup detach → Close → overlay.Children.Remove 恰发生在
	/// <c>PopupOverlayLayer.MeasureOverride</c> 枚举自身 Children 期间 →
	/// "Collection was modified"（2026-09-07 实证）。换肤前同步关闭子菜单即可让 overlay
	/// 在布局遍前稳定，且与用户点击主题项的正常行为一致（MenuBase 本来就要关菜单）。
	///
	/// 兜底：换肤字典替换前，先同步关闭所有打开的菜单子菜单，再沿所有窗口收集
	/// "TemplatedParent 不在视觉祖先链上"的 ItemsPresenter（= popup 托管的孤儿），反射调用
	/// internal TemplatedParent setter 置 null，一步复刻正常模板撕毁：旧生成器同步 Dispose、
	/// 释放被旧 Panel 持有的菜单项容器 + ItemsControl 置 null（旧 presenter 不再于后续
	/// 布局遍复活抢 items）。
	///
	/// 遍历用视觉树 + Popup.Child 递归展开，而非逻辑树：Avalonia 12 的
	/// <c>TopLevel.LogicalChildren</c> 被重写为 [TopLevelHost, Content]——模板根 Border 不在
	/// 其中（2026-09-07 实证：Window 逻辑子级只有 2 项，沿它遍历到不了 PART_MainMenu 及
	/// 其子菜单 popup）。视觉树覆盖模板内容与 PopupOverlayLayer 托管的 popup；PopupRoot 托管
	/// 与已关闭的 popup（Child 视觉链无根但完整）经每个 Popup.Child 递归进入——连"开过又
	/// 关掉"的 popup 内容也能收进来，这正是"菜单开过一次、换肤后再开就崩"的间歇路径。
	/// </summary>
	internal static class PopupItemsPresenterRelease
	{
		/// <summary>释放所有窗口中 popup 托管的孤儿 ItemsPresenter。必须在换肤字典替换
		///（MergedDictionaries Add/Remove）之前同步调用——模板重建发生在其后的首个布局遍。</summary>
		public static void ReleaseOrphaned()
		{
			if (Application.Current?.ApplicationLifetime is ClassicDesktopStyleApplicationLifetime lifetime)
			{
				foreach (Window window in lifetime.Windows.ToArray())
				{
					ReleaseOrphaned(window);
				}
			}
		}

		private static void ReleaseOrphaned(Visual root)
		{
			// 根因二兜底：先同步关闭所有打开的菜单子菜单（见类注释根因二）。
			// 与用户点击主题项的正常行为一致——MenuBase 冒泡关闭本来就要做，这里只是把它
			// 提前到换肤字典替换之前、且改为同步执行，让 PopupOverlayLayer 在首个布局遍前稳定。
			foreach (MenuItem menuItem in root.GetSelfAndVisualDescendants().OfType<MenuItem>())
			{
				if (menuItem.IsSubMenuOpen)
				{
					menuItem.IsSubMenuOpen = false;
				}
			}

			// 先收集后释放：ResetState 会改视觉结构，不能边遍历边动。
			List<ItemsPresenter> orphans = new List<ItemsPresenter>();
			CollectOrphanedPresenters(root, orphans, new HashSet<Visual>());
			foreach (ItemsPresenter presenter in orphans)
			{
				ForceResetState(presenter);
			}
		}

		/// <summary>视觉树遍历收集孤儿 presenter；每个 Popup 控件再递归进入其 Child 子树
		///（PopupRoot 托管/已关闭的 popup 内容不在主视觉树里，但 Child 的视觉链自身完整）。
		/// 嵌套 popup（二级菜单）在父 popup 的 Child 子树里同样被扫到。</summary>
		private static void CollectOrphanedPresenters(Visual root, List<ItemsPresenter> orphans, HashSet<Visual> scanned)
		{
			foreach (Visual visual in root.GetSelfAndVisualDescendants())
			{
				if (!scanned.Add(visual))
				{
					continue;
				}
				if (visual is ItemsPresenter presenter && IsPopupHosted(presenter))
				{
					orphans.Add(presenter);
				}
				if (visual is Popup popup && popup.Child is Visual child)
				{
					CollectOrphanedPresenters(child, orphans, scanned);
				}
			}
		}

		/// <summary>TemplatedParent 不在视觉祖先链上 = 内容被 PopupHost 托管（PopupRoot/
		/// PopupOverlayLayer 是独立视觉根，或 popup 已关、内容无视觉根）。菜单/下拉的二级内容
		/// 即此形态。对照：菜单栏 Menu 自身、ContextMenu 自身的 ItemsPresenter 的 TemplatedParent
		/// 都在视觉祖先链上，模板撕毁可达、无需本兜底。</summary>
		private static bool IsPopupHosted(ItemsPresenter presenter)
		{
			if (presenter.TemplatedParent is not Visual owner)
			{
				return false;
			}
			for (Visual ancestor = presenter.GetVisualParent(); ancestor != null; ancestor = ancestor.GetVisualParent())
			{
				if (ancestor == owner)
				{
					return false;
				}
			}
			return true;
		}

		/// <summary>公开 API 里没有等价入口——<c>TemplatedParent</c> 是只有 getter 委托的
		/// DirectProperty（SetValue 不可用）、setter 是 internal。反射调用 internal setter 置 null，
		/// 一步复刻正常模板撕毁（<c>TemplatedControl.ApplyTemplate</c> 的
		/// <c>child.TemplatedParent = null</c>）：<c>ItemsPresenter.OnPropertyChanged</c> 里
		/// ResetState（Dispose 旧 generator、清空旧 Panel 释放容器）+ ItemsControl=null。
		/// ItemsControl=null 是关键——之前用 ItemsPanel 赋值触发 ResetState 的写法保留了
		/// ItemsControl，旧 presenter 在 popup 视觉链里于下一布局遍 ApplyTemplate
		///（Panel is null && ItemsControl is not null）复活、重新抢占同一批 items，
		/// 新模板的 presenter 再插入时照样抛 already has a visual parent。</summary>
		private static void ForceResetState(ItemsPresenter presenter)
		{
			try
			{
				TemplatedParentSetter.Invoke(presenter, new object?[] { null });
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to release popup-hosted ItemsPresenter before theme dictionary swap: " + ex.Message, ex);
			}
		}

		private static MethodInfo TemplatedParentSetter { get; } = typeof(StyledElement)
			.GetProperty("TemplatedParent")!
			.GetSetMethod(nonPublic: true)!;
	}
}
