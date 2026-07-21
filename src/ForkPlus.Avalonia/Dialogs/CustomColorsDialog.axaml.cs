using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using ForkPlus.Services;
using ForkPlus.Settings;
using ForkPlus.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Avalonia.Dialogs
{
    // Avalonia 版 CustomColorsDialog（对照 WPF CustomColorsDialog.xaml 213 行 + .cs 966 行）。
    //
    // 对照 WPF：
    //   - public partial class CustomColorsDialog : ForkPlusDialogWindow
    //   - ShowHeader=false / ShowLogo=false / ShowFooter=false（自定义布局）
    //   - 颜色列表 ItemsControl + DataTemplate（颜色名 + 预览块 + hex 输入 + Reset 按钮）
    //   - 底部按钮：Import/Export + Random Palette + Reset All
    //   - Popup 颜色选择器：HSV 2D 调色盘 + 色相条 + RGB 滑块 + 预设色板 + hex 输入
    //   - LoadItems / InitializeSwatches / ApplyAndRefresh / ApplyPopupColor
    //   - Import/Export JSON 配置（schema="ForkPlus.CustomColors/v1"）
    //   - RandomPalette_Click：基于主色相派生整套配色
    //
    // Avalonia 版差异：
    //   1. spike 模式：根 Grid 自定义布局，无 TitleTextBlock/DescriptionTextBlock（不调 SetTitle/SetDescription）
    //   2. ShowFooter=false（与 WPF 一致，无 Submit/Cancel footer）
    //   3. MouseLeftButtonUp/Down/Move → PointerPressed/Released/Moved（Avalonia 11）
    //   4. CaptureMouse/ReleaseMouseCapture → e.Pointer.Capture(control)/e.Pointer.Capture(null)
    //   5. Line X1/Y1/X2/Y2 → StartPoint/EndPoint（Avalonia Shapes.Line API）
    //   6. System.Windows.Media.Color → Avalonia.Media.Color
    //   7. ColorConverter.ConvertFromString → Avalonia.Media.Color.Parse
    //   8. Application.Current.Resources[key] → TryGetResource（Avalonia 11 API）
    //   9. App.ApplyCustomColors() → spike 不接入（Avalonia 工程暂无该方法，留 Phase 2.5 后接入）
    //  10. SaveFileDialog/OpenFileDialog → StorageProvider.SaveFilePickerAsync/OpenFilePickerAsync
    //  11. MessageBox.Show → MessageBoxWindow.ShowDialog
    //  12. Cursors.Hand → Avalonia.Input.Cursor + StandardCursorType.Hand
    //  13. CustomColorItem → 嵌套类（避免引用 WPF 工程）
    public partial class CustomColorsDialog : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        // 可自定义的颜色 key 列表（对照 WPF _editableColorKeys）。
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
        private bool _isPopupInitializing;
        private bool _isLoaded;

        // JSON 配置文件的 schema 标识（对照 WPF CustomColorsSchema）
        private const string CustomColorsSchema = "ForkPlus.CustomColors/v1";

        public CustomColorsDialog()
        {
            // 关闭基类 ForkPlusDialogWindow 的 chrome（自定义布局，与 WPF 一致）
            ShowFooter = false;
            InitializeComponent();
            Localize();
            LoadItems();
            InitializeSwatches();
            // Popup 关闭（点外部）时清空正在编辑的 item
            ColorPickerPopup.Closed += Popup_Closed;
            Loaded += (_, _) => { _isLoaded = true; };
        }

        private void Localize()
        {
            string lang = ServiceLocator.UserSettings?.UiLanguage ?? ForkPlusSettings.Default.UiLanguage;
            Title = Translate("Custom Colors", lang);
            HeaderTextBlock.Text = Translate("Custom Colors", lang) +
                " (" + Translate(ForkPlusSettings.Default.Theme.SkinName(), lang) + ")";
            ResetAllButton.Content = Translate("Reset All", lang);
            RandomPaletteButton.Content = Translate("Random Palette", lang);
            ImportColorsButton.Content = Translate("Import Colors", lang);
            ExportColorsButton.Content = Translate("Export Colors", lang);
            PopupTitleText.Text = Translate("Color Picker", lang);
            SwatchLabelText.Text = Translate("Presets", lang);
        }

        // 对照 WPF: LoadItems
        private void LoadItems()
        {
            _workingCopy = new Dictionary<string, string>();
            Dictionary<string, string> saved = ForkPlusSettings.Default.CustomColors;
            _items = new List<CustomColorItem>();
            string lang = ServiceLocator.UserSettings?.UiLanguage ?? ForkPlusSettings.Default.UiLanguage;
            string resetLabel = Translate("Reset", lang);
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

        // 对照 WPF: GetCurrentColorHex - 从当前 Application.Resources 取某个 Color key 的 hex 值
        private string GetCurrentColorHex(string key)
        {
            try
            {
                if (Application.Current?.Resources.TryGetResource(key, null, out object obj) == true && obj is Color c)
                {
                    return "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
                }
            }
            catch { }
            return "#FFFFFF";
        }

        // 对照 WPF: TranslateColorKey
        private string TranslateColorKey(string key, string lang)
        {
            string i18nKey = "Color." + key;
            string translated = Translate(i18nKey, lang);
            if (translated != null && translated == i18nKey)
                return key;
            return translated ?? key;
        }

        // 对照 WPF: InitializeSwatches
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
                var swatch = new Border
                {
                    Width = 20, Height = 20,
                    Margin = new Thickness(2),
                    BorderBrush = GetBorderBrush(),
                    BorderThickness = new Thickness(1),
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Tag = hex,
                };
                try { swatch.Background = new SolidColorBrush(Color.Parse(hex)); }
                catch { continue; }
                swatch.PointerReleased += Swatch_Click;
                SwatchPanel.Children.Add(swatch);
            }
        }

        private static IBrush GetBorderBrush()
        {
            try
            {
                if (Application.Current?.Resources.TryGetResource("ThemeBorderMidBrush", null, out object obj) == true && obj is IBrush b)
                    return b;
            }
            catch { }
            return Brushes.Gray;
        }

        #region HSV 调色盘

        // 对照 WPF: Swatch_Click
        private void Swatch_Click(object sender, PointerReleasedEventArgs e)
        {
            if (sender is Border b && b.Tag is string hex)
            {
                _suppressUpdates = true;
                PopupHexBox.Text = hex;
                _suppressUpdates = false;
                UpdatePopupFromHex(hex);
            }
        }

        // 对照 WPF: ColorPreview_Click - 颜色预览块点击 → 打开颜色选择 Popup
        // Avalonia 11 没有 FrameworkElement，改用 Control（同样有 Tag 属性）
        private void ColorPreview_Click(object sender, PointerReleasedEventArgs e)
        {
            if (sender is Control fe && fe.Tag is CustomColorItem item)
            {
                _popupEditingItem = item;
                _isPopupInitializing = true;
                UpdatePopupFromHex(item.HexValue);
                _isPopupInitializing = false;
                ColorPickerPopup.IsOpen = true;
            }
        }

        // 对照 WPF: UpdatePopupFromHex
        private void UpdatePopupFromHex(string hex)
        {
            try
            {
                Color c = Color.Parse(hex);
                _suppressUpdates = true;
                RSlider.Value = c.R;
                GSlider.Value = c.G;
                BSlider.Value = c.B;
                PopupHexBox.Text = "#" + c.R.ToString("X2") + c.G.ToString("X2") + c.B.ToString("X2");
                PopupPreviewRect.Fill = new SolidColorBrush(c);
                RgbToHsv(c.R, c.G, c.B, out double h, out double s, out double v);
                UpdateHsvCanvas(h, s, v);
                UpdateHueIndicator(h);
                _suppressUpdates = false;
                if (!_isPopupInitializing)
                    ApplyPopupColor();
            }
            catch { }
        }

        // 对照 WPF: ApplyPopupColor
        private void ApplyPopupColor()
        {
            if (_popupEditingItem == null) return;
            string hex = PopupHexBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(hex)) return;
            if (!hex.StartsWith("#")) hex = "#" + hex;
            try { Color.Parse(hex); }
            catch { return; }
            _popupEditingItem.HexValue = hex;
            _popupEditingItem.IsCustomized = true;
            _workingCopy[_popupEditingItem.Key] = hex;
            ApplyAndRefresh();
        }

        // 对照 WPF: Popup_Closed
        private void Popup_Closed(object sender, EventArgs e)
        {
            _popupEditingItem = null;
        }

        // 对照 WPF: UpdateHsvCanvas
        private void UpdateHsvCanvas(double h, double s, double v)
        {
            Color pureHue = HsvToRgbColor(h, 1.0, 1.0);
            HsvBaseRect.Fill = new SolidColorBrush(pureHue);
            double x = s * 240 - 5;
            double y = (1 - v) * 160 - 5;
            Canvas.SetLeft(HsvIndicator, Math.Max(-5, Math.Min(235, x)));
            Canvas.SetTop(HsvIndicator, Math.Max(-5, Math.Min(155, y)));
        }

        // 对照 WPF: UpdateHueIndicator（WPF 用 Y1/Y2，Avalonia 用 StartPoint/EndPoint）
        private void UpdateHueIndicator(double h)
        {
            double y = (h / 360.0) * 160;
            HueIndicator.StartPoint = new Point(0, y);
            HueIndicator.EndPoint = new Point(20, y);
        }

        // 对照 WPF: HsvCanvas_MouseDown → PointerPressed
        private void HsvCanvas_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            _isDraggingHsv = true;
            e.Pointer.Capture(HsvCanvas);
            UpdateHsvFromMouse(e.GetPosition(HsvCanvas));
        }

        // 对照 WPF: HsvCanvas_MouseUp → PointerReleased
        private void HsvCanvas_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            _isDraggingHsv = false;
            e.Pointer.Capture(null);
        }

        // 对照 WPF: HsvCanvas_MouseMove → PointerMoved
        private void HsvCanvas_PointerMoved(object sender, PointerEventArgs e)
        {
            if (_isDraggingHsv)
                UpdateHsvFromMouse(e.GetPosition(HsvCanvas));
        }

        // 对照 WPF: UpdateHsvFromMouse
        private void UpdateHsvFromMouse(Point pos)
        {
            double s = Math.Max(0, Math.Min(1, pos.X / 240));
            double v = Math.Max(0, Math.Min(1, 1 - pos.Y / 160));
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
            ApplyPopupColor();
        }

        // 对照 WPF: HueCanvas_MouseDown/Up/Move → PointerPressed/Released/Moved
        private void HueCanvas_PointerPressed(object sender, PointerPressedEventArgs e)
        {
            _isDraggingHue = true;
            e.Pointer.Capture(HueCanvas);
            UpdateHueFromMouse(e.GetPosition(HueCanvas));
        }

        private void HueCanvas_PointerReleased(object sender, PointerReleasedEventArgs e)
        {
            _isDraggingHue = false;
            e.Pointer.Capture(null);
        }

        private void HueCanvas_PointerMoved(object sender, PointerEventArgs e)
        {
            if (_isDraggingHue)
                UpdateHueFromMouse(e.GetPosition(HueCanvas));
        }

        // 对照 WPF: UpdateHueFromMouse
        private void UpdateHueFromMouse(Point pos)
        {
            double h = Math.Max(0, Math.Min(360, (pos.Y / 160) * 360));
            GetSvFromIndicator(out double s, out double v);
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
            ApplyPopupColor();
        }

        // 对照 WPF: GetHueFromIndicator（WPF 用 HueIndicator.Y1，Avalonia 用 StartPoint.Y）
        private double GetHueFromIndicator()
        {
            return (HueIndicator.StartPoint.Y / 160) * 360;
        }

        // 对照 WPF: GetSvFromIndicator
        private void GetSvFromIndicator(out double s, out double v)
        {
            double x = Canvas.GetLeft(HsvIndicator) + 5;
            double y = Canvas.GetTop(HsvIndicator) + 5;
            s = Math.Max(0, Math.Min(1, x / 240));
            v = Math.Max(0, Math.Min(1, 1 - y / 160));
        }

        // 对照 WPF: RgbSlider_ValueChanged（WPF 用 RoutedPropertyChangedEventArgs<double>，
        //   Avalonia 11 用非泛型 RangeBaseValueChangedEventArgs）
        private void RgbSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (!_isLoaded || _suppressUpdates) return;
            byte r = (byte)Math.Round(RSlider.Value);
            byte g = (byte)Math.Round(GSlider.Value);
            byte b = (byte)Math.Round(BSlider.Value);
            string hex = "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
            _suppressUpdates = true;
            PopupHexBox.Text = hex;
            PopupPreviewRect.Fill = new SolidColorBrush(Color.FromRgb(r, g, b));
            RgbToHsv(r, g, b, out double h, out double s, out double v);
            UpdateHsvCanvas(h, s, v);
            UpdateHueIndicator(h);
            _suppressUpdates = false;
            ApplyPopupColor();
        }

        // 对照 WPF: PopupHex_TextChanged
        private void PopupHex_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_suppressUpdates) return;
            UpdatePopupFromHex(PopupHexBox.Text);
        }

        // HSV ↔ RGB 转换（对照 WPF RgbToHsv / HsvToRgbColor）
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

        // 对照 WPF: HexTextBox_TextChanged
        private void HexTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb && tb.Tag is CustomColorItem item)
            {
                string hex = tb.Text?.Trim() ?? "";
                if (string.IsNullOrEmpty(hex)) return;
                if (!hex.StartsWith("#")) hex = "#" + hex;
                try
                {
                    Color.Parse(hex);
                    item.HexValue = hex;
                    item.IsCustomized = true;
                    _workingCopy[item.Key] = hex;
                    ApplyAndRefresh();
                }
                catch { }
            }
        }

        // 对照 WPF: ResetItem_Click
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

        // 对照 WPF: ResetAll_Click
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

        #region 导入/导出颜色配置（对照 WPF）

        // 对照 WPF: ExportColors_Click
        private async void ExportColors_Click(object sender, RoutedEventArgs e)
        {
            string lang = ServiceLocator.UserSettings?.UiLanguage ?? ForkPlusSettings.Default.UiLanguage;
            ColorPickerPopup.IsOpen = false;

            if (_workingCopy == null || _workingCopy.Count == 0)
            {
                var msgBox = new MessageBoxWindow(
                    Translate("Export Colors", lang),
                    Translate("No custom colors to export. Customize some colors first.", lang),
                    "OK", "Cancel", showCancelButton: false);
                await msgBox.ShowDialog<bool?>(this);
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var options = new FilePickerSaveOptions
            {
                Title = Translate("Export Colors", lang),
                DefaultExtension = "json",
                SuggestedFileName = "ForkPlus-Colors-" + ForkPlusSettings.Default.Theme.SkinName() + ".json",
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new FilePickerFileType("JSON")
                    {
                        Patterns = new List<string> { "*.json" }
                    },
                    new FilePickerFileType("All files")
                    {
                        Patterns = new List<string> { "*.*" }
                    }
                }
            };

            var storageFile = await topLevel.StorageProvider.SaveFilePickerAsync(options);
            if (storageFile == null) return;

            try
            {
                var exportColors = new Dictionary<string, string>(_workingCopy);
                JObject root = new JObject
                {
                    ["schema"] = CustomColorsSchema,
                    ["theme"] = ForkPlusSettings.Default.Theme.ToString(),
                    ["exportedAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    ["customColors"] = JObject.FromObject(exportColors),
                };
                string json = root.ToString(Formatting.Indented);
                await using var stream = await storageFile.OpenWriteAsync();
                using var writer = new StreamWriter(stream);
                await writer.WriteAsync(json);

                var msgBox = new MessageBoxWindow(
                    Translate("Export Colors", lang),
                    string.Format(Translate("Exported {0} custom colors to:\n{1}", lang),
                        exportColors.Count, storageFile.Path.LocalPath),
                    "OK", "Cancel", showCancelButton: false);
                await msgBox.ShowDialog<bool?>(this);
            }
            catch (Exception ex)
            {
                var msgBox = new MessageBoxWindow(
                    Translate("Export Colors", lang),
                    Translate("Export failed: ", lang) + ex.Message,
                    "OK", "Cancel", showCancelButton: false);
                await msgBox.ShowDialog<bool?>(this);
            }
        }

        // 对照 WPF: ImportColors_Click
        private async void ImportColors_Click(object sender, RoutedEventArgs e)
        {
            string lang = ServiceLocator.UserSettings?.UiLanguage ?? ForkPlusSettings.Default.UiLanguage;
            ColorPickerPopup.IsOpen = false;

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var options = new FilePickerOpenOptions
            {
                Title = Translate("Import Colors", lang),
                AllowMultiple = false,
                FileTypeFilter = new List<FilePickerFileType>
                {
                    new FilePickerFileType("JSON")
                    {
                        Patterns = new List<string> { "*.json" }
                    },
                    new FilePickerFileType("All files")
                    {
                        Patterns = new List<string> { "*.*" }
                    }
                }
            };

            var result = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            if (result == null || result.Count == 0) return;
            var storageFile = result[0];

            string jsonText;
            try
            {
                await using var stream = await storageFile.OpenReadAsync();
                using var reader = new StreamReader(stream);
                jsonText = await reader.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                var msgBox = new MessageBoxWindow(
                    Translate("Import Colors", lang),
                    Translate("Cannot read file: ", lang) + ex.Message,
                    "OK", "Cancel", showCancelButton: false);
                await msgBox.ShowDialog<bool?>(this);
                return;
            }

            // 校验 1: 必须是合法 JSON
            JObject root;
            try
            {
                JToken parsed = JToken.Parse(jsonText);
                if (parsed.Type != JTokenType.Object)
                {
                    var msgBox = new MessageBoxWindow(
                        Translate("Import Colors", lang),
                        Translate("Invalid format: JSON root must be an object.", lang),
                        "OK", "Cancel", showCancelButton: false);
                    await msgBox.ShowDialog<bool?>(this);
                    return;
                }
                root = (JObject)parsed;
            }
            catch (JsonReaderException ex)
            {
                var msgBox = new MessageBoxWindow(
                    Translate("Import Colors", lang),
                    Translate("Invalid JSON: ", lang) + ex.Message,
                    "OK", "Cancel", showCancelButton: false);
                await msgBox.ShowDialog<bool?>(this);
                return;
            }

            // 校验 2: schema 字段如果存在，必须匹配
            JToken schemaToken = root["schema"];
            if (schemaToken != null)
            {
                if (schemaToken.Type != JTokenType.String || (string)schemaToken != CustomColorsSchema)
                {
                    var msgBox = new MessageBoxWindow(
                        Translate("Import Colors", lang),
                        string.Format(Translate("Unsupported schema. Expected '{0}'.", lang), CustomColorsSchema),
                        "OK", "Cancel", showCancelButton: false);
                    await msgBox.ShowDialog<bool?>(this);
                    return;
                }
            }

            // 校验 3: customColors 字段必须存在且是对象
            JToken colorsToken = root["customColors"];
            if (colorsToken == null)
            {
                var msgBox = new MessageBoxWindow(
                    Translate("Import Colors", lang),
                    Translate("Invalid format: missing 'customColors' field.", lang),
                    "OK", "Cancel", showCancelButton: false);
                await msgBox.ShowDialog<bool?>(this);
                return;
            }
            if (colorsToken.Type != JTokenType.Object)
            {
                var msgBox = new MessageBoxWindow(
                    Translate("Import Colors", lang),
                    Translate("Invalid format: 'customColors' must be an object.", lang),
                    "OK", "Cancel", showCancelButton: false);
                await msgBox.ShowDialog<bool?>(this);
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
                if (valToken.Type != JTokenType.String)
                {
                    errorCount++;
                    if (errorCount <= maxErrorsShown)
                        errorBuf.AppendLine(string.Format(Translate("  - '{0}': value must be a string", lang), key));
                    continue;
                }
                string hex = (string)valToken;
                if (!validKeys.Contains(key))
                {
                    errorCount++;
                    if (errorCount <= maxErrorsShown)
                        errorBuf.AppendLine(string.Format(Translate("  - '{0}': unknown color key", lang), key));
                    continue;
                }
                if (!IsValidHexColor(hex))
                {
                    errorCount++;
                    if (errorCount <= maxErrorsShown)
                        errorBuf.AppendLine(string.Format(Translate("  - '{0}': invalid hex color '{1}'", lang), key, hex));
                    continue;
                }
                if (!hex.StartsWith("#")) hex = "#" + hex;
                imported[key] = hex;
            }

            if (errorCount > 0)
            {
                string summary;
                if (errorCount > maxErrorsShown)
                    summary = string.Format(Translate("Import aborted: {0} errors found (showing first {1}):\n", lang),
                        errorCount, maxErrorsShown);
                else
                    summary = string.Format(Translate("Import aborted: {0} errors found:\n", lang), errorCount);
                var msgBox = new MessageBoxWindow(
                    Translate("Import Colors", lang),
                    summary + errorBuf.ToString(),
                    "OK", "Cancel", showCancelButton: false);
                await msgBox.ShowDialog<bool?>(this);
                return;
            }

            if (imported.Count == 0)
            {
                var msgBox = new MessageBoxWindow(
                    Translate("Import Colors", lang),
                    Translate("No valid color entries found in file.", lang),
                    "OK", "Cancel", showCancelButton: false);
                await msgBox.ShowDialog<bool?>(this);
                return;
            }

            // 应用导入的配色：合并到 _workingCopy 并刷新 UI + 落盘
            foreach (KeyValuePair<string, string> kv in imported)
                _workingCopy[kv.Key] = kv.Value;

            foreach (CustomColorItem item in _items)
            {
                if (_workingCopy.TryGetValue(item.Key, out string hex))
                {
                    item.HexValue = hex;
                    item.IsCustomized = true;
                }
            }

            ApplyAndRefresh();

            var successBox = new MessageBoxWindow(
                Translate("Import Colors", lang),
                string.Format(Translate("Imported {0} colors successfully.", lang), imported.Count),
                "OK", "Cancel", showCancelButton: false);
            await successBox.ShowDialog<bool?>(this);
        }

        // 对照 WPF: IsValidHexColor（WPF 用 ColorConverter.ConvertFromString，Avalonia 用 Color.Parse）
        private static bool IsValidHexColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return false;
            try
            {
                string normalized = hex.Trim();
                if (!normalized.StartsWith("#")) normalized = "#" + normalized;
                Color.Parse(normalized);
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        // 对照 WPF: RandomPalette_Click
        private void RandomPalette_Click(object sender, RoutedEventArgs e)
        {
            bool isDark = ForkPlusSettings.Default.Theme.IsDarkBase();
            var rand = new Random();
            double baseHue = rand.NextDouble() * 360.0;
            double accentHue = (baseHue + 180.0 + (rand.NextDouble() * 60.0 - 30.0)) % 360.0;

            Func<double, double, double, byte, Color> hsv = (h, s, v, a) =>
            {
                Color c = HsvToRgbColor(h, s, v);
                return Color.FromArgb(a, c.R, c.G, c.B);
            };

            Color bgColor, panelBgColor, secondaryBgColor, borderColor, labelColor, fgColor, secondaryLabelColor, accentColor, accentSecondaryColor, referenceColor, iconColor;
            if (isDark)
            {
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

            double greenHue = 120.0 + (rand.NextDouble() * 60.0 - 30.0);
            double redHue = (360.0 + (rand.NextDouble() * 30.0 - 15.0)) % 360.0;
            Color diffAdded, diffRemoved, diffAddBg, diffRemoveBg, diffExactAdd, diffExactRemove;
            if (isDark)
            {
                diffAdded = hsv(greenHue, 0.40 + rand.NextDouble() * 0.15, 0.35 + rand.NextDouble() * 0.15, 255);
                diffRemoved = hsv(redHue, 0.40 + rand.NextDouble() * 0.15, 0.35 + rand.NextDouble() * 0.15, 255);
                diffAddBg = hsv(greenHue, 0.20 + rand.NextDouble() * 0.15, 0.15 + rand.NextDouble() * 0.15, 255);
                diffRemoveBg = hsv(redHue, 0.20 + rand.NextDouble() * 0.15, 0.15 + rand.NextDouble() * 0.15, 255);
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

            Color codeBg = isDark ? hsv(baseHue, 0.18, 0.12, 255) : hsv(baseHue, 0.10, 0.99, 255);
            Color codeFg = isDark ? hsv(baseHue, 0.08, 0.92, 255) : hsv(baseHue, 0.25, 0.15, 255);
            Color syntaxComment = isDark ? hsv(baseHue, 0.20, 0.55, 255) : hsv(baseHue, 0.35, 0.45, 255);
            Color syntaxString = hsv((baseHue + 30.0) % 360.0, 0.55, isDark ? 0.85 : 0.40, 255);
            Color syntaxKeyword = hsv((baseHue + 180.0) % 360.0, 0.70, isDark ? 0.80 : 0.45, 255);
            Color syntaxNumber = hsv((baseHue + 90.0) % 360.0, 0.55, isDark ? 0.75 : 0.40, 255);
            Color lineNumberFg = isDark ? hsv(baseHue, 0.10, 0.45, 255) : hsv(baseHue, 0.20, 0.55, 255);
            Color lineNumberSep = isDark ? hsv(baseHue, 0.10, 0.25, 255) : hsv(baseHue, 0.10, 0.80, 255);
            Color chunkBorder = accentColor;
            Color chunkBg = hsv(baseHue, 0.40, isDark ? 0.30 : 0.85, 60);
            Color windowBg = bgColor;
            Color titleBarBg = isDark ? hsv(baseHue, 0.20, 0.16, 255) : hsv(baseHue, 0.14, 0.96, 255);

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
            Set("Diff.AddColor", diffAddBg);
            Set("Diff.RemoveColor", diffRemoveBg);
            Set("Diff.ExactAddColor", diffExactAdd);
            Set("Diff.ExactRemoveColor", diffExactRemove);
            Set("CodeEditor.BackgroundColor", codeBg);
            Set("CodeEditor.ForegroundColor", codeFg);
            Set("Syntax.CommentColor", syntaxComment);
            Set("Syntax.StringColor", syntaxString);
            Set("Syntax.KeywordColor", syntaxKeyword);
            Set("Syntax.NumberColor", syntaxNumber);
            Set("LineNumber.ForegroundColor", lineNumberFg);
            Set("LineNumber.SeparatorColor", lineNumberSep);
            Set("ChunkSelection.BorderColor", chunkBorder);
            Set("ChunkSelection.BackgroundColor", chunkBg);
            Set("Window.BackgroundColor", windowBg);
            Set("Window.TitleBar.BackgroundColor", titleBarBg);

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

        // 对照 WPF: ApplyAndRefresh
        // spike 版：跳过 App.ApplyCustomColors()（Avalonia 工程暂无该方法，留 Phase 2.5 后接入）
        private void ApplyAndRefresh()
        {
            ForkPlusSettings.Default.CustomColors = new Dictionary<string, string>(_workingCopy);
            if (_workingCopy.Count > 0)
            {
                ForkPlusSettings.Default.UseCustomColors = true;
            }
            // spike 版：跳过 App.ApplyCustomColors()（Avalonia 工程暂无该方法）
            // 主界面实时预览需要订阅 NotificationCenter.ApplicationThemeChanged，
            // spike 不接入，留 Phase 2.5 完成主题服务后再接入
            foreach (CustomColorItem item in _items)
                item.RefreshPreview();
            try { ForkPlusSettings.Default.Save(); } catch { }
        }

        // 对照 WPF: PreferencesLocalization.Translate(text, lang)
        private static string Translate(string text, string lang)
        {
            var localization = ServiceLocator.Localization;
            if (localization != null)
            {
                return localization.Translate(text, lang);
            }
            return text;
        }

        // 对照 WPF: CustomColorItem（嵌套 ViewModel）
        public class CustomColorItem : System.ComponentModel.INotifyPropertyChanged
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

            // 对照 WPF: PreviewBrush - WPF 用 System.Windows.Media.Brush，Avalonia 用 Avalonia.Media.IBrush
            public IBrush PreviewBrush
            {
                get
                {
                    try { return new SolidColorBrush(Color.Parse(HexValue)); }
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

            public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string name = null)
                => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
    }
}
