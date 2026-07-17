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
	/// <summary>自定义颜色编辑对话框。列出可自定义的核心颜色，支持 hex 输入和颜色选择器。
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
			"CodeEditor.BackgroundColor",
			"CodeEditor.ForegroundColor",
			"Window.BackgroundColor",
			"Window.TitleBar.BackgroundColor",
		};

		private List<CustomColorItem> _items;
		private Dictionary<string, string> _workingCopy;  // 工作副本，确定后才写回
		private CustomColorItem _popupEditingItem;
		private bool _suppressPopupHexUpdate;

		public CustomColorsDialog()
		{
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
			OkButton.Content = PreferencesLocalization.Translate("OK", lang);
			CancelButton.Content = PreferencesLocalization.Translate("Cancel", lang);
			PopupTitleText.Text = PreferencesLocalization.Translate("Color Picker", lang);
		}

		/// <summary>加载颜色列表。每项显示当前生效值（自定义覆盖或预设原色）。</summary>
		private void LoadItems()
		{
			_workingCopy = new Dictionary<string, string>();
			Dictionary<string, string> saved = ForkPlusSettings.Default.CustomColors;
			_items = new List<CustomColorItem>();
			foreach (string key in _editableColorKeys)
			{
				// 优先用自定义值，否则从当前资源字典取预设原色
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
				_items.Add(new CustomColorItem(key, TranslateColorKey(key), hex, isCustomized));
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

		private string TranslateColorKey(string key)
		{
			// 用 key 作为 i18n key，找不到就返回原 key
			string lang = ForkPlusSettings.Default.UiLanguage;
			string translated = PreferencesLocalization.Translate("Color." + key, lang);
			// Translate 找不到时返回原文，所以如果返回值以 "Color." 开头说明没翻译
			if (translated != null && translated.StartsWith("Color."))
				return key;
			return translated;
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
			};
			foreach (string hex in palette)
			{
				Border swatch = new Border
				{
					Width = 18, Height = 18,
					Margin = new Thickness(1),
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

		private void Swatch_Click(object sender, MouseButtonEventArgs e)
		{
			if (sender is Border b && b.Tag is string hex)
			{
				_suppressPopupHexUpdate = true;
				PopupHexBox.Text = hex;
				_suppressPopupHexUpdate = false;
				UpdatePopupPreviewAndSliders(hex);
			}
		}

		/// <summary>颜色预览块点击 → 打开颜色选择 Popup。</summary>
		private void ColorPreview_Click(object sender, MouseButtonEventArgs e)
		{
			if (sender is FrameworkElement fe && fe.Tag is CustomColorItem item)
			{
				_popupEditingItem = item;
				_suppressPopupHexUpdate = true;
				PopupHexBox.Text = item.HexValue;
				_suppressPopupHexUpdate = false;
				UpdatePopupPreviewAndSliders(item.HexValue);
				ColorPickerPopup.IsOpen = true;
			}
		}

		private void UpdatePopupPreviewAndSliders(string hex)
		{
			try
			{
				Color c = (Color)ColorConverter.ConvertFromString(hex);
				PopupPreviewRect.Fill = new SolidColorBrush(c);
				RSlider.Value = c.R;
				GSlider.Value = c.G;
				BSlider.Value = c.B;
			}
			catch { }
		}

		private void RgbSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
		{
			if (!IsLoaded) return;
			byte r = (byte)Math.Round(RSlider.Value);
			byte g = (byte)Math.Round(GSlider.Value);
			byte b = (byte)Math.Round(BSlider.Value);
			string hex = "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
			_suppressPopupHexUpdate = true;
			PopupHexBox.Text = hex;
			_suppressPopupHexUpdate = false;
			PopupPreviewRect.Fill = new SolidColorBrush(Color.FromRgb(r, g, b));
		}

		private void PopupHex_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (_suppressPopupHexUpdate) return;
			UpdatePopupPreviewAndSliders(PopupHexBox.Text);
		}

		private void PopupOk_Click(object sender, RoutedEventArgs e)
		{
			if (_popupEditingItem != null)
			{
				string hex = PopupHexBox.Text.Trim();
				if (!hex.StartsWith("#")) hex = "#" + hex;
				try
				{
					ColorConverter.ConvertFromString(hex);  // 验证
				}
				catch
				{
					return;  // 无效值，不关闭
				}
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

		/// <summary>hex 输入框文字变化 → 实时更新预览 + 应用。</summary>
		private void HexTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			if (sender is TextBox tb && tb.Tag is CustomColorItem item)
			{
				string hex = tb.Text.Trim();
				if (string.IsNullOrEmpty(hex)) return;
				if (!hex.StartsWith("#")) hex = "#" + hex;
				try
				{
					Color c = (Color)ColorConverter.ConvertFromString(hex);
					item.HexValue = hex;
					item.IsCustomized = true;
					_workingCopy[item.Key] = hex;
					ApplyAndRefresh();
				}
				catch { /* 无效 hex，忽略 */ }
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

		/// <summary>把工作副本写入 settings + 应用到 UI（即时预览）。</summary>
		private void ApplyAndRefresh()
		{
			ForkPlusSettings.Default.CustomColors = new Dictionary<string, string>(_workingCopy);
			App.ApplyCustomColors();
			// 刷新列表中每项的预览画刷（预设原色可能因覆盖移除而变回原色）
			foreach (CustomColorItem item in _items)
				item.RefreshPreview();
		}

		private void Ok_Click(object sender, RoutedEventArgs e)
		{
			// 工作副本已在 ApplyAndRefresh 中写入 settings，这里只需保存 + 关闭
			DialogResult = true;
			Close();
		}

		private void Cancel_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
		}

		/// <summary>颜色项 ViewModel。</summary>
		public class CustomColorItem : INotifyPropertyChanged
		{
			public string Key { get; }
			public string DisplayName { get; }

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

			public CustomColorItem(string key, string displayName, string hexValue, bool isCustomized)
			{
				Key = key;
				DisplayName = displayName;
				_hexValue = hexValue;
				_isCustomized = isCustomized;
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
