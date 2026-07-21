using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 UserColorsUserControl（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/UserColorsUserControl.xaml.cs（76 行）：
    //   - 静态 _userBrushes（UserColorBrushes 实例，AllBrushes(ThemeType) 返回 49 长度数组）
    //   - 构造函数接受 ToggleButton parentButton + string userEmail + byte userBrushIndex
    //   - ColorButton_Changed：取 ToggleButton.DataContext as UserColorViewModel，
    //     按 IsSelected 计算 selectedColor（IsSelected=true → brushIndex-1，false → -1），
    //     UpdateUserColor(selectedColor) + HidePopup()
    //   - Refresh(byte userBrush)：构造 48 个 UserColorViewModel（跳过 index 0），
    //     按 userBrush 选中对应项
    //   - UpdateUserColor：触发 SelectedColorChanged 事件（参数 (userEmail, brushIndex)）
    //   - HidePopup：_parentButton.IsChecked = false
    //
    // Avalonia 版差异（spike 简化）：
    //   - WPF UserColorBrushes（依赖 ThemeType + 颜色混合算法）→ spike 静态 PaletteHex[49] 数组
    //     （48 色 4×12 grid，index 0 占位 null；与 WPF CreateBrushes 生成的 49 项数组对齐）
    //   - WPF ToggleButton.Checked/Unchecked → IsCheckedChanged 事件
    //   - WPF ToggleButton parentButton（关闭 Popup）→ spike 保留（构造函数接受 ToggleButton）
    //   - WPF UserColorViewModel : INotifyPropertyChanged → spike POCO（spike 不需要双向绑定回写）
    //   - WPF _userBrushes.AllBrushes(ForkPlusSettings.Default.Theme) → PaletteHex 静态数组
    public partial class UserColorsUserControl : UserControl
    {
        // ===== UserColorViewModel POCO（对照 WPF UserColorViewModel.cs：INotifyPropertyChanged）=====
        // spike 简化为 POCO：Avalonia 数据绑定不需要 INotifyPropertyChanged（直接 ItemsSource 重置）
        public class UserColorViewModel
        {
            public int BrushIndex { get; }
            public ISolidColorBrush Brush { get; }
            public bool IsSelected { get; set; }

            public UserColorViewModel(int brushIndex, ISolidColorBrush brush, bool isSelected)
            {
                BrushIndex = brushIndex;
                Brush = brush;
                IsSelected = isSelected;
            }
        }

        // ===== PaletteHex 静态调色板（对照 WPF UserColorBrushes._userBorderColors + 4×12 grid）=====
        // WPF UserColorBrushes._userBorderColors 12 基色 + 4 opacity 档位（100/70/50/30 light，
        // 100/80/60/40 dark）共 48 色，index 0 占位 null。
        // spike 用 hex 字符串硬编码（取 opacity=100 档位 12 基色 + opacity=70/50/30 三档混合色近似）。
        // 对照 WPF _userBorderColors hex:
        //   #E63D0C / #FF9921 / #CAC61B / #7EC71D / #1BC529 / #1BC57F /
        //   #0FCCBF / #23BBFC / #4377E2 / #6348DA / #9E32D4 / #DE0FD5
        private static readonly string[] PaletteHex = new string[49]
        {
            null, // index 0 占位（对照 WPF list.Add(null)）
            // Row 0: opacity=100（纯色）
            "#FFE63D0C", "#FFFF9921", "#FFCAC61B", "#FF7EC71D", "#FF1BC529", "#FF1BC57F",
            "#FF0FCCBF", "#FF23BBFC", "#FF4377E2", "#FF6348DA", "#FF9E32D4", "#FFDE0FD5",
            // Row 1: opacity=70（混合白色 70%）
            "#FFF16A47", "#FFF5B361", "#FFE5E24F", "#FFBEDB53", "#FF5DD967", "#FF5DD9A6",
            "#FF4EDDD2", "#FF65CBFD", "#FF7E9BE9", "#FF8E78E3", "#FFB268DD", "#FFE650DC",
            // Row 2: opacity=50
            "#FFF39B82", "#FFF7C891", "#FFEDE588", "#FFD0E58A", "#FF8CE190", "#FF8CE1BF",
            "#FF7FE6DF", "#FF94DEFE", "#FFA6BCEE", "#FFB1A6EA", "#FFC59BE7", "#FFED87E5",
            // Row 3: opacity=30
            "#FFF5BDB0", "#FFF9DDB8", "#FFF4EDC4", "#FFE0EDB2", "#FFBCEBB4", "#FFBCEBD2",
            "#FFB6EDE9", "#FFC6E8FE", "#FFCDD8F4", "#FFD2CDF1", "#FFDECFEE", "#FFF5B3EB"
        };

        // ===== 私有字段（对照 WPF）=====
        private readonly ToggleButton _parentButton;
        private readonly string _userEmail;

        // ===== 公共属性（对照 WPF ColorViewModels）=====
        private List<UserColorViewModel> ColorViewModels { get; set; }

        // ===== 事件（对照 WPF: public event EventHandler<(string, byte)> SelectedColorChanged）=====
        public event EventHandler<(string userEmail, byte brushIndex)> SelectedColorChanged;

        // ===== 构造函数（对照 WPF: UserColorsUserControl(ToggleButton parentButton, string userEmail, byte userBrushIndex)）=====
        public UserColorsUserControl() : this(null, null, 0)
        {
        }

        public UserColorsUserControl(ToggleButton parentButton, string userEmail, byte userBrushIndex)
        {
            InitializeComponent();
            _parentButton = parentButton;
            _userEmail = userEmail;
            Refresh(userBrushIndex);
        }

        // ===== ColorButton_Changed（对照 WPF）=====
        // 对照 WPF: private void ColorButton_Changed(object sender, RoutedEventArgs e)
        //   WPF: e.Handled = true;
        //         if (sender is ToggleButton { DataContext: UserColorViewModel dataContext })
        //         { int selectedColor = (dataContext.IsSelected ? (dataContext.BrushIndex - 1) : (-1));
        //           UpdateUserColor(selectedColor); }
        //         HidePopup();
        private void ColorButton_Changed(object sender, RoutedEventArgs e)
        {
            e.Handled = true;
            if (sender is ToggleButton tb && tb.DataContext is UserColorViewModel dataContext)
            {
                int selectedColor = dataContext.IsSelected ? (dataContext.BrushIndex - 1) : (-1);
                UpdateUserColor(selectedColor);
            }
            HidePopup();
        }

        // ===== Refresh（对照 WPF）=====
        // 对照 WPF: private void Refresh(byte userBrush)
        //   WPF: SolidColorBrush[] array = _userBrushes.AllBrushes(ForkPlusSettings.Default.Theme);
        //         ColorViewModels = new List<UserColorViewModel>(array.Length);
        //         for (int i = 1; i < array.Length; i++) ColorViewModels.Add(new UserColorViewModel(i, array[i], false));
        //         Colors.ItemsSource = ColorViewModels;
        //         if (userBrush - 1 > -1) ColorViewModels[userBrush - 1].IsSelected = true;
        //   spike: 用 PaletteHex 静态数组替代 AllBrushes(theme)
        private void Refresh(byte userBrush)
        {
            ColorViewModels = new List<UserColorViewModel>(PaletteHex.Length);
            for (int i = 1; i < PaletteHex.Length; i++)
            {
                ColorViewModels.Add(new UserColorViewModel(i, ParseBrush(PaletteHex[i]), false));
            }
            Colors.ItemsSource = ColorViewModels;
            int num = userBrush - 1;
            if (num > -1 && num < ColorViewModels.Count)
            {
                ColorViewModels[num].IsSelected = true;
            }
        }

        // ===== UpdateUserColor（对照 WPF）=====
        // 对照 WPF: private void UpdateUserColor(int selectedColor)
        //   WPF: int num = selectedColor + 1;
        //         SelectedColorChanged?.Invoke(this, (_userEmail, (byte)num));
        private void UpdateUserColor(int selectedColor)
        {
            int num = selectedColor + 1;
            SelectedColorChanged?.Invoke(this, (_userEmail, (byte)num));
        }

        // ===== HidePopup（对照 WPF）=====
        // 对照 WPF: private void HidePopup() { _parentButton.IsChecked = false; }
        private void HidePopup()
        {
            if (_parentButton != null)
            {
                _parentButton.IsChecked = false;
            }
        }

        // spike 辅助：从 hex 字符串解析 SolidColorBrush
        // 对照 WPF ColorConverter.ConvertFromString + new SolidColorBrush
        private static ISolidColorBrush ParseBrush(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Brushes.Transparent;
            // hex 形如 #FFE63D0C（ARGB）
            string h = hex.TrimStart('#');
            byte a = Convert.ToByte(h.Substring(0, 2), 16);
            byte r = Convert.ToByte(h.Substring(2, 2), 16);
            byte g = Convert.ToByte(h.Substring(4, 2), 16);
            byte b = Convert.ToByte(h.Substring(6, 2), 16);
            return new SolidColorBrush(Color.FromArgb(a, r, g, b));
        }
    }
}
