using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>自定义颜色编辑对话框。列出可自定义的核心颜色，支持 hex 输入和 HSV 颜色选择器。
	/// 改动即时应用到主界面并立即落盘到 settings.json（每次换色都 Save），无需 OK/Cancel 确认。
	/// 关闭对话框靠窗口标题栏 X 按钮；颜色选择器 Popup 靠点外部关闭。</summary>
	public partial class CustomColorsDialog : ForkPlusDialogWindow
	{
		/// <summary>可自定义的颜色 key 列表（Colors.*.xaml 中的 Color resource key）。
		/// 只暴露核心颜色，不暴露全部 260+ key。</summary>
		private static readonly string[] _editableColorKeys = new string[]
	{
		"BackgroundColor",
		"SecondaryBackgroundColor",
		"PanelBackgroundColor",
		"BorderColor",
		"TileBorderColor",
		"LabelColor",
		"ForegroundColor",
		"SecondaryLabelColor",
		"AccentColor",
		"AccentSecondaryColor",
		"ReferenceColor",
		"IconColor",
		"Diff.AddedColor",
		"Diff.RemovedColor",
		"Diff.AddColor",
		"Diff.RemoveColor",
		"Diff.ExactAddColor",
		"Diff.ExactRemoveColor",
		"LineNumber.ForegroundColor",
		"LineNumber.SeparatorColor",
		"ChunkSelection.BorderColor",
		"ChunkSelection.BackgroundColor",
		"Syntax.CommentColor",
		"Syntax.StringColor",
		"Syntax.KeywordColor",
		"Syntax.NumberColor",
		"CodeEditor.BackgroundColor",
		"CodeEditor.ForegroundColor",
		"Window.BackgroundColor",
		"Window.TitleBar.BackgroundColor",
	};

		private List<CustomColorItem> _items;
	private Dictionary<string, string> _workingCopy;
	private CustomColorItem _popupEditingItem;
	private bool _suppressUpdates;
	private bool _isDraggingHsv;
	private bool _isDraggingHue;
	// Popup 初始化阶段标志：ColorPreview_Click 打开 Popup 时用 item 当前 hex 初始化控件，
	// 此时不应该把"初始化值"当成"用户改色"写回 _workingCopy（避免覆盖已有自定义值）。
	private bool _isPopupInitializing;

	// ===== Bug 修复（2026-09-04，"颜色管理器面板有性能问题"）=====
	// 拖 HSV 方块/色相条/RGB 滑块时每个 PointerMove 都触发一次 ApplyPopupColor →
	// ApplyAndRefresh → App.ApplyCustomColors（重载整个 Generic 主题字典 ~290 Color +
	// 270 Brush 并让全 UI DynamicResource 失效重解析）+ RaiseApplicationThemeChanged
	// （20+ 订阅控件刷新）+ 30 项预览重建 + 同步写盘。WPF 下 BAML 加载快勉强可用；
	// Avalonia 每次都重新解析 axaml 字典，60Hz 拖动下 UI 直接卡死。
	// 修复：拖动中只做轻量更新（item.HexValue → 列表预览色块经 INPC 立即刷新），
	// 重活（主题字典重载 + 全 UI 刷新 + 落盘）防抖 150ms，停止拖动后才执行一次；
	// Popup 关闭/对话框关闭时立即 flush，保证"实时预览"语义不丢（最迟 150ms）。
	// 防抖用 Avalonia 原生 DispatcherTimer（UI 线程计时器）：不走 DelayedAction 的
	// ServiceLocator.Dispatcher 转发链（该服务仅主程序启动时初始化，对话框早于其
	// 初始化/测试环境为 null 时回调会被静默丢弃）。
	private global::Avalonia.Threading.DispatcherTimer _applyDebounceTimer;
	private bool _hasPendingApply;

	// ===== Bug 修复（2026-09-04，"颜色管理器面板失焦关不掉 + 点色块关了又开"）=====
	// light dismiss 关闭时刻：点击色块本身的按压先经 light dismiss 关掉 Popup，
	// 抬起时 PointerReleased 又触重开（WPF 下该次点击被 StaysOpen=False 的 Popup
	// 捕获吞掉，表现为开→关切换）。借时间戳识别"刚被本次按压关闭"以免重开。
	private DateTime? _popupClosedAtUtc;

	// ===== Bug 修复（2026-09-05，"自定义颜色弹窗有概率关不掉/要点好几下，甚至 UI 卡死崩溃"）=====
	// 关窗销毁路径守卫。关窗时序里平台会先把激活权移交别的窗口（触发 Deactivated），
	// 旧代码在 Deactivated 里无条件 ColorPickerPopup.IsOpen = false → Popup_Closed →
	// FlushPendingApply → ApplyAndRefresh——重活（重载整个主题字典 + 全 UI 资源失效 +
	// 广播 20+ 订阅者刷新 + 同步写盘）全部同步跑在窗口 teardown 路径上：
	//   1) 关窗被阻塞数百 ms → 用户连点 X（"要点好几下才能关掉"）；
	//   2) 订阅者刷新可能访问正在拆除的控件树，OnClosed 又无异常保护——任何订阅者
	//      异常都是未处理 dispatcher 异常，直接崩掉整个应用（"UI 卡死崩溃"）。
	// 置位时机：OnClosing（用户点 X 的主路径）+ OnClosed 兜底（Owner 级联关闭等
	// 可能不经 OnClosing）。
	private bool _isClosing;

	public CustomColorsDialog()
	{
		// 关闭基类 ForkPlusDialogWindow 自动添加的 chrome（logo/header/footer/command preview）。
		// 基类假设内容 Grid 是两列布局（Column 0=logo 列，Column 1=内容），会自动塞入 64x64
		// ForkPlus logo + 标题头 + 底部 Submit/Cancel footer。本对话框自定义布局，不兼容该结构，
		// 若不关闭会导致 logo 与颜色列表叠在 Column 0 上挤在一起，且 footer 与自定义按钮重复。
		ShowHeader = false;
		ShowLogo = false;
		ShowFooter = false;
		InitializeComponent();
		Localize();
		LoadItems();
		InitializeSwatches();
		_applyDebounceTimer = new global::Avalonia.Threading.DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(150.0)
		};
		_applyDebounceTimer.Tick += delegate
		{
			_applyDebounceTimer.Stop();
			_hasPendingApply = false;
			ApplyAndRefresh();
		};
		// Popup 关闭（点外部）时清空正在编辑的 item，避免后续误写；并 flush 防抖中的待应用色。
		ColorPickerPopup.Closed += Popup_Closed;
		// Bug 修复（失焦消失）：窗口失活（切应用/点其他窗口）→ 关闭 Popup，
		// 对齐 WPF StaysOpen=False 的失活关闭语义（light dismiss 只覆盖窗口内按压）。
		// 2026-09-05：关窗销毁路径上平台必触发失活——此时不再操作 Popup（销毁中改 IsOpen
		// 是 PopupRoot 生命周期竞态，且会连带 Popup_Closed 在 teardown 里跑重活，见 _isClosing）。
		Deactivated += delegate
		{
			if (_isClosing)
			{
				return;
			}
			ColorPickerPopup.IsOpen = false;
		};
	}

		private void Localize()
		{
			string lang = ForkPlusSettings.Default.UiLanguage;
			Title = PreferencesLocalization.Translate("Custom Colors", lang);
			HeaderTextBlock.Text = PreferencesLocalization.Translate("Custom Colors", lang) +
				" (" + PreferencesLocalization.Translate(ForkPlusSettings.Default.Theme.SkinName(), lang) + ")";
			ResetAllButton.Content = PreferencesLocalization.Translate("Reset All", lang);
		RandomPaletteButton.Content = PreferencesLocalization.Translate("Random Palette", lang);
			ImportColorsButton.Content = PreferencesLocalization.Translate("Import Colors", lang);
			ExportColorsButton.Content = PreferencesLocalization.Translate("Export Colors", lang);
			PopupTitleText.Text = PreferencesLocalization.Translate("Color Picker", lang);
			SwatchLabelText.Text = PreferencesLocalization.Translate("Presets", lang);
			// DataTemplate 里的 "Reset" 按钮文字在 LoadItems 后通过遍历设置
		}

		/// <summary>加载颜色列表。每项显示当前生效值（自定义覆盖或预设原色）。</summary>
		private void LoadItems()
	{
		_workingCopy = new Dictionary<string, string>();
		Dictionary<string, string> saved = ForkPlusSettings.Default.CustomColors;
			_items = new List<CustomColorItem>();
			string lang = ForkPlusSettings.Default.UiLanguage;
			string resetLabel = PreferencesLocalization.Translate("Reset", lang);
			foreach (string key in _editableColorKeys)
			{
				string hex;
				bool isCustomized;
				if (saved != null && saved.TryGetValue(key, out string savedHex) && !string.IsNullOrEmpty(savedHex))
				{
					hex = savedHex;
					isCustomized = true;
					_workingCopy[key] = hex;
				}
				else
				{
					hex = GetCurrentColorHex(key);
					isCustomized = false;
				}
				_items.Add(new CustomColorItem(key, TranslateColorKey(key, lang), hex, isCustomized, resetLabel));
		}
		// 注入编辑回调：列表 hex 框改色经 HexValue setter 走防抖应用（2026-09-05）
		foreach (CustomColorItem item in _items)
		{
			item.HexValueEdited = OnItemHexEdited;
		}
		ColorListControl.ItemsSource = _items;
		}

		/// <summary>从当前 Application.Resources 取某个 Color key 的 hex 值（预设原色）。</summary>
		private string GetCurrentColorHex(string key)
	{
		try
		{
			// Migration note（2026-09-03，"自定义颜色窗口不加载当前颜色，显示全是 #FFFFFF"根因）：
			// WPF 的 Application.Current.Resources[key] 索引器会穿透 MergedDictionaries 找到
			// 主题色（Generic.{Skin}.axaml 合并在 App.Resources.MergedDictionaries 里）；Avalonia
			// 的索引器只查顶层字典（headless 探针实测：BackgroundColor 明明在合并字典里，
			// Resources["BackgroundColor"] 却返回 null），30 个颜色 key 全部命中不了 → 全部走
			// fallback "#FFFFFF"。改用 ResourceCompat.TryFindResource（底层 Resources.TryGetResource）：
			// 与 WPF 索引器同语义——先查顶层、再逆序穿透合并字典（末尾 merge 的自定义颜色
			// 覆盖字典优先命中），主题原色与自定义覆盖色都能取到。
				object obj = ResourceCompat.TryFindResource(Application.Current, key);
				if (obj is Color c)
					return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
			}
			catch { }
			return "#FFFFFF";
		}

		/// <summary>颜色 key → 国际化显示名。用 "Color." + key 作为 i18n key，
		/// 找不到翻译时返回 key 原文。</summary>
		private string TranslateColorKey(string key, string lang)
		{
			string i18nKey = "Color." + key;
			string translated = PreferencesLocalization.Translate(i18nKey, lang);
			// Translate 找不到时返回原文（即 "Color." + key），此时 fallback 到 key
			if (translated != null && translated == i18nKey)
				return key;
			return translated ?? key;
		}

		/// <summary>初始化预设色板（常用颜色快速选择）。</summary>
		private void InitializeSwatches()
		{
			string[] palette = new string[]
			{
				"#FFFFFF", "#C0C0C0", "#808080", "#404040", "#000000",
				"#FF0000", "#FF8000", "#FFFF00", "#80FF00", "#00FF00",
				"#00FF80", "#00FFFF", "#0080FF", "#0000FF", "#8000FF",
				"#FF00FF", "#FF0080", "#007ACC", "#3E9FF8", "#BD93F9",
				"#F8F8F2", "#282A36", "#21222C", "#44475A", "#1F2328",
				"#A855F7", "#1A1625", "#241B33", "#3D2E5C", "#E4E0EB",
			};
			foreach (string hex in palette)
			{
				Border swatch = new Border
				{
					Width = 20, Height = 20,
					Margin = new Thickness(2),
					// Migration note（2026-09-03，同 GetCurrentColorHex 根因）：BorderBrush 在
					// 合并主题字典里，WPF 索引器能穿透查到；Avalonia 索引器只查顶层字典返回
					// null → 预设色板 30 个色块全部无描边。改用 TryFindResource 取到画刷。
					BorderBrush = ResourceCompat.TryFindResource(Application.Current, "BorderBrush") as Brush,
					BorderThickness = new Thickness(1),
					Cursor = Cursors.Hand,
					Tag = hex,
				};
				try { swatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
				catch { continue; }
				swatch.PointerReleased += Swatch_Click;
				SwatchPanel.Children.Add(swatch);
			}
		}

		#region HSV 调色盘

		// 订阅方挂在 PointerReleased 上，处理函数参数类型须与 PointerReleasedEventArgs 匹配
		private void Swatch_Click(object sender, global::Avalonia.Input.PointerReleasedEventArgs e)
		{
			if (sender is Border b && b.Tag is string hex)
			{
				_suppressUpdates = true;
				PopupHexBox.Text = hex;
				_suppressUpdates = false;
				UpdatePopupFromHex(hex);
			}
		}

		/// <summary>颜色预览块点击（抬起）→ 打开颜色选择 Popup。
		/// Bug 修复（2026-09-04，"面板弹出来不对"）：原版 WPF 挂 MouseLeftButtonUp（抬起弹出），
		/// 迁移版错挂 PointerPressed（按下即弹，手未抬 Popup 已挡住光标）——恢复抬起弹出；
		/// 并锚定 PlacementTarget=被点色块 + Bottom（原版 Placement="Mouse" 的稳定等效，
		/// Pointer 模式跟随指针漂移）。另带"关了又开"守卫（见 _popupClosedAtUtc 注释）。</summary>
		private void ColorPreview_Click(object sender, global::Avalonia.Input.PointerReleasedEventArgs e)
		{
			// 守卫：本次按压若刚通过 light dismiss 关掉了 Popup（WPF 下该点击被
			// StaysOpen=False 的 Popup 捕获吞掉、只关不开），则不再重开——
			// 色块表现为开→关的切换，而不是关了又开。
			DateTime? closedAt = _popupClosedAtUtc;
			if (closedAt.HasValue && (DateTime.UtcNow - closedAt.Value).TotalMilliseconds < 300.0)
			{
				_popupClosedAtUtc = null;
				return;
			}
			if (sender is global::Avalonia.Controls.Control fe && fe.Tag is CustomColorItem item)
			{
				_popupEditingItem = item;
				// 锚定被点色块：Popup 弹在色块正下方（Bottom），不再跟随指针。
				ColorPickerPopup.PlacementTarget = fe;
				// 初始化阶段标志：用 item 当前 hex 填充 Popup 控件时不要回写 _workingCopy
				_isPopupInitializing = true;
				UpdatePopupFromHex(item.HexValue);
				_isPopupInitializing = false;
				ColorPickerPopup.IsOpen = true;
			}
		}

		/// <summary>从 hex 值更新整个 Popup 状态（HSV 方块、色相条、RGB 滑块、预览）。
		/// 非初始化阶段会顺带把当前 hex 实时写回 _workingCopy + 主界面 + 落盘。</summary>
		private void UpdatePopupFromHex(string hex)
		{
			try
			{
				Color c = (Color)ColorConverter.ConvertFromString(hex);
				// RGB 滑块
				_suppressUpdates = true;
				RSlider.Value = c.R;
				GSlider.Value = c.G;
				BSlider.Value = c.B;
				PopupHexBox.Text = "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
				PopupPreviewRect.Fill = new SolidColorBrush(c);
				// HSV 方块 + 色相条
				double h, s, v;
				RgbToHsv(c.R, c.G, c.B, out h, out s, out v);
				UpdateHsvCanvas(h, s, v);
				UpdateHueIndicator(h);
				_suppressUpdates = false;
				// 用户在 Popup 内的任何交互（拖滑块/HSV/色相条、改 hex、点预设色板）都实时落盘
				if (!_isPopupInitializing)
					ApplyPopupColor();
			}
			catch { }
		}

		/// <summary>把 Popup 当前 hex 实时写回正在编辑的 item + _workingCopy（轻量，INPC 立即刷列表预览），
		/// 重活（主题字典重载 + 全 UI 刷新 + 落盘）经防抖调度（见 _applyDebounce 注释）。
		/// 拖动滑块/HSV 时列表预览色块立即跟随，主界面与 settings.json 最迟 150ms 后同步。</summary>
		private void ApplyPopupColor()
		{
			if (_popupEditingItem == null) return;
			string hex = PopupHexBox.Text.Trim();
			if (string.IsNullOrEmpty(hex)) return;
			if (!hex.StartsWith("#")) hex = "#" + hex;
			try { ColorConverter.ConvertFromString(hex); }
			catch { return; }
			// 程序化赋值经 _suppressUpdates 抑制编辑回调（本方法自管 workingCopy/防抖）
			_suppressUpdates = true;
			_popupEditingItem.HexValue = hex;
			_popupEditingItem.IsCustomized = true;
			_suppressUpdates = false;
			_workingCopy[_popupEditingItem.Key] = hex;
			// 防抖重活：拖动中每个 PointerMove 都进这里，全量 ApplyAndRefresh 会卡死 UI。
			_hasPendingApply = true;
			_applyDebounceTimer.Stop();
			_applyDebounceTimer.Start();
		}

		/// <summary>立即执行防抖中的待应用色（Popup 关闭/对话框关闭时调用，保证不丢最后一次改动）。</summary>
		private void FlushPendingApply()
		{
			if (!_hasPendingApply) return;
			_hasPendingApply = false;
			_applyDebounceTimer.Stop();
			ApplyAndRefresh();
		}

		private void Popup_Closed(object sender, EventArgs e)
	{
		// 先记关闭时刻（供 ColorPreview_Click 的"关了又开"守卫判断），
		// 再调度 flush——时间戳与按压同拍，重活推迟到下一拍。
		_popupClosedAtUtc = DateTime.UtcNow;
		_popupEditingItem = null;
		// 关窗触发的 Popup 关闭不在此 flush（销毁路径禁跑重活，OnClosed 统一收尾）。
		if (_isClosing)
		{
			return;
		}
		// 关闭即 flush（用户选完色点外部关闭，最后一次改动立即应用 + 落盘）。
		// 2026-09-05：flush 从同步改为推迟一拍（Background 优先级）。同步 flush 时，
		// 重活会阻塞同一次点击的 PointerReleased 分发——release 到达时距关闭时刻已超
		// 300ms 守卫窗口（慢机重载主题字典可达数百 ms），ColorPreview_Click 会把刚被
		// 按压关掉的弹板又开回去（"弹板关不掉"）。推迟后 release 先于重活分发、守卫在
		// 窗口内生效；主界面变色延迟 < 一帧，"立即生效"语义不变。
		global::Avalonia.Threading.Dispatcher.UIThread.Post(FlushPendingApply, global::Avalonia.Threading.DispatcherPriority.Background);
	}

	/// <summary>对话框关闭：轻量收尾——只做持久化 + 推迟应用，绝不在销毁路径跑重活。
	/// 换色已实时落盘的设计下，关窗时 pending 的最后一步不能丢；但 OnClosed 里同步跑
	/// ApplyAndRefresh（重载主题字典 + 广播 20+ 订阅者刷新 + 写盘）会把重活压在窗口
	/// teardown 路径上——关窗卡顿数百 ms（"点好几下才能关掉"），订阅者异常 = 未处理
	/// dispatcher 异常 = 应用崩溃（2026-09-05）。改为：settings 赋值 + Save（轻）同步做，
	/// App.ApplyCustomColors 推迟到窗口销毁完成后（订阅者树里不再有半拆的对话框）。 </summary>
	protected override void OnClosed(EventArgs e)
	{
		_isClosing = true; // 兜底：Owner 级联关闭等路径可能不经 OnClosing
		try
		{
			_applyDebounceTimer.Stop();
		}
		catch
		{
		}
		base.OnClosed(e);
		try
		{
			if (_hasPendingApply)
			{
				_hasPendingApply = false;
				ForkPlusSettings.Default.CustomColors = new Dictionary<string, string>(_workingCopy);
				if (_workingCopy.Count > 0)
				{
					ForkPlusSettings.Default.UseCustomColors = true;
				}
				try { ForkPlusSettings.Default.Save(); } catch { /* 持久化失败不阻断关窗 */ }
				// 应用推迟到销毁完成后；方法组无捕获，不延长本对话框生命周期。
				global::Avalonia.Threading.Dispatcher.UIThread.Post(App.ApplyCustomColors, global::Avalonia.Threading.DispatcherPriority.Background);
			}
		}
		catch
		{
			// 销毁路径上任何异常都不允许冒泡成未处理 dispatcher 异常（= 崩溃）
		}
	}

	/// <summary>关窗最早可拦截点：置 _isClosing，让后续的 Deactivated/Popup_Closed
	/// 走销毁守卫（不再操作 Popup、不再在 teardown 里 flush 重活）。</summary>
	protected override void OnClosing(global::Avalonia.Controls.WindowClosingEventArgs e)
	{
		_isClosing = true;
		base.OnClosing(e);
	}

		/// <summary>更新 HSV 2D 方块的背景色（当前色相纯色）+ 指示器位置。</summary>
		private void UpdateHsvCanvas(double h, double s, double v)
		{
			Color pureHue = HsvToRgbColor(h, 1.0, 1.0);
			HsvBaseRect.Fill = new SolidColorBrush(pureHue);
			// x = 饱和度 * 宽, y = (1 - 明度) * 高
			double x = s * 240 - 5;  // -5 居中指示器
			double y = (1 - v) * 160 - 5;
			Canvas.SetLeft(HsvIndicator, Math.Max(-5, Math.Min(235, x)));
			Canvas.SetTop(HsvIndicator, Math.Max(-5, Math.Min(155, y)));
		}

		private void UpdateHueIndicator(double h)
		{
			double y = (h / 360.0) * 160;
			// Migration note：Avalonia Line 无 Y1/Y2，只有 StartPoint/EndPoint；
			// X 坐标沿用 axaml 中的定义（0 与 20，与 X1/X2 对应）。
			HueIndicator.StartPoint = new Point(HueIndicator.StartPoint.X, y);
			HueIndicator.EndPoint = new Point(HueIndicator.EndPoint.X, y);
		}

		// HSV 方块鼠标交互
		private void HsvCanvas_MouseDown(object sender, global::Avalonia.Input.PointerPressedEventArgs e)
		{
			_isDraggingHsv = true;
			e.Pointer.Capture(HsvCanvas);
			UpdateHsvFromMouse(e.GetPosition(HsvCanvas));
		}

		private void HsvCanvas_MouseUp(object sender, global::Avalonia.Input.PointerReleasedEventArgs e)
		{
			_isDraggingHsv = false;
			if (e.Pointer.Captured == HsvCanvas)
			{
				e.Pointer.Capture(null);
			}
		}

		private void HsvCanvas_MouseMove(object sender, global::Avalonia.Input.PointerEventArgs e)
		{
			if (_isDraggingHsv)
				UpdateHsvFromMouse(e.GetPosition(HsvCanvas));
		}

		private void UpdateHsvFromMouse(Point pos)
		{
			double s = Math.Max(0, Math.Min(1, pos.X / 240));
			double v = Math.Max(0, Math.Min(1, 1 - pos.Y / 160));
			// 取当前色相
			double h = GetHueFromIndicator();
			Color c = HsvToRgbColor(h, s, v);
			_suppressUpdates = true;
			RSlider.Value = c.R;
			GSlider.Value = c.G;
			BSlider.Value = c.B;
			PopupHexBox.Text = "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
			PopupPreviewRect.Fill = new SolidColorBrush(c);
			UpdateHsvCanvas(h, s, v);
			_suppressUpdates = false;
			// 拖 HSV 方块时 _suppressUpdates 阻止了下游事件，手动触发实时落盘。
			ApplyPopupColor();
		}

		// 色相条鼠标交互
		private void HueCanvas_MouseDown(object sender, global::Avalonia.Input.PointerPressedEventArgs e)
		{
			_isDraggingHue = true;
			e.Pointer.Capture(HueCanvas);
			UpdateHueFromMouse(e.GetPosition(HueCanvas));
		}

		private void HueCanvas_MouseUp(object sender, global::Avalonia.Input.PointerReleasedEventArgs e)
		{
			_isDraggingHue = false;
			if (e.Pointer.Captured == HueCanvas)
			{
				e.Pointer.Capture(null);
			}
		}

		private void HueCanvas_MouseMove(object sender, global::Avalonia.Input.PointerEventArgs e)
		{
			if (_isDraggingHue)
				UpdateHueFromMouse(e.GetPosition(HueCanvas));
		}

		private void UpdateHueFromMouse(Point pos)
		{
			double h = Math.Max(0, Math.Min(360, (pos.Y / 160) * 360));
			double s, v;
			GetSvFromIndicator(out s, out v);
			Color c = HsvToRgbColor(h, s, v);
			_suppressUpdates = true;
			RSlider.Value = c.R;
			GSlider.Value = c.G;
			BSlider.Value = c.B;
			PopupHexBox.Text = "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
			PopupPreviewRect.Fill = new SolidColorBrush(c);
			UpdateHsvCanvas(h, s, v);
			UpdateHueIndicator(h);
			_suppressUpdates = false;
			// 拖色相条时 _suppressUpdates 阻止了下游事件，手动触发实时落盘。
			ApplyPopupColor();
		}

		private double GetHueFromIndicator()
		{
			// Migration note：Avalonia Line 无 Y1，用 StartPoint.Y 读取指示器位置。
			return (HueIndicator.StartPoint.Y / 160) * 360;
		}

		private void GetSvFromIndicator(out double s, out double v)
		{
			double x = Canvas.GetLeft(HsvIndicator) + 5;
			double y = Canvas.GetTop(HsvIndicator) + 5;
			s = Math.Max(0, Math.Min(1, x / 240));
			v = Math.Max(0, Math.Min(1, 1 - y / 160));
		}

		private void RgbSlider_ValueChanged(object sender, global::Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
		{
			if (!IsLoaded || _suppressUpdates) return;
			byte r = (byte)Math.Round(RSlider.Value);
			byte g = (byte)Math.Round(GSlider.Value);
			byte b = (byte)Math.Round(BSlider.Value);
			string hex = "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
			_suppressUpdates = true;
			PopupHexBox.Text = hex;
			PopupPreviewRect.Fill = new SolidColorBrush(Color.FromRgb(r, g, b));
			double h, s, v;
			RgbToHsv(r, g, b, out h, out s, out v);
			UpdateHsvCanvas(h, s, v);
			UpdateHueIndicator(h);
			_suppressUpdates = false;
			// 拖 RGB 滑块时 _suppressUpdates 阻止了 PopupHex_TextChanged → UpdatePopupFromHex，
			// 这里手动触发实时落盘（用户拖滑块时主界面立刻跟着变）。
			ApplyPopupColor();
		}

		private void PopupHex_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (_suppressUpdates) return;
			UpdatePopupFromHex(PopupHexBox.Text);
		}

		// HSV ↔ RGB 转换
		private static void RgbToHsv(byte r, byte g, byte b, out double h, out double s, out double v)
		{
			double rd = r / 255.0, gd = g / 255.0, bd = b / 255.0;
			double max = Math.Max(rd, Math.Max(gd, bd));
			double min = Math.Min(rd, Math.Min(gd, bd));
			double delta = max - min;
			v = max;
			s = max == 0 ? 0 : delta / max;
			if (delta == 0)
				h = 0;
			else if (max == rd)
				h = 60 * (((gd - bd) / delta) % 6);
			else if (max == gd)
				h = 60 * (((bd - rd) / delta) + 2);
			else
				h = 60 * (((rd - gd) / delta) + 4);
			if (h < 0) h += 360;
		}

		private static Color HsvToRgbColor(double h, double s, double v)
		{
			double c = v * s;
			double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
			double m = v - c;
			double r, g, b;
			if (h < 60) { r = c; g = x; b = 0; }
			else if (h < 120) { r = x; g = c; b = 0; }
			else if (h < 180) { r = 0; g = c; b = x; }
			else if (h < 240) { r = 0; g = x; b = c; }
			else if (h < 300) { r = x; g = 0; b = c; }
			else { r = c; g = 0; b = x; }
			return Color.FromRgb((byte)Math.Round((r + m) * 255), (byte)Math.Round((g + m) * 255), (byte)Math.Round((b + m) * 255));
		}

		#endregion

		/// <summary>列表内 hex 输入框改色（经 HexValue setter 编辑回调，见 CustomColorItem）。
		/// Migration note（2026-09-05，"列表里直接改 hex 不生效"）：WPF 原版 XAML 挂 TextChanged，
		/// 迁移丢失——TwoWay 绑定只写了 item.HexValue（列表预览变了），_workingCopy/主题/落盘
		/// 全不走，关窗即丢。修复不补 TextChanged 而走 setter 回调：绑定初始化（source→target）
		/// 只读 getter 不触发回调；程序化赋值（Reset/Random/Import）经 _suppressUpdates 同步抑制，
		/// 无"绑定回声"污染（TextChanged 方案下程序化改值→绑定刷 Text→TextChanged 回写，
		/// 会把 Reset 刚清掉的自定义项又写回去，表现为 Reset 失效）。
		/// 走 150ms 防抖（与 Popup 改色一致）：原死代码实现是每击键一次全量 ApplyAndRefresh。 </summary>
		private void OnItemHexEdited(CustomColorItem item)
		{
			if (_suppressUpdates || item == null)
			{
				return;
			}
			string hex = item.HexValue == null ? "" : item.HexValue.Trim();
			if (string.IsNullOrEmpty(hex)) return;
			if (!hex.StartsWith("#")) hex = "#" + hex;
			try
			{
				ColorConverter.ConvertFromString(hex);
			}
			catch
			{
				return; // 输入到一半（如 "#28"）不应用，等输完
			}
			item.HexValue = hex;
			item.IsCustomized = true;
			_workingCopy[item.Key] = hex;
			// 与 Popup 改色一致的防抖调度：连击期间轻量更新，停顿后跑一次重活
			_hasPendingApply = true;
			_applyDebounceTimer.Stop();
			_applyDebounceTimer.Start();
		}

		private void ResetItem_Click(object sender, RoutedEventArgs e)
		{
			if (sender is Button btn && btn.Tag is CustomColorItem item)
			{
				_workingCopy.Remove(item.Key);
				// 程序化赋值经 _suppressUpdates 抑制编辑回调（否则回调把刚删的 key 又写回，
				// Reset 表现为"按了没反应"）
				_suppressUpdates = true;
				item.HexValue = GetCurrentColorHex(item.Key);
				item.IsCustomized = false;
				_suppressUpdates = false;
				ApplyAndRefresh();
			}
		}

		private void ResetAll_Click(object sender, RoutedEventArgs e)
	{
		_workingCopy.Clear();
		// 程序化赋值经 _suppressUpdates 抑制编辑回调（同 ResetItem_Click）
		_suppressUpdates = true;
		foreach (CustomColorItem item in _items)
		{
			item.HexValue = GetCurrentColorHex(item.Key);
			item.IsCustomized = false;
		}
		_suppressUpdates = false;
		ApplyAndRefresh();
	}

	#region 导入/导出颜色配置（v2.1.3 新增）

	/// <summary>JSON 配置文件的 schema 标识。导入时校验，未来 schema 升级时向后兼容判断用。</summary>
	private const string CustomColorsSchema = "ForkPlus.CustomColors/v1";

	/// <summary>导出当前配色（_workingCopy 中所有自定义项）为 JSON 文件。
	/// JSON 格式：
	/// {
	///   "schema": "ForkPlus.CustomColors/v1",
	///   "theme": "Dark",          // 导出时的主题（仅参考，导入时不强制匹配）
	///   "exportedAt": "2026-07-19T10:00:00Z",
	///   "customColors": {
	///     "BackgroundColor": "#282A36",
	///     "Diff.AddedColor": "#50FA7B",
	///     ...
	///   }
	/// }
	/// 仅导出用户实际自定义过的颜色项（_workingCopy 中的项），未自定义的预设色不导出。
	/// 这样导出文件简洁，且导入方按需覆盖。</summary>
	private void ExportColors_Click(object sender, RoutedEventArgs e)
	{
		string lang = ForkPlusSettings.Default.UiLanguage;
		// 关闭 Popup 避免遮挡 SaveFileDialog
		ColorPickerPopup.IsOpen = false;

		if (_workingCopy == null || _workingCopy.Count == 0)
		{
			new MessageBoxWindow(
				PreferencesLocalization.Translate("Export Colors", lang),
				PreferencesLocalization.Translate("No custom colors to export. Customize some colors first.", lang),
				"OK",
				showCancelButton: false).ShowDialog();
			return;
		}

		// Migration note：WPF Microsoft.Win32.SaveFileDialog 在 Avalonia 无对应（StorageProvider 为异步 API，
		// 会把整条同步调用链异步化）；改用仓库内同步 Win32 封装 OpenDialog.SelectFileSaveLocation
		// （仅支持单过滤器 *.json；非 Windows 平台返回 false 视为取消）。
		string defaultFileName = "ForkPlus-Colors-" + ForkPlusSettings.Default.Theme.SkinName() + ".json";
		if (!OpenDialog.SelectFileSaveLocation(this, PreferencesLocalization.Translate("Export Colors", lang), null, defaultFileName, out string exportPath))
		{
			return;
		}
		// 对应 WPF SaveFileDialog.AddExtension = true：用户没敲扩展名时补 .json。
		if (!exportPath.EndsWith(".json", StringComparison.CurrentCultureIgnoreCase))
		{
			exportPath += ".json";
		}

		try
		{
			// 构建导出 JSON（仅含 _workingCopy 中实际自定义项）
			Dictionary<string, string> exportColors = new Dictionary<string, string>(_workingCopy);
			JObject root = new JObject
			{
				["schema"] = CustomColorsSchema,
				["theme"] = ForkPlusSettings.Default.Theme.ToString(),
				["exportedAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
				["customColors"] = JObject.FromObject(exportColors),
			};
			string json = root.ToString(Formatting.Indented);
			File.WriteAllText(exportPath, json);

			new MessageBoxWindow(
				PreferencesLocalization.Translate("Export Colors", lang),
				string.Format(PreferencesLocalization.Translate("Exported {0} custom colors to:\n{1}", lang),
				exportColors.Count, exportPath),
				"OK",
				showCancelButton: false).ShowDialog();
		}
		catch (Exception ex)
		{
			new MessageBoxWindow(
				PreferencesLocalization.Translate("Export Colors", lang),
				PreferencesLocalization.Translate("Export failed: ", lang) + ex.Message,
				"OK",
				showCancelButton: false,
				showWarningIcon: true).ShowDialog();
		}
	}

	/// <summary>从 JSON 文件导入配色。导入前严格校验：
	/// 1. 必须是合法 JSON
	/// 2. 顶层必须是对象，含 customColors 字段（对象）
	/// 3. schema 字段如果存在，必须是 "ForkPlus.CustomColors/v1"
	/// 4. customColors 中每个 key 必须在 _editableColorKeys 白名单内
	/// 5. customColors 中每个 value 必须是合法 hex 颜色（#RRGGBB 或 #AARRGGBB 或 RRGGBB）
	/// 校验失败时弹出 MessageBox 提示具体错误，不修改任何当前配置。</summary>
	private void ImportColors_Click(object sender, RoutedEventArgs e)
	{
		string lang = ForkPlusSettings.Default.UiLanguage;
		// 关闭 Popup 避免遮挡 OpenFileDialog
		ColorPickerPopup.IsOpen = false;

		// Migration note：WPF Microsoft.Win32.OpenFileDialog 在 Avalonia 无对应；
		// 改用仓库内同步 Win32 封装 OpenDialog.SelectFile（单过滤器 *.json，
		// 非 Windows 平台返回 false 视为取消，CheckFileExists 由 Win32 FOS_FILEMUSTEXIST 承担）。
		if (!OpenDialog.SelectFile(this, PreferencesLocalization.Translate("Import Colors", lang), null, "JSON", "*.json", out string importPath))
		{
			return;
		}

		string jsonText;
		try
		{
			jsonText = File.ReadAllText(importPath);
		}
		catch (Exception ex)
		{
			new MessageBoxWindow(
				PreferencesLocalization.Translate("Import Colors", lang),
				PreferencesLocalization.Translate("Cannot read file: ", lang) + ex.Message,
				"OK",
				showCancelButton: false,
				showWarningIcon: true).ShowDialog();
			return;
		}

		// 校验 1: 必须是合法 JSON
		JObject root;
		try
		{
			JToken parsed = JToken.Parse(jsonText);
			if (parsed.Type != JTokenType.Object)
			{
				new MessageBoxWindow(
					PreferencesLocalization.Translate("Import Colors", lang),
					PreferencesLocalization.Translate("Invalid format: JSON root must be an object.", lang),
					"OK",
					showCancelButton: false,
					showWarningIcon: true).ShowDialog();
				return;
			}
			root = (JObject)parsed;
		}
		catch (JsonReaderException ex)
		{
			new MessageBoxWindow(
				PreferencesLocalization.Translate("Import Colors", lang),
				PreferencesLocalization.Translate("Invalid JSON: ", lang) + ex.Message,
				"OK",
				showCancelButton: false,
				showWarningIcon: true).ShowDialog();
			return;
		}

		// 校验 2: schema 字段如果存在，必须匹配
		JToken schemaToken = root["schema"];
		if (schemaToken != null)
		{
			if (schemaToken.Type != JTokenType.String || (string)schemaToken != CustomColorsSchema)
			{
				new MessageBoxWindow(
					PreferencesLocalization.Translate("Import Colors", lang),
					string.Format(PreferencesLocalization.Translate("Unsupported schema. Expected '{0}'.", lang), CustomColorsSchema),
					"OK",
					showCancelButton: false,
					showWarningIcon: true).ShowDialog();
				return;
			}
		}

		// 校验 3: customColors 字段必须存在且是对象
		JToken colorsToken = root["customColors"];
		if (colorsToken == null)
		{
			new MessageBoxWindow(
				PreferencesLocalization.Translate("Import Colors", lang),
				PreferencesLocalization.Translate("Invalid format: missing 'customColors' field.", lang),
				"OK",
				showCancelButton: false,
				showWarningIcon: true).ShowDialog();
			return;
		}
		if (colorsToken.Type != JTokenType.Object)
		{
			new MessageBoxWindow(
				PreferencesLocalization.Translate("Import Colors", lang),
				PreferencesLocalization.Translate("Invalid format: 'customColors' must be an object.", lang),
				"OK",
				showCancelButton: false,
				showWarningIcon: true).ShowDialog();
			return;
		}

		// 校验 4 & 5: 每个 key 在白名单内 + 每个 value 是合法 hex
		HashSet<string> validKeys = new HashSet<string>(_editableColorKeys);
		Dictionary<string, string> imported = new Dictionary<string, string>();
		int errorCount = 0;
		System.Text.StringBuilder errorBuf = new System.Text.StringBuilder();
		const int maxErrorsShown = 10;

		JObject colorsObj = (JObject)colorsToken;
		foreach (KeyValuePair<string, JToken> kv in colorsObj)
		{
			string key = kv.Key;
			JToken valToken = kv.Value;
			// value 必须是字符串
			if (valToken.Type != JTokenType.String)
			{
				errorCount++;
				if (errorCount <= maxErrorsShown)
					errorBuf.AppendLine(string.Format(PreferencesLocalization.Translate("  - '{0}': value must be a string", lang), key));
				continue;
			}
			string hex = (string)valToken;
			// key 白名单
			if (!validKeys.Contains(key))
			{
				errorCount++;
				if (errorCount <= maxErrorsShown)
					errorBuf.AppendLine(string.Format(PreferencesLocalization.Translate("  - '{0}': unknown color key", lang), key));
				continue;
			}
			// value 合法 hex
			if (!IsValidHexColor(hex))
			{
				errorCount++;
				if (errorCount <= maxErrorsShown)
					errorBuf.AppendLine(string.Format(PreferencesLocalization.Translate("  - '{0}': invalid hex color '{1}'", lang), key, hex));
				continue;
			}
			// 规范化：统一加 # 前缀
			if (!hex.StartsWith("#")) hex = "#" + hex;
			imported[key] = hex;
		}

		if (errorCount > 0)
		{
			string summary;
			if (errorCount > maxErrorsShown)
				summary = string.Format(PreferencesLocalization.Translate("Import aborted: {0} errors found (showing first {1}):\n", lang),
					errorCount, maxErrorsShown);
			else
				summary = string.Format(PreferencesLocalization.Translate("Import aborted: {0} errors found:\n", lang), errorCount);
			new MessageBoxWindow(
				PreferencesLocalization.Translate("Import Colors", lang),
				summary + errorBuf.ToString(),
				"OK",
				showCancelButton: false,
				showWarningIcon: true).ShowDialog();
			return;
		}

		if (imported.Count == 0)
		{
			new MessageBoxWindow(
				PreferencesLocalization.Translate("Import Colors", lang),
				PreferencesLocalization.Translate("No valid color entries found in file.", lang),
				"OK",
				showCancelButton: false,
				showWarningIcon: true).ShowDialog();
			return;
		}

		// 校验通过，应用导入的配色：合并到 _workingCopy 并刷新 UI + 落盘
		// 注意：导入是"覆盖式合并"——导入文件中出现的 key 覆盖当前 _workingCopy 中的值，
		// 导入文件中未出现的 key 保持当前值不变。这样用户可以只导入部分颜色覆盖。
		foreach (KeyValuePair<string, string> kv in imported)
			_workingCopy[kv.Key] = kv.Value;

		// 同步 UI：更新每项的 HexValue/IsCustomized（程序化赋值经 _suppressUpdates 抑制编辑回调）
		_suppressUpdates = true;
		foreach (CustomColorItem item in _items)
		{
			if (_workingCopy.TryGetValue(item.Key, out string hex))
			{
				item.HexValue = hex;
				item.IsCustomized = true;
			}
		}
		_suppressUpdates = false;

		ApplyAndRefresh();

		new MessageBoxWindow(
			PreferencesLocalization.Translate("Import Colors", lang),
			string.Format(PreferencesLocalization.Translate("Imported {0} colors successfully.", lang), imported.Count),
			"OK",
			showCancelButton: false).ShowDialog();
	}

	/// <summary>校验 hex 颜色字符串是否合法。接受以下格式：
	/// #RRGGBB / #AARRGGBB / RRGGBB / AARRGGBB（不区分大小写）。
	/// 用 ColorConverter.ConvertFromString 试解析，失败即非法。</summary>
	private static bool IsValidHexColor(string hex)
	{
		if (string.IsNullOrWhiteSpace(hex)) return false;
		try
		{
			string normalized = hex.Trim();
			if (!normalized.StartsWith("#")) normalized = "#" + normalized;
			Color c = (Color)ColorConverter.ConvertFromString(normalized);
			return true;
		}
		catch
		{
			return false;
		}
	}

	#endregion

	/// <summary>随机生成一套搭配合理的配色并应用到所有可编辑颜色。
	/// 算法：随机一个主色相 H，按当前主题基底（light/dark）派生整套配色——
	/// 背景用极低饱和度 + 高/低明度的近中性色，面板/边框用稍深的同色调，
	/// 文字/前景用对比色（light 主题用深色文字，dark 主题用浅色文字），
	/// accent 用主色相满饱和，diff added 在绿区(90-150°)、removed 在红区(345-15°)内随机，
	/// 语法高亮用主色相邻近的几个色相做区分。保证整体色调统一、可读。</summary>
	private void RandomPalette_Click(object sender, RoutedEventArgs e)
	{
		bool isDark = ForkPlusSettings.Default.Theme.IsDarkBase();
		var rand = new Random();
		// 主色相 0-360，避免取到极端红/绿区（留给 diff 用）
		double baseHue = rand.NextDouble() * 360.0;
		// 辅助色相：主色相对侧（互补色附近，加随机偏移）
		double accentHue = (baseHue + 180.0 + (rand.NextDouble() * 60.0 - 30.0)) % 360.0;

		// HSV→Color 辅助
		Func<double, double, double, byte, Color> hsv = (h, s, v, a) =>
		{
			Color c = HsvToRgbColor(h, s, v);
			return Color.FromArgb(a, c.R, c.G, c.B);
		};

		// 按基底明暗派生背景/文字/accent
		Color bgColor, panelBgColor, secondaryBgColor, borderColor, labelColor, fgColor, secondaryLabelColor, accentColor, accentSecondaryColor, referenceColor, iconColor;
		if (isDark)
		{
			// dark：背景低明度近黑带轻微主色调，文字浅色
			bgColor = hsv(baseHue, 0.15, 0.10, 255);
			panelBgColor = hsv(baseHue, 0.18, 0.14, 255);
			secondaryBgColor = hsv(baseHue, 0.20, 0.18, 255);
			borderColor = hsv(baseHue, 0.15, 0.28, 255);
			labelColor = hsv(baseHue, 0.10, 0.92, 255);
			fgColor = hsv(baseHue, 0.08, 0.96, 255);
			secondaryLabelColor = hsv(baseHue, 0.12, 0.65, 255);
			accentColor = hsv(baseHue, 0.70, 0.95, 255);
			accentSecondaryColor = hsv(accentHue, 0.65, 0.90, 255);
			referenceColor = hsv(baseHue, 0.55, 0.80, 255);
			iconColor = hsv(baseHue, 0.30, 0.85, 255);
		}
		else
		{
			// light：背景高明度近白带轻微主色调，文字深色
			bgColor = hsv(baseHue, 0.10, 0.98, 255);
			panelBgColor = hsv(baseHue, 0.12, 0.95, 255);
			secondaryBgColor = hsv(baseHue, 0.14, 0.90, 255);
			borderColor = hsv(baseHue, 0.15, 0.80, 255);
			labelColor = hsv(baseHue, 0.30, 0.20, 255);
			fgColor = hsv(baseHue, 0.25, 0.12, 255);
			secondaryLabelColor = hsv(baseHue, 0.20, 0.45, 255);
			accentColor = hsv(baseHue, 0.75, 0.60, 255);
			accentSecondaryColor = hsv(accentHue, 0.70, 0.55, 255);
			referenceColor = hsv(baseHue, 0.60, 0.50, 255);
			iconColor = hsv(baseHue, 0.40, 0.40, 255);
		}
		// diff：保持绿/红语义色，但色相在绿区(90-150)/红区(345-15)内随机偏移，
	// 饱和度/明度也轻微随机，避免每次随机配色 Diff 颜色都完全相同（用户反馈"不动"）。
	double greenHue = 120.0 + (rand.NextDouble() * 60.0 - 30.0);          // 90-150
	double redHue = (360.0 + (rand.NextDouble() * 30.0 - 15.0)) % 360.0;  // 345-15
	Color diffAdded, diffRemoved, diffAddBg, diffRemoveBg, diffExactAdd, diffExactRemove;
	if (isDark)
	{
		diffAdded = hsv(greenHue, 0.40 + rand.NextDouble() * 0.15, 0.35 + rand.NextDouble() * 0.15, 255);
		diffRemoved = hsv(redHue, 0.40 + rand.NextDouble() * 0.15, 0.35 + rand.NextDouble() * 0.15, 255);
		// 行底色：比块色更低饱和度，接近背景
		diffAddBg = hsv(greenHue, 0.20 + rand.NextDouble() * 0.15, 0.15 + rand.NextDouble() * 0.15, 255);
		diffRemoveBg = hsv(redHue, 0.20 + rand.NextDouble() * 0.15, 0.15 + rand.NextDouble() * 0.15, 255);
		// 行内字色：比块色更鲜，作高亮
		diffExactAdd = hsv(greenHue, 0.65 + rand.NextDouble() * 0.20, 0.55 + rand.NextDouble() * 0.20, 255);
		diffExactRemove = hsv(redHue, 0.65 + rand.NextDouble() * 0.20, 0.55 + rand.NextDouble() * 0.20, 255);
	}
	else
	{
		diffAdded = hsv(greenHue, 0.35 + rand.NextDouble() * 0.15, 0.85 + rand.NextDouble() * 0.10, 255);
		diffRemoved = hsv(redHue, 0.35 + rand.NextDouble() * 0.15, 0.87 + rand.NextDouble() * 0.10, 255);
		diffAddBg = hsv(greenHue, 0.10 + rand.NextDouble() * 0.15, 0.90 + rand.NextDouble() * 0.08, 255);
		diffRemoveBg = hsv(redHue, 0.10 + rand.NextDouble() * 0.15, 0.90 + rand.NextDouble() * 0.08, 255);
		diffExactAdd = hsv(greenHue, 0.65 + rand.NextDouble() * 0.20, 0.30 + rand.NextDouble() * 0.15, 255);
		diffExactRemove = hsv(redHue, 0.65 + rand.NextDouble() * 0.20, 0.30 + rand.NextDouble() * 0.15, 255);
	}
	// 代码编辑器：背景跟随主背景，前景跟随主文字
	Color codeBg = isDark ? hsv(baseHue, 0.18, 0.12, 255) : hsv(baseHue, 0.10, 0.99, 255);
	Color codeFg = isDark ? hsv(baseHue, 0.08, 0.92, 255) : hsv(baseHue, 0.25, 0.15, 255);
	// 语法高亮：围绕主色相派生 4 个 token 色，避开 diff 红(0°)/绿(120°)区
	Color syntaxComment = isDark ? hsv(baseHue, 0.20, 0.55, 255) : hsv(baseHue, 0.35, 0.45, 255);
	Color syntaxString = hsv((baseHue + 30.0) % 360.0, 0.55, isDark ? 0.85 : 0.40, 255);
	Color syntaxKeyword = hsv((baseHue + 180.0) % 360.0, 0.70, isDark ? 0.80 : 0.45, 255);
	Color syntaxNumber = hsv((baseHue + 90.0) % 360.0, 0.55, isDark ? 0.75 : 0.40, 255);
	// 行号：弱化文字色，分隔线极淡
	Color lineNumberFg = isDark ? hsv(baseHue, 0.10, 0.45, 255) : hsv(baseHue, 0.20, 0.55, 255);
	Color lineNumberSep = isDark ? hsv(baseHue, 0.10, 0.25, 255) : hsv(baseHue, 0.10, 0.80, 255);
	// 选区：复用强调色作边框，带半透明的强调色变体作背景
	Color chunkBorder = accentColor;
	Color chunkBg = hsv(baseHue, 0.40, isDark ? 0.30 : 0.85, 60);
	// 窗口/标题栏背景
	Color windowBg = bgColor;
	Color titleBarBg = isDark ? hsv(baseHue, 0.20, 0.16, 255) : hsv(baseHue, 0.14, 0.96, 255);

	// 写入工作副本
	void Set(string key, Color c)
	{
		_workingCopy[key] = "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
	}
	Set("BackgroundColor", bgColor);
	Set("SecondaryBackgroundColor", secondaryBgColor);
	Set("PanelBackgroundColor", panelBgColor);
	Set("BorderColor", borderColor);
	Set("TileBorderColor", borderColor);
	Set("LabelColor", labelColor);
	Set("ForegroundColor", fgColor);
	Set("SecondaryLabelColor", secondaryLabelColor);
	Set("AccentColor", accentColor);
	Set("AccentSecondaryColor", accentSecondaryColor);
	Set("ReferenceColor", referenceColor);
	Set("IconColor", iconColor);
	Set("Diff.AddedColor", diffAdded);
	Set("Diff.RemovedColor", diffRemoved);
	// 补齐 Diff 细粒度色：行底色 + 行内字色（之前遗漏）
	Set("Diff.AddColor", diffAddBg);
	Set("Diff.RemoveColor", diffRemoveBg);
	Set("Diff.ExactAddColor", diffExactAdd);
	Set("Diff.ExactRemoveColor", diffExactRemove);
	Set("CodeEditor.BackgroundColor", codeBg);
	Set("CodeEditor.ForegroundColor", codeFg);
	// 补齐语法高亮 4 个 token 色（之前遗漏）
	Set("Syntax.CommentColor", syntaxComment);
	Set("Syntax.StringColor", syntaxString);
	Set("Syntax.KeywordColor", syntaxKeyword);
	Set("Syntax.NumberColor", syntaxNumber);
	// 补齐行号 + 选区色（之前遗漏）
	Set("LineNumber.ForegroundColor", lineNumberFg);
	Set("LineNumber.SeparatorColor", lineNumberSep);
	Set("ChunkSelection.BorderColor", chunkBorder);
	Set("ChunkSelection.BackgroundColor", chunkBg);
	Set("Window.BackgroundColor", windowBg);
	Set("Window.TitleBar.BackgroundColor", titleBarBg);

		// 更新 UI（程序化赋值经 _suppressUpdates 抑制编辑回调）
	_suppressUpdates = true;
		foreach (CustomColorItem item in _items)
		{
			if (_workingCopy.TryGetValue(item.Key, out string hex))
			{
				item.HexValue = hex;
				item.IsCustomized = true;
			}
		}
		_suppressUpdates = false;
		ApplyAndRefresh();
	}

		private void ApplyAndRefresh()
	{
		ForkPlusSettings.Default.CustomColors = new Dictionary<string, string>(_workingCopy);
		// 首次启用自定义颜色时 UseCustomColors 仍是 false，App.ApplyCustomColors 会走早退分支，
		// 导致主窗口无法实时预览。这里在 _workingCopy 非空时置 true，
		// 让 ApplyCustomColors 走正常分支 merge ResourceDictionary + raise ApplicationThemeChanged。
		if (_workingCopy.Count > 0)
		{
			ForkPlusSettings.Default.UseCustomColors = true;
		}
		// 关键：merge ResourceDictionary + raise ApplicationThemeChanged，
		// 否则 Diff/热力图/行号边距等 20+ 订阅控件不会重绘，主界面不会实时生效。
		App.ApplyCustomColors();
		foreach (CustomColorItem item in _items)
			item.RefreshPreview();
		// 立即落盘到 settings.json，避免崩溃/关窗丢失。换色已实时落盘，故无需 OK/Cancel。
		try { ForkPlusSettings.Default.Save(); } catch { /* 持久化失败不阻断编辑 */ }
	}

		/// <summary>颜色项 ViewModel。</summary>
		public class CustomColorItem : INotifyPropertyChanged
		{
			public string Key { get; }
			public string DisplayName { get; }
			public string ResetLabel { get; }

			private string _hexValue;
		public string HexValue
		{
			get { return _hexValue; }
			set
			{
				// 同值短路：正常事件语义外，还断开 OnItemHexEdited 回调里的规范化回写重入
				if (_hexValue == value)
				{
					return;
				}
				_hexValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(PreviewBrush));
				// 编辑回调（对话框注入）：用户经列表 hex 框改色（TwoWay 绑定 target→source 推送）
				// 时走防抖应用。绑定初始化（source→target）只读 getter 不触发；程序化赋值
				//（Reset/Random/Import/popup 同步）由对话框经 _suppressUpdates 抑制。
				HexValueEdited?.Invoke(this);
			}
		}

		/// <summary>对话框注入的编辑回调（2026-09-05，"列表里直接改 hex 不生效"，见 OnItemHexEdited）。</summary>
		internal Action<CustomColorItem> HexValueEdited;

			private bool _isCustomized;
			public bool IsCustomized
			{
				get { return _isCustomized; }
				set { _isCustomized = value; OnPropertyChanged(); }
			}

			public Brush PreviewBrush
			{
				get
				{
					try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(HexValue)); }
					// Migration note：Avalonia Brushes.White 返回 IImmutableSolidColorBrush，不能隐式转 Brush，
					// 用 new SolidColorBrush(Colors.White) 等价替换。
					catch { return new SolidColorBrush(Colors.White); }
				}
			}

			public CustomColorItem(string key, string displayName, string hexValue, bool isCustomized, string resetLabel)
			{
				Key = key;
				DisplayName = displayName;
				_hexValue = hexValue;
				_isCustomized = isCustomized;
				ResetLabel = resetLabel;
			}

			public void RefreshPreview()
			{
				OnPropertyChanged(nameof(PreviewBrush));
			}

			public event PropertyChangedEventHandler PropertyChanged;
			private void OnPropertyChanged([CallerMemberName] string name = null)
				=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		}
	}
}
