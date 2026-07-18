using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>自定义颜色编辑对话框。列出可自定义的核心颜色，支持 hex 输入和 HSV 颜色选择器。
	/// 改动即时应用到 UI（通过 App.ApplyCustomColors），确定后保存到 settings.json。</summary>
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
	// 对话框打开时的原始状态快照，Cancel 时还原（编辑过程已实时应用到 UI）。
	private Dictionary<string, string> _originalCustomColors;
	private bool _originalUseCustomColors;
	private CustomColorItem _popupEditingItem;
	private bool _suppressUpdates;
	private bool _isDraggingHsv;
	private bool _isDraggingHue;

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
		}

		private void Localize()
		{
			string lang = ForkPlusSettings.Default.UiLanguage;
			Title = PreferencesLocalization.Translate("Custom Colors", lang);
			HeaderTextBlock.Text = PreferencesLocalization.Translate("Custom Colors", lang) +
				" (" + PreferencesLocalization.Translate(ForkPlusSettings.Default.Theme.SkinName(), lang) + ")";
			ResetAllButton.Content = PreferencesLocalization.Translate("Reset All", lang);
		RandomPaletteButton.Content = PreferencesLocalization.Translate("Random Palette", lang);
		OkButton.Content = PreferencesLocalization.Translate("OK", lang);
			CancelButton.Content = PreferencesLocalization.Translate("Cancel", lang);
			PopupTitleText.Text = PreferencesLocalization.Translate("Color Picker", lang);
			SwatchLabelText.Text = PreferencesLocalization.Translate("Presets", lang);
			PopupOkButton.Content = PreferencesLocalization.Translate("OK", lang);
			PopupCancelButton.Content = PreferencesLocalization.Translate("Cancel", lang);
			// DataTemplate 里的 "Reset" 按钮文字在 LoadItems 后通过遍历设置
		}

		/// <summary>加载颜色列表。每项显示当前生效值（自定义覆盖或预设原色）。</summary>
		private void LoadItems()
	{
		// 保存打开对话框前的原始状态，供 Cancel 还原。
		_originalCustomColors = ForkPlusSettings.Default.CustomColors != null
			? new Dictionary<string, string>(ForkPlusSettings.Default.CustomColors)
			: new Dictionary<string, string>();
		_originalUseCustomColors = ForkPlusSettings.Default.UseCustomColors;
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
			ColorListControl.ItemsSource = _items;
		}

		/// <summary>从当前 Application.Resources 取某个 Color key 的 hex 值（预设原色）。</summary>
		private string GetCurrentColorHex(string key)
		{
			try
			{
				object obj = Application.Current.Resources[key];
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
					BorderBrush = (Brush)Application.Current.Resources["BorderBrush"],
					BorderThickness = new Thickness(1),
					Cursor = Cursors.Hand,
					Tag = hex,
				};
				try { swatch.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
				catch { continue; }
				swatch.MouseLeftButtonUp += Swatch_Click;
				SwatchPanel.Children.Add(swatch);
			}
		}

		#region HSV 调色盘

		private void Swatch_Click(object sender, MouseButtonEventArgs e)
		{
			if (sender is Border b && b.Tag is string hex)
			{
				_suppressUpdates = true;
				PopupHexBox.Text = hex;
				_suppressUpdates = false;
				UpdatePopupFromHex(hex);
			}
		}

		/// <summary>颜色预览块点击 → 打开颜色选择 Popup。</summary>
		private void ColorPreview_Click(object sender, MouseButtonEventArgs e)
		{
			if (sender is FrameworkElement fe && fe.Tag is CustomColorItem item)
			{
				_popupEditingItem = item;
				UpdatePopupFromHex(item.HexValue);
				ColorPickerPopup.IsOpen = true;
			}
		}

		/// <summary>从 hex 值更新整个 Popup 状态（HSV 方块、色相条、RGB 滑块、预览）。</summary>
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
			}
			catch { }
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
			HueIndicator.Y1 = y;
			HueIndicator.Y2 = y;
		}

		// HSV 方块鼠标交互
		private void HsvCanvas_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_isDraggingHsv = true;
			HsvCanvas.CaptureMouse();
			UpdateHsvFromMouse(e.GetPosition(HsvCanvas));
		}

		private void HsvCanvas_MouseUp(object sender, MouseButtonEventArgs e)
		{
			_isDraggingHsv = false;
			HsvCanvas.ReleaseMouseCapture();
		}

		private void HsvCanvas_MouseMove(object sender, MouseEventArgs e)
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
		}

		// 色相条鼠标交互
		private void HueCanvas_MouseDown(object sender, MouseButtonEventArgs e)
		{
			_isDraggingHue = true;
			HueCanvas.CaptureMouse();
			UpdateHueFromMouse(e.GetPosition(HueCanvas));
		}

		private void HueCanvas_MouseUp(object sender, MouseButtonEventArgs e)
		{
			_isDraggingHue = false;
			HueCanvas.ReleaseMouseCapture();
		}

		private void HueCanvas_MouseMove(object sender, MouseEventArgs e)
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
		}

		private double GetHueFromIndicator()
		{
			return (HueIndicator.Y1 / 160) * 360;
		}

		private void GetSvFromIndicator(out double s, out double v)
		{
			double x = Canvas.GetLeft(HsvIndicator) + 5;
			double y = Canvas.GetTop(HsvIndicator) + 5;
			s = Math.Max(0, Math.Min(1, x / 240));
			v = Math.Max(0, Math.Min(1, 1 - y / 160));
		}

		private void RgbSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
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

		private void PopupOk_Click(object sender, RoutedEventArgs e)
		{
			if (_popupEditingItem != null)
			{
				string hex = PopupHexBox.Text.Trim();
				if (!hex.StartsWith("#")) hex = "#" + hex;
				try { ColorConverter.ConvertFromString(hex); }
				catch { return; }
				_popupEditingItem.HexValue = hex;
				_popupEditingItem.IsCustomized = true;
				_workingCopy[_popupEditingItem.Key] = hex;
				ApplyAndRefresh();
			}
			ColorPickerPopup.IsOpen = false;
			_popupEditingItem = null;
		}

		private void PopupCancel_Click(object sender, RoutedEventArgs e)
		{
			ColorPickerPopup.IsOpen = false;
			_popupEditingItem = null;
		}

		private void HexTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (sender is TextBox tb && tb.Tag is CustomColorItem item)
			{
				string hex = tb.Text.Trim();
				if (string.IsNullOrEmpty(hex)) return;
				if (!hex.StartsWith("#")) hex = "#" + hex;
				try
				{
					ColorConverter.ConvertFromString(hex);
					item.HexValue = hex;
					item.IsCustomized = true;
					_workingCopy[item.Key] = hex;
					ApplyAndRefresh();
				}
				catch { }
			}
		}

		private void ResetItem_Click(object sender, RoutedEventArgs e)
		{
			if (sender is Button btn && btn.Tag is CustomColorItem item)
			{
				_workingCopy.Remove(item.Key);
				item.HexValue = GetCurrentColorHex(item.Key);
				item.IsCustomized = false;
				ApplyAndRefresh();
			}
		}

		private void ResetAll_Click(object sender, RoutedEventArgs e)
	{
		_workingCopy.Clear();
		foreach (CustomColorItem item in _items)
		{
			item.HexValue = GetCurrentColorHex(item.Key);
			item.IsCustomized = false;
		}
		ApplyAndRefresh();
	}

	/// <summary>随机生成一套搭配合理的配色并应用到所有可编辑颜色。
	/// 算法：随机一个主色相 H，按当前主题基底（light/dark）派生整套配色——
	/// 背景用极低饱和度 + 高/低明度的近中性色，面板/边框用稍深的同色调，
	/// 文字/前景用对比色（light 主题用深色文字，dark 主题用浅色文字），
	/// accent 用主色相满饱和，diff added 取绿区(120°)、removed 取红区(0°)，
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
		// diff：固定绿/红区，按基底调整明度饱和度
	Color diffAdded, diffRemoved, diffAddBg, diffRemoveBg, diffExactAdd, diffExactRemove;
	if (isDark)
	{
		diffAdded = hsv(120.0, 0.45, 0.40, 255);
		diffRemoved = hsv(0.0, 0.45, 0.40, 255);
		// 行底色：比块色更低饱和度，接近背景
		diffAddBg = hsv(120.0, 0.25, 0.20, 255);
		diffRemoveBg = hsv(0.0, 0.25, 0.20, 255);
		// 行内字色：比块色更鲜，作高亮
		diffExactAdd = hsv(120.0, 0.75, 0.65, 255);
		diffExactRemove = hsv(0.0, 0.75, 0.65, 255);
	}
	else
	{
		diffAdded = hsv(120.0, 0.40, 0.90, 255);
		diffRemoved = hsv(0.0, 0.40, 0.92, 255);
		diffAddBg = hsv(120.0, 0.15, 0.95, 255);
		diffRemoveBg = hsv(0.0, 0.15, 0.95, 255);
		diffExactAdd = hsv(120.0, 0.75, 0.35, 255);
		diffExactRemove = hsv(0.0, 0.75, 0.35, 255);
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

		// 更新 UI
		foreach (CustomColorItem item in _items)
		{
			if (_workingCopy.TryGetValue(item.Key, out string hex))
			{
				item.HexValue = hex;
				item.IsCustomized = true;
			}
		}
		ApplyAndRefresh();
	}

		private void ApplyAndRefresh()
	{
		ForkPlusSettings.Default.CustomColors = new Dictionary<string, string>(_workingCopy);
		// 首次启用自定义颜色时 UseCustomColors 仍是 false，App.ApplyCustomColors 会走早退分支，
		// 导致编辑过程中主窗口无法实时预览。这里在 _workingCopy 非空时临时置 true，
		// 让 ApplyCustomColors 走正常分支 merge ResourceDictionary + raise ApplicationThemeChanged。
		// Cancel_Click 会按 _originalUseCustomColors 还原，OK 时会再设 true。
		bool prevUseCustom = ForkPlusSettings.Default.UseCustomColors;
		if (_workingCopy.Count > 0)
		{
			ForkPlusSettings.Default.UseCustomColors = true;
		}
		App.ApplyCustomColors();
		foreach (CustomColorItem item in _items)
			item.RefreshPreview();
	}

	private void Ok_Click(object sender, RoutedEventArgs e)
	{
		// 确认编辑后启用自定义颜色覆盖（有自定义项时），并立即持久化避免崩溃丢失。
		if (_workingCopy.Count > 0)
		{
			ForkPlusSettings.Default.UseCustomColors = true;
		}
		// 关键修复：必须调用 ApplyCustomColors 把 _workingCopy merge 到
		// Application.Current.Resources 并 raise ApplicationThemeChanged，
		// 否则 Diff/热力图/行号边距等 20+ 订阅控件不会重绘，UI 不会实时生效。
		App.ApplyCustomColors();
		try { ForkPlusSettings.Default.Save(); } catch { /* 持久化失败不阻断关闭 */ }
		DialogResult = true;
		Close();
	}

	private void Cancel_Click(object sender, RoutedEventArgs e)
	{
		// 还原对话框打开前的状态（编辑过程已实时应用到 UI，需撤销）。
		ForkPlusSettings.Default.CustomColors = new Dictionary<string, string>(_originalCustomColors);
		ForkPlusSettings.Default.UseCustomColors = _originalUseCustomColors;
		App.ApplyCustomColors();
		DialogResult = false;
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
				set { _hexValue = value; OnPropertyChanged(); OnPropertyChanged(nameof(PreviewBrush)); }
			}

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
					catch { return Brushes.White; }
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
