// 回归测试（2026-09-05，"自定义颜色弹窗有概率关不掉/要点好几下才能关掉，甚至 UI 卡死崩溃"）：
// 根因三层（详见 CustomColorsDialog.axaml.cs 内 Migration note）：
//   1) 关窗销毁路径跑重活：OnClosed/关窗 Deactivated → Popup 关 → FlushPendingApply →
//      ApplyAndRefresh（重载整个主题字典 + 广播 20+ 订阅者刷新 + 同步写盘）同步压在
//      teardown 上——关窗卡数百 ms（连点 X）+ 订阅者异常 = 未处理 dispatcher 异常 = 崩溃；
//   2) light dismiss 吞第一次 X 按压（需二次点击，与 WPF StaysOpen=false 一致，但叠加 1)
//      的阻塞被放大成"点了没反应"）；
//   3) Popup_Closed 同步 flush 阻塞同一次点击的 PointerReleased 分发——release 到达时距
//      关闭时刻超 300ms 守卫窗口，ColorPreview_Click 把刚关掉的弹板又开回去（弹板"关不掉"）。
// 修复不变量：
//   A) 关窗路径只做轻持久化（settings 赋值 + Save），App.ApplyCustomColors 推迟到销毁完成后
//      （Post Background）；重活绝不跑在 OnClosed 同步路径上；
//   B) 弹窗弹板关闭时 flush 推迟一拍（Post Background），守卫时间戳与按压同拍可用；
//   C) _isClosing 守卫：关窗触发的 Deactivated/Popup_Closed 不再操作 Popup/跑重活；
//   D) 列表 hex 输入改色经 HexValue setter 编辑回调走防抖（绑定初始化不误触发、
//      程序化赋值被 _suppressUpdates 抑制、防抖前 settings 不被污染）。
using System;
using System.IO;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using ForkPlus.Settings;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class CustomColorsDialogCloseTests
	{
		private const string TestHex = "#0AC0DE"; // 全套件唯一值，避免既有测试资源污染干扰断言

		// ===== A) 关窗：轻持久化同步、重活推迟到销毁之后 =====

		[Fact]
		public void CloseWithPending_PersistsLightweightSync_AppliesDeferredAfterTeardown()
		{
			var settings = ForkPlusSettings.Default;
			var originalRef = settings.CustomColors;
			var savedCustomColors = originalRef == null
				? null
				: new System.Collections.Generic.Dictionary<string, string>(originalRef);
			bool savedUseCustomColors = settings.UseCustomColors;
			string settingsPath = null;
			string settingsBackup = null;
			try
			{
				string diag = HeadlessAppBootstrap.Run(delegate
				{
					settingsBackup = ReadSettingsFile(out settingsPath);
					var dialog = NewShownDialogWithPending(out FieldInfo pendingField);

					// 关窗（同步走 OnClosing → OnClosed）：
					dialog.Close();

					// 轻持久化已完成（settings 内存对象已拿到最后一步，关窗不丢改动）
					bool persisted = settings.CustomColors != null
						&& settings.CustomColors.TryGetValue("BackgroundColor", out string applied)
						&& applied == TestHex;
					bool pendingCleared = !(bool)pendingField.GetValue(dialog);
					// 重活尚未跑（App.ApplyCustomColors 已 Post 未执行）：主题资源仍是原色。
					// 修复前 OnClosed 同步 FlushPendingApply → ApplyAndRefresh → 资源立即被改写，
					// 关窗被重活阻塞数百 ms。
					bool heavyDeferred = !ResourceHexEquals("BackgroundColor", TestHex);

					// 销毁完成后重活补跑（Post Background 由 RunJobs 执行）
					Dispatcher.UIThread.RunJobs();
					bool appliedAfterTeardown = ResourceHexEquals("BackgroundColor", TestHex);

					return "persisted=" + persisted + " pendingCleared=" + pendingCleared
						+ " heavyDeferred=" + heavyDeferred + " appliedAfterTeardown=" + appliedAfterTeardown;
				});
				Assert.True(diag == "persisted=True pendingCleared=True heavyDeferred=True appliedAfterTeardown=True", diag);
			}
			finally
			{
				RestoreSettingsAndResources(savedCustomColors, savedUseCustomColors, settingsPath, settingsBackup);
			}
		}

		// ===== B) 弹板关闭：flush 推迟一拍（守卫时间戳同拍可用、重活不阻塞 release 分发）=====

		[Fact]
		public void PopupClosed_FlushDeferredOneBeat_GuardTimestampAvailableImmediately()
		{
			var settings = ForkPlusSettings.Default;
			var originalRef = settings.CustomColors;
			var savedCustomColors = originalRef == null
				? null
				: new System.Collections.Generic.Dictionary<string, string>(originalRef);
			bool savedUseCustomColors = settings.UseCustomColors;
			string settingsPath = null;
			string settingsBackup = null;
			try
			{
				string diag = HeadlessAppBootstrap.Run(delegate
				{
					settingsBackup = ReadSettingsFile(out settingsPath);
					var dialog = NewShownDialogWithPending(out FieldInfo pendingField);
					FieldInfo closedAtField = typeof(global::ForkPlus.UI.Dialogs.CustomColorsDialog)
						.GetField("_popupClosedAtUtc", BindingFlags.NonPublic | BindingFlags.Instance);
					MethodInfo popupClosed = typeof(global::ForkPlus.UI.Dialogs.CustomColorsDialog)
						.GetMethod("Popup_Closed", BindingFlags.NonPublic | BindingFlags.Instance);

					// 弹板关闭（等价点外部）：修复前同步 FlushPendingApply——settings 立即被写 +
					// 重活阻塞同一次点击的 release 分发（300ms 守卫窗口被吞，弹板"关不掉"）。
					popupClosed.Invoke(dialog, new object[] { dialog.ColorPickerPopup, EventArgs.Empty });

					bool flushDeferred = ReferenceEquals(settings.CustomColors, originalRef);
					bool guardTimestampImmediate = closedAtField.GetValue(dialog) != null;

					// 一拍之后 flush 补跑：最后一次改色应用 + 落盘
					Dispatcher.UIThread.RunJobs();
					bool flushedAfterOneBeat = !ReferenceEquals(settings.CustomColors, originalRef)
						&& settings.CustomColors != null
						&& settings.CustomColors.TryGetValue("BackgroundColor", out string applied)
						&& applied == TestHex
						&& !(bool)pendingField.GetValue(dialog);

					dialog.Close();
					Dispatcher.UIThread.RunJobs();
					return "flushDeferred=" + flushDeferred + " guardTimestampImmediate=" + guardTimestampImmediate
						+ " flushedAfterOneBeat=" + flushedAfterOneBeat;
				});
				Assert.True(diag == "flushDeferred=True guardTimestampImmediate=True flushedAfterOneBeat=True", diag);
			}
			finally
			{
				RestoreSettingsAndResources(savedCustomColors, savedUseCustomColors, settingsPath, settingsBackup);
			}
		}

		// ===== C) 关窗守卫：弹板开着关窗不炸、不跑重活；销毁中失活不再操作 Popup =====

		[Fact]
		public void CloseWithPopupOpen_TeardownGuard_NoHeavyWorkInClosePath()
		{
			var settings = ForkPlusSettings.Default;
			var originalRef = settings.CustomColors;
			var savedCustomColors = originalRef == null
				? null
				: new System.Collections.Generic.Dictionary<string, string>(originalRef);
			bool savedUseCustomColors = settings.UseCustomColors;
			string settingsPath = null;
			string settingsBackup = null;
			try
			{
				string diag = HeadlessAppBootstrap.Run(delegate
				{
					settingsBackup = ReadSettingsFile(out settingsPath);
					var dialog = NewShownDialogWithPending(out FieldInfo pendingField);
					FieldInfo closingField = typeof(global::ForkPlus.UI.Dialogs.CustomColorsDialog)
						.GetField("_isClosing", BindingFlags.NonPublic | BindingFlags.Instance);

					// 弹板开着关窗（最容易踩销毁路径重活/PopupRoot 竞态的场景）
					dialog.ColorPickerPopup.PlacementTarget = dialog.ColorListControl;
					dialog.ColorPickerPopup.IsOpen = true;
					Dispatcher.UIThread.RunJobs();

					dialog.Close(); // OnClosing 置 _isClosing → Deactivated/Popup_Closed 走守卫

					bool closingFlagSet = (bool)closingField.GetValue(dialog);
					bool windowClosed = !dialog.IsVisible;
					// 关窗路径未同步跑重活（资源未改写 = ApplyCustomColors 未在 teardown 上执行）
					bool noHeavyInTeardown = !ResourceHexEquals("BackgroundColor", TestHex);
					// 轻持久化已做（不丢改动）
					bool persisted = settings.CustomColors != null
						&& settings.CustomColors.TryGetValue("BackgroundColor", out string applied)
						&& applied == TestHex;

					// 销毁中失活（平台关窗必触发）：守卫生效，不再操作 Popup，不抛异常
					bool deactivatedAfterCloseNoThrow = true;
					try
					{
						SimulateDeactivated(dialog);
					}
					catch
					{
						deactivatedAfterCloseNoThrow = false;
					}
					Dispatcher.UIThread.RunJobs();
					bool appliedAfterTeardown = ResourceHexEquals("BackgroundColor", TestHex);

					return "closingFlagSet=" + closingFlagSet + " windowClosed=" + windowClosed
						+ " noHeavyInTeardown=" + noHeavyInTeardown + " persisted=" + persisted
						+ " deactivatedAfterCloseNoThrow=" + deactivatedAfterCloseNoThrow
						+ " appliedAfterTeardown=" + appliedAfterTeardown;
				});
				Assert.True(diag == "closingFlagSet=True windowClosed=True noHeavyInTeardown=True persisted=True "
					+ "deactivatedAfterCloseNoThrow=True appliedAfterTeardown=True", diag);
			}
			finally
			{
				RestoreSettingsAndResources(savedCustomColors, savedUseCustomColors, settingsPath, settingsBackup);
			}
		}

		// ===== D) 列表 hex 输入：编辑回调走防抖；绑定初始化/程序化赋值不误触发 =====

		[Fact]
		public void ListHexEdit_EditorCallback_Debounced_InitAndProgrammaticSuppressed()
		{
			var settings = ForkPlusSettings.Default;
			var originalRef = settings.CustomColors;
			var savedCustomColors = originalRef == null
				? null
				: new System.Collections.Generic.Dictionary<string, string>(originalRef);
			bool savedUseCustomColors = settings.UseCustomColors;
			string settingsPath = null;
			string settingsBackup = null;
			try
			{
				// Phase 1：对话框就绪 + 绑定初始化不触发编辑回调
				bool initClean = HeadlessAppBootstrap.Run(delegate
				{
					settingsBackup = ReadSettingsFile(out settingsPath);
					var dialog = NewShownDialog(out FieldInfo pendingField, out FieldInfo workingCopyField, out var items);
					// 列表容器已生成、绑定已初始化（source→target 只读 getter）——不得有任何
					// 误起防抖（修复回归：绑定初始化 ≠ 用户改色）。
					// LoadItems 会把已保存的自定义项放进 _workingCopy——初始化"干净"的判据是
					// pending 不置位（没跑防抖），而非 workingCopy 为空。
					return !(bool)pendingField.GetValue(dialog);
				});
				Assert.True(initClean, "绑定初始化不应触发编辑回调（防抖误置位）");

				// Phase 2：用户改 hex（等价 TwoWay 绑定 target→source 推送）→ 防抖
				string diag = HeadlessAppBootstrap.Run(delegate
				{
					var dialog = NewShownDialog(out FieldInfo pendingField, out FieldInfo workingCopyField, out var items);
					var item = (global::ForkPlus.UI.Dialogs.CustomColorsDialog.CustomColorItem)items[0];

					// 用户改色（绑定推送 → HexValue setter → 编辑回调）
					item.HexValue = TestHex;
					bool pendingAfterEdit = (bool)pendingField.GetValue(dialog);
					var workingCopy = (System.Collections.Generic.Dictionary<string, string>)workingCopyField.GetValue(dialog);
					bool workingCopyUpdated = workingCopy.TryGetValue("BackgroundColor", out string wc) && wc == TestHex;
					bool settingsNotPolluted = ReferenceEquals(settings.CustomColors, originalRef); // 防抖未到期不写盘

					// 程序化赋值（Reset/Random/Import 同路径）被 _suppressUpdates 抑制：
					// 回调不得把值写回 workingCopy（否则 Reset 刚删的 key 被回写 = "Reset 失效"）
					FieldInfo suppressField = typeof(global::ForkPlus.UI.Dialogs.CustomColorsDialog)
						.GetField("_suppressUpdates", BindingFlags.NonPublic | BindingFlags.Instance);
					suppressField.SetValue(dialog, true);
					item.HexValue = "#FF00FF";
					suppressField.SetValue(dialog, false);
					bool suppressedNoRewrite = workingCopy.TryGetValue("BackgroundColor", out string wc2) && wc2 == TestHex;

					dialog.Close();
					Dispatcher.UIThread.RunJobs();
					return "pendingAfterEdit=" + pendingAfterEdit + " workingCopyUpdated=" + workingCopyUpdated
						+ " settingsNotPolluted=" + settingsNotPolluted + " suppressedNoRewrite=" + suppressedNoRewrite;
				});
				Assert.True(diag == "pendingAfterEdit=True workingCopyUpdated=True settingsNotPolluted=True "
					+ "suppressedNoRewrite=True", diag);
			}
			finally
			{
				RestoreSettingsAndResources(savedCustomColors, savedUseCustomColors, settingsPath, settingsBackup);
			}
		}

		// ===== 助手 =====

		// 读取 settings.json 文件内容（null = 文件不存在）。修复的 OnClosed 会 Save() 写真实
		// settings.json（~/.local/share/ForkPlus/），测试必须在文件层备份/恢复——只恢复内存
		// settings 不够：污染落盘后，下一个测试进程启动即加载脏 CustomColors（且 UseCustomColors
		// 已被置 true），App 启动路径 ApplyCustomColors 把脏色 merge 进主题资源，污染后续所有
		// 断言（实测：全套件 5 个用例连环假红）。备份/恢复手法与 UiSmokeHeadlessTests.
		// CustomColorsDialog_RandomPalette_Click_NoCrash 一致。
		// ⚠️ 线程规则：App.ForkDirectoryPath 触发 App 静态构造（含 SolidColorBrush 等 Avalonia
		// 对象），必须在 headless UI 线程启动之后、且在 UI 线程上访问——所以备份收进每个测试
		// 第一次 Run 委托的开头执行，恢复收进 RestoreSettingsAndResources 的 Run 委托内。
		private static string ReadSettingsFile(out string path)
		{
			path = Path.Combine(global::ForkPlus.App.ForkDirectoryPath, "settings.json");
			return File.Exists(path) ? File.ReadAllText(path) : null;
		}

		private static void WriteSettingsFile(string path, string backup)
		{
			if (backup != null)
			{
				File.WriteAllText(path, backup);
			}
			else if (File.Exists(path))
			{
				// 测试前不存在 settings.json（全新环境）：恢复"不存在"状态。
				File.Delete(path);
			}
		}

		// 创建已显示、带 pending 改色（防抖挂起）的对话框；防抖 Interval 拉长到 3s 防自动触发。
		private static global::ForkPlus.UI.Dialogs.CustomColorsDialog NewShownDialogWithPending(out FieldInfo pendingField)
		{
			var dialog = NewShownDialog(out pendingField, out FieldInfo workingCopyField, out var items);
			FieldInfo editingField = typeof(global::ForkPlus.UI.Dialogs.CustomColorsDialog)
				.GetField("_popupEditingItem", BindingFlags.NonPublic | BindingFlags.Instance);
			FieldInfo timerField = typeof(global::ForkPlus.UI.Dialogs.CustomColorsDialog)
				.GetField("_applyDebounceTimer", BindingFlags.NonPublic | BindingFlags.Instance);
			MethodInfo applyMethod = typeof(global::ForkPlus.UI.Dialogs.CustomColorsDialog)
				.GetMethod("ApplyPopupColor", BindingFlags.NonPublic | BindingFlags.Instance);

			// headless 防抖窗口拉长（同 CustomColorsPopupTests 类注释 b：防 RunJobs 内 PromoteTimers 提前触发）
			((DispatcherTimer)timerField.GetValue(dialog)).Interval = TimeSpan.FromMilliseconds(3000);

			editingField.SetValue(dialog, items[0]);
			dialog.PopupHexBox.Text = TestHex;
			Dispatcher.UIThread.RunJobs();
			applyMethod.Invoke(dialog, null);
			return dialog;
		}

		private static global::ForkPlus.UI.Dialogs.CustomColorsDialog NewShownDialog(
			out FieldInfo pendingField, out FieldInfo workingCopyField, out System.Collections.IList items)
		{
			var dialog = new global::ForkPlus.UI.Dialogs.CustomColorsDialog();
			dialog.Show();
			dialog.UpdateLayout();
			Dispatcher.UIThread.RunJobs(); // 容器生成 + 绑定初始化
			pendingField = typeof(global::ForkPlus.UI.Dialogs.CustomColorsDialog)
				.GetField("_hasPendingApply", BindingFlags.NonPublic | BindingFlags.Instance);
			workingCopyField = typeof(global::ForkPlus.UI.Dialogs.CustomColorsDialog)
				.GetField("_workingCopy", BindingFlags.NonPublic | BindingFlags.Instance);
			FieldInfo itemsField = typeof(global::ForkPlus.UI.Dialogs.CustomColorsDialog)
				.GetField("_items", BindingFlags.NonPublic | BindingFlags.Instance);
			items = (System.Collections.IList)itemsField.GetValue(dialog);
			return dialog;
		}

		// 当前应用资源里某 Color key 的 hex 是否等于期望值（穿透合并字典，含自定义覆盖）
		private static bool ResourceHexEquals(string key, string expectedHex)
		{
			object obj = ResourceCompat.TryFindResource(Application.Current, key);
			if (obj is Color c)
			{
				return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2") == expectedHex;
			}
			return false;
		}

		// 恢复全局 settings（内存）+ 重放 ApplyCustomColors 清理应用资源里的测试覆盖 +
		// 恢复 settings.json 文件（OnClosed 的 Save 会写盘，见 ReadSettingsFile 注释）。
		private static void RestoreSettingsAndResources(
			System.Collections.Generic.Dictionary<string, string> savedCustomColors, bool savedUseCustomColors,
			string settingsPath = null, string settingsBackup = null)
		{
			HeadlessAppBootstrap.Run(delegate
			{
				ForkPlusSettings.Default.CustomColors = savedCustomColors;
				ForkPlusSettings.Default.UseCustomColors = savedUseCustomColors;
				global::ForkPlus.App.ApplyCustomColors();
				if (settingsPath != null)
				{
					try
					{
						WriteSettingsFile(settingsPath, settingsBackup);
					}
					catch
					{
						// 文件恢复失败不阻断测试收尾（内存/资源已恢复）
					}
				}
				return 0;
			});
		}

		private static void SimulateDeactivated(Window window)
		{
			// headless 无平台激活切换，反射触发 WindowBase 内部失活路径
			//（与 CustomColorsPopupTests/GitMmPopupDismissTests 相同手法）。
			MethodInfo method = typeof(WindowBase).GetMethod("HandleDeactivated",
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			Assert.NotNull(method);
			method.Invoke(window, null);
		}
	}
}
