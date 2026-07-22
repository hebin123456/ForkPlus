using System.ComponentModel;
using Avalonia.Media;
using Avalonia;
using ForkPlus.Settings;
using ForkPlus.UI;

// Avalonia spike 版 BranchViewModel（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/BranchViewModel.cs（167 行）：
//   - WPF: public abstract class BranchViewModel : ReferenceViewModel
//   - 4 组静态 SolidColorBrush 数组：borderBrushesLight/Dark + backgroundBrushesLight/Dark（各 13 个颜色）
//   - BorderBrush / BackgroundBrush 属性（依据 ActiveGraphColumn + 主题明暗选择颜色）
//   - RefreshBrushes()：清空缓存重选
//   - 静态构造：ColorConverter.ConvertFromString + Freeze
//   - 依赖：System.Windows.Media.SolidColorBrush / ColorConverter / ForkPlusSettings
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF System.Windows.Media.SolidColorBrush → Avalonia.Media.SolidColorBrush
//   2. WPF ColorConverter.ConvertFromString → Avalonia.Color.Parse
//   3. WPF SolidColorBrush.Freeze() → Avalonia 无此方法（跳过）
//   4. WPF ForkPlusSettings.Default.Theme → spike 保留（Core 可用）
//   5. WPF PropertyChangedEventArgs → System.ComponentModel（跨平台）
//
// spike 简化（task spec 关键 API）：
//   - BorderBrush / BackgroundBrush + RefreshBrushes
namespace ForkPlus.Avalonia
{
    public abstract class BranchViewModel : ReferenceViewModel
    {
        private static readonly PropertyChangedEventArgs BorderBrushChangedEventArgs = new PropertyChangedEventArgs("BorderBrush");
        private static readonly PropertyChangedEventArgs BackgroundBrushChangedEventArgs = new PropertyChangedEventArgs("BackgroundBrush");

        private static readonly SolidColorBrush[] _borderBrushesLight;
        private static readonly SolidColorBrush[] _borderBrushesDark;
        private static readonly SolidColorBrush[] _backgroundBrushesLight;
        private static readonly SolidColorBrush[] _backgroundBrushesDark;

        private SolidColorBrush _borderBrush;
        private SolidColorBrush _backgroundBrush;

        public SolidColorBrush BorderBrush
        {
            get
            {
                if (_borderBrush == null)
                {
                    int num = ((base.ActiveGraphColumn >= 0) ? base.ActiveGraphColumn : 0);
                    _borderBrush = ForkPlusSettings.Default.Theme.IsDarkBase()
                        ? _borderBrushesDark[num % _borderBrushesDark.Length]
                        : _borderBrushesLight[num % _borderBrushesLight.Length];
                }
                return _borderBrush;
            }
            set
            {
                if (_borderBrush != value)
                {
                    _borderBrush = value;
                    RaisePropertyChanged(BorderBrushChangedEventArgs);
                }
            }
        }

        public SolidColorBrush BackgroundBrush
        {
            get
            {
                if (_backgroundBrush == null)
                {
                    int num = ((base.ActiveGraphColumn >= 0) ? base.ActiveGraphColumn : 0);
                    _backgroundBrush = ForkPlusSettings.Default.Theme.IsDarkBase()
                        ? _backgroundBrushesDark[num % _backgroundBrushesDark.Length]
                        : _backgroundBrushesLight[num % _backgroundBrushesLight.Length];
                }
                return _backgroundBrush;
            }
            set
            {
                if (_backgroundBrush != value)
                {
                    _backgroundBrush = value;
                    RaisePropertyChanged(BackgroundBrushChangedEventArgs);
                }
            }
        }

        static BranchViewModel()
        {
            // 对照 WPF: ColorConverter.ConvertFromString("#FF9502") → Avalonia: Color.Parse("#FF9502")
            _borderBrushesLight = new SolidColorBrush[13]
            {
                new SolidColorBrush(Color.Parse("#FF9502")),
                new SolidColorBrush(Color.Parse("#FFCC00")),
                new SolidColorBrush(Color.Parse("#FF3B30")),
                new SolidColorBrush(Color.Parse("#A2845E")),
                new SolidColorBrush(Color.Parse("#64DA38")),
                new SolidColorBrush(Color.Parse("#1CADF8")),
                new SolidColorBrush(Color.Parse("#CB73E1")),
                new SolidColorBrush(Color.Parse("#8E8E91")),
                new SolidColorBrush(Color.Parse("#FF2968")),
                new SolidColorBrush(Color.Parse("#30D5C8")),
                new SolidColorBrush(Color.Parse("#5856D6")),
                new SolidColorBrush(Color.Parse("#B4D435")),
                new SolidColorBrush(Color.Parse("#FF6F61"))
            };
            _borderBrushesDark = new SolidColorBrush[13]
            {
                new SolidColorBrush(Color.Parse("#F28B1F")),
                new SolidColorBrush(Color.Parse("#BF9A2F")),
                new SolidColorBrush(Color.Parse("#CB2327")),
                new SolidColorBrush(Color.Parse("#A68357")),
                new SolidColorBrush(Color.Parse("#27A649")),
                new SolidColorBrush(Color.Parse("#0082BA")),
                new SolidColorBrush(Color.Parse("#9D53A0")),
                new SolidColorBrush(Color.Parse("#6D6D6E")),
                new SolidColorBrush(Color.Parse("#CC1F56")),
                new SolidColorBrush(Color.Parse("#20A89E")),
                new SolidColorBrush(Color.Parse("#4A48B0")),
                new SolidColorBrush(Color.Parse("#8DAA28")),
                new SolidColorBrush(Color.Parse("#E05A4D"))
            };
            _backgroundBrushesLight = new SolidColorBrush[13]
            {
                new SolidColorBrush(Color.Parse("#FFF2DD")),
                new SolidColorBrush(Color.Parse("#FFF9DC")),
                new SolidColorBrush(Color.Parse("#FFE5E4")),
                new SolidColorBrush(Color.Parse("#F3F0EA")),
                new SolidColorBrush(Color.Parse("#E9FBE4")),
                new SolidColorBrush(Color.Parse("#DFF5FF")),
                new SolidColorBrush(Color.Parse("#FAECFC")),
                new SolidColorBrush(Color.Parse("#F1F1F1")),
                new SolidColorBrush(Color.Parse("#FFE3EC")),
                new SolidColorBrush(Color.Parse("#DEF7F5")),
                new SolidColorBrush(Color.Parse("#E5E5F7")),
                new SolidColorBrush(Color.Parse("#F2F9DE")),
                new SolidColorBrush(Color.Parse("#FFE5E2"))
            };
            _backgroundBrushesDark = new SolidColorBrush[13]
            {
                new SolidColorBrush(Color.Parse("#5D3D14")),
                new SolidColorBrush(Color.Parse("#5B4C0E")),
                new SolidColorBrush(Color.Parse("#5F2425")),
                new SolidColorBrush(Color.Parse("#433A32")),
                new SolidColorBrush(Color.Parse("#285224")),
                new SolidColorBrush(Color.Parse("#13445B")),
                new SolidColorBrush(Color.Parse("#503455")),
                new SolidColorBrush(Color.Parse("#3D3D3F")),
                new SolidColorBrush(Color.Parse("#601E35")),
                new SolidColorBrush(Color.Parse("#1A4A45")),
                new SolidColorBrush(Color.Parse("#272650")),
                new SolidColorBrush(Color.Parse("#3D4E1A")),
                new SolidColorBrush(Color.Parse("#5D2A24"))
            };
            // 对照 WPF: brush.Freeze() — Avalonia 无 Freeze，跳过
        }

        public BranchViewModel(int graphColumn) : base(graphColumn)
        {
        }

        public void RefreshBrushes()
        {
            _borderBrush = null;
            _backgroundBrush = null;
        }
    }
}
