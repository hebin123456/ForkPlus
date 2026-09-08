// 真机 bug 复现（2026-09-04，"自定义颜色界面颜色管理器面板弹出不对 + 性能问题 + 失焦关不掉"）：
// WPF 原版 ColorPickerPopup 为 StaysOpen="False"（点外部/窗口失活即关）+ MouseLeftButtonUp
// 抬起弹出 + Placement="Mouse"；迁移版丢失全部三条语义：
//   1) 无 IsLightDismissEnabled / 无失活关闭 → 弹出后点外部、切应用都关不掉；
//   2) 错挂 PointerPressed（按下即弹）+ Placement="Pointer" → 弹出位置/时机不对；
//   3) 拖动中每个 PointerMove 都全量 ApplyAndRefresh（重载 Generic 主题字典 +
//      RaiseApplicationThemeChanged 全 UI 刷新 + 30 项预览重建 + 同步写盘）→ UI 卡死。
// 修复：IsLightDismissEnabled + 窗口 Deactivated 关闭；抬起弹出 + PlacementTarget 锚定；
// 轻量/重活分离 + DispatcherTimer 150ms 防抖，Popup 关闭/对话框关闭时 flush。
// 本测试四层回归：XAML 结构防回归、失活关闭行为、防抖只跑一次重活、防抖到期自动应用。
//
// headless 时序要点（两个防抖测试共用，勿删本段注释）：
//   a) Avalonia 12 中 TextBox.Text setter 触发的 TextChanged 不是同步的——设置 Text 后
//      事件随下一个布局/渲染 pass（Dispatcher job）才 raise。测试在 func 内设置 Text 后
//      读 _hasPendingApply 恒 false，必须经一次 RunJobs 才会走 ApplyPopupColor。
//   b) headless 下打开 Popup 的布局/渲染 job 常耗时 >150ms（冷启 JIT + Skia 软渲染，
//      实测 RunJobs 317ms），而 RunJobs 的 ExecuteJob 末尾会 PromoteTimers：拖动模拟
//      前启动的 150ms 防抖会在 RunJobs 结束前到期并触发 Tick → ApplyAndRefresh 提前
//      跑掉，"拖动中不跑重活"的断言窗口被吞。两个测试都先把防抖 Interval 反射拉长到
//      3000ms（Phase 1 内绝不到期）；"到期自动应用"测试在拖动结束后把 Interval 改回
//      150ms——DispatcherTimer.Interval setter 在 _isEnabled 时会把 DueTimeInMs 重调度为
//      now+interval（Avalonia 源码语义），等价于"停止拖动后 150ms"，仍验证真实到期路径。
//   c) 防抖 DispatcherTimer 的 Tick 以 Background 优先级（-2）执行；RunJobs 能执行
//      Background job，空闲主循环也能自动驱动到期计时器（实证探针：500ms 内 50ms 间隔
//      计时器 Tick 了 7 次），因此测试线程 Sleep 等待即可，无需额外触发。
using System;
using System.IO;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using ForkPlus.Settings;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class CustomColorsPopupTests
	{
		// ===== 1) XAML 结构防回归 =====

		[Fact]
		public void ColorPickerPopupXaml_LightDismissAndAnchorAndReleaseOpen()
		{
			string root = FindRepositoryRoot();
			string xaml = File.ReadAllText(Path.Combine(root,
				"src", "ForkPlus", "UI", "Dialogs", "CustomColorsDialog.axaml"));

			// 原版 StaysOpen="False" 的 Avalonia 对应物：light dismiss（点外部关闭）。
			Assert.Contains("IsLightDismissEnabled=\"True\"", xaml);
			// 不再跟随指针漂移：锚定 PlacementTarget + Bottom（code-behind 动态设置锚点）。
			Assert.Contains("Placement=\"Bottom\"", xaml);
			Assert.DoesNotContain("Placement=\"Pointer\"", xaml);
			// 原版 MouseLeftButtonUp（抬起弹出）语义：不再按下即弹。
			Assert.Contains("PointerReleased=\"ColorPreview_Click\"", xaml);
			Assert.DoesNotContain("PointerPressed=\"ColorPreview_Click\"", xaml);
		}

		// ===== 2) 失活关闭行为（生产接线：窗口 Deactivated → popup 关）=====

		[Fact]
		public void ColorPickerPopup_WindowDeactivated_ClosesPopup()
		{
			string diag = HeadlessAppBootstrap.Run(delegate
			{
				var dialog = new global::ForkPlus.UI.Dialogs.CustomColorsDialog();
				dialog.Show();
				dialog.UpdateLayout();

				Popup popup = dialog.ColorPickerPopup;
				bool dismissedOnOutsideClick = popup.IsLightDismissEnabled;

				// 生产打开路径（ColorPreview_Click 修复后）：锚定 + 打开。
				popup.PlacementTarget = dialog.ColorListControl;
				popup.IsOpen = true;
				Dispatcher.UIThread.RunJobs();
				bool opened = popup.IsOpen;

				// 窗口失活（切应用/点其他窗口）→ 立即关闭（light dismiss 只覆盖窗口内按压）。
				SimulateDeactivated(dialog);
				Dispatcher.UIThread.RunJobs();
				bool closedAfterDeactivate = !popup.IsOpen;

				dialog.Close();
				return "lightDismiss=" + dismissedOnOutsideClick + " opened=" + opened + " closedAfterDeactivate=" + closedAfterDeactivate;
			});
			Assert.True(diag == "lightDismiss=True opened=True closedAfterDeactivate=True", diag);
		}

		// ===== 3) 防抖：拖动中多次改色只执行一次重活，关闭时 flush 不丢最后一次 =====

		[Fact]
		public void ApplyPopupColor_DebouncesHeavyRefresh_FlushesOnClose()
		{
			string diag = HeadlessAppBootstrap.Run(delegate
			{
				// 备份全局 settings（ApplyAndRefresh 会写 CustomColors + Save）。
				var settings = ForkPlusSettings.Default;
				// 判据用原始引用：savedCustomColors 是"恢复用副本"，拿副本比引用恒 false
				//（settings.json 持久盘上 CustomColors 非空时必踩，曾致本测试误报失败）。
				var originalRef = settings.CustomColors;
				var savedCustomColors = originalRef == null
					? null
					: new System.Collections.Generic.Dictionary<string, string>(originalRef);
				bool savedUseCustomColors = settings.UseCustomColors;
				try
				{
					var dialog = new global::ForkPlus.UI.Dialogs.CustomColorsDialog();
					dialog.Show();
					dialog.UpdateLayout();

					FieldInfo itemsField = typeof(global::ForkPlus.UI.Dialogs.CustomColorsDialog)
						.GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance);
					var items = (System.Collections.IList)itemsField.GetValue(dialog);
					FieldInfo editingField = typeof(global::ForkPlus.UI.Dialogs.CustomColorsDialog)
						.GetField("_popupEditingItem", BindingFlags.NonPublic | BindingFlags.Instance);
					editingField.SetValue(dialog, items[0]);
					FieldInfo pendingField = typeof(global::ForkPlus.UI.Dialogs.CustomColorsDialog)
						.GetField("_hasPendingApply", BindingFlags.NonPublic | BindingFlags.Instance);
					FieldInfo timerField = typeof(global::ForkPlus.UI.Dialogs.CustomColorsDialog)
						.GetField("_applyDebounceTimer", BindingFlags.NonPublic | BindingFlags.Instance);
					MethodInfo applyMethod = typeof(global::ForkPlus.UI.Dialogs.CustomColorsDialog)
						.GetMethod("ApplyPopupColor", BindingFlags.NonPublic | BindingFlags.Instance);

					// headless 防抖窗口拉长（见类注释 b）。
					((DispatcherTimer)timerField.GetValue(dialog)).Interval = TimeSpan.FromMilliseconds(3000);

					// 生产打开路径：填 hex（TextChanged 延迟到布局 pass 触发，见类注释 a）+ 打开 Popup。
					dialog.PopupHexBox.Text = "#123456";
					dialog.ColorPickerPopup.PlacementTarget = dialog.ColorListControl;
					dialog.ColorPickerPopup.IsOpen = true;
					Dispatcher.UIThread.RunJobs();

					// 拖动中连续 5 次 PointerMove 级别的改色（模拟拖 HSV/滑块的 60Hz 调用）。
					for (int i = 0; i < 5; i++)
					{
						dialog.PopupHexBox.Text = "#654321";
						applyMethod.Invoke(dialog, null);
					}

					// 轻量分离：_workingCopy/列表项已更新（INPC 预览即时跟随），但重活尚未执行
					//（_hasPendingApply=true；ApplyAndRefresh 总是 new 一个字典赋给 settings.CustomColors，
					// 引用未变 = 重活没跑过，settings 未被污染）。
					bool pendingAfterDrag = (bool)pendingField.GetValue(dialog);
					bool settingsUntouched = ReferenceEquals(settings.CustomColors, originalRef);

					// Popup 关闭 → flush：最后一次改色立即应用 + 落盘。
					dialog.ColorPickerPopup.IsOpen = false;
					Dispatcher.UIThread.RunJobs();

					bool flushedAfterClose = (bool)pendingField.GetValue(dialog) == false
						&& settings.CustomColors != null
						&& settings.CustomColors.TryGetValue("BackgroundColor", out string applied)
						&& applied == "#654321";

					dialog.Close();
					return "pending=" + pendingAfterDrag + " untouched=" + settingsUntouched + " flushed=" + flushedAfterClose;
				}
				finally
				{
					// 恢复全局 settings，避免污染其他测试。
					settings.CustomColors = savedCustomColors;
					settings.UseCustomColors = savedUseCustomColors;
				}
			});
			Assert.True(diag == "pending=True untouched=True flushed=True", diag);
		}

		// ===== 4) 防抖到期：拖动停止后 150ms 自动应用（不依赖 popup 关闭）=====

		[Fact]
		public void ApplyPopupColor_DebounceTimer_AutoAppliesAfterDragStops()
		{
			var settings = ForkPlusSettings.Default;
			var originalRef = settings.CustomColors;
			var savedCustomColors = originalRef == null
				? null
				: new System.Collections.Generic.Dictionary<string, string>(originalRef);
			bool savedUseCustomColors = settings.UseCustomColors;
			var dialog = default(global::ForkPlus.UI.Dialogs.CustomColorsDialog);
			bool pendingDuringDrag = false;
			bool untouchedDuringDrag = false;
			try
			{
				// Phase 1（UI 线程）：设置编辑项 + 打开 Popup + 模拟拖动（防抖计时器反复重置）。
				HeadlessAppBootstrap.Run(delegate
				{
					dialog = new global::ForkPlus.UI.Dialogs.CustomColorsDialog();
					dialog.Show();
					dialog.UpdateLayout();

					FieldInfo itemsField = typeof(global::ForkPlus.UI.Dialogs.CustomColorsDialog)
						.GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance);
					var items = (System.Collections.IList)itemsField.GetValue(dialog);
					FieldInfo editingField = typeof(global::ForkPlus.UI.Dialogs.CustomColorsDialog)
						.GetField("_popupEditingItem", BindingFlags.NonPublic | BindingFlags.Instance);
					editingField.SetValue(dialog, items[0]);
					FieldInfo pendingField = typeof(global::ForkPlus.UI.Dialogs.CustomColorsDialog)
						.GetField("_hasPendingApply", BindingFlags.NonPublic | BindingFlags.Instance);
					FieldInfo timerField = typeof(global::ForkPlus.UI.Dialogs.CustomColorsDialog)
						.GetField("_applyDebounceTimer", BindingFlags.NonPublic | BindingFlags.Instance);
					MethodInfo applyMethod = typeof(global::ForkPlus.UI.Dialogs.CustomColorsDialog)
						.GetMethod("ApplyPopupColor", BindingFlags.NonPublic | BindingFlags.Instance);

					// headless 防抖窗口拉长（见类注释 b），保证 Phase 1 内重活不提前执行。
					var timer = (DispatcherTimer)timerField.GetValue(dialog);
					timer.Interval = TimeSpan.FromMilliseconds(3000);

					dialog.PopupHexBox.Text = "#123456";
					dialog.ColorPickerPopup.PlacementTarget = dialog.ColorListControl;
					dialog.ColorPickerPopup.IsOpen = true;
					Dispatcher.UIThread.RunJobs();

					// 拖动中连续 5 次改色（模拟 60Hz 拖 HSV/滑块）。
					for (int i = 0; i < 5; i++)
					{
						dialog.PopupHexBox.Text = "#654321";
						applyMethod.Invoke(dialog, null);
					}
					pendingDuringDrag = (bool)pendingField.GetValue(dialog);
					untouchedDuringDrag = ReferenceEquals(settings.CustomColors, originalRef);

					// 拖动结束，恢复生产防抖值 150ms：Interval setter 在 _isEnabled 时会把
					// DueTimeInMs 重调度为 now+150（等价"停止拖动后 150ms"，见类注释 b）。
					timer.Interval = TimeSpan.FromMilliseconds(150);
				});

				// Phase 2（测试线程等待）：UI 线程主循环空闲 → 150ms 防抖计时器到期自动应用
				//（见类注释 c：空闲主循环自动驱动到期计时器，等待必须发生在测试线程上，
				// 在 UI 线程内 Sleep 会阻塞主循环导致 Tick 永不触发）。
				System.Threading.Thread.Sleep(600);

				// Phase 3（UI 线程）：验证自动应用已发生（_hasPendingApply 清零 + settings 已写入）。
				bool autoApplied = HeadlessAppBootstrap.Run(delegate
				{
					FieldInfo pendingField = typeof(global::ForkPlus.UI.Dialogs.CustomColorsDialog)
						.GetField("_hasPendingApply", BindingFlags.NonPublic | BindingFlags.Instance);
					bool applied = !(bool)pendingField.GetValue(dialog)
						&& settings.CustomColors != null
						&& settings.CustomColors.TryGetValue("BackgroundColor", out string hex)
						&& hex == "#654321";
					dialog.ColorPickerPopup.IsOpen = false;
					Dispatcher.UIThread.RunJobs();
					dialog.Close();
					return applied;
				});
				Assert.True(pendingDuringDrag && untouchedDuringDrag && autoApplied,
					"pendingDuringDrag=" + pendingDuringDrag + " untouchedDuringDrag=" + untouchedDuringDrag + " autoApplied=" + autoApplied);
			}
			finally
			{
				settings.CustomColors = savedCustomColors;
				settings.UseCustomColors = savedUseCustomColors;
			}
		}

		// ===== 助手 =====

		private static void SimulateDeactivated(Window window)
		{
			// headless 无平台激活切换，反射触发 WindowBase 内部失活路径
			//（与 GitMmPopupDismissTests 相同手法）。
			MethodInfo method = typeof(WindowBase).GetMethod("HandleDeactivated",
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			Assert.NotNull(method);
			method.Invoke(window, null);
		}

		private static string FindRepositoryRoot()
		{
			string directory = AppContext.BaseDirectory;
			while (!string.IsNullOrWhiteSpace(directory))
			{
				if (Directory.Exists(Path.Combine(directory, ".git")) || File.Exists(Path.Combine(directory, ".git")))
				{
					return directory;
				}
				directory = Path.GetDirectoryName(directory);
			}
			throw new DirectoryNotFoundException("Could not find repository root.");
		}
	}
}
