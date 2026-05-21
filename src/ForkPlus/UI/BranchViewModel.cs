using System.ComponentModel;
using System.Windows.Media;
using ForkPlus.Settings;

namespace ForkPlus.UI
{
	public abstract class BranchViewModel : ReferenceViewModel
	{
		private static readonly PropertyChangedEventArgs BorderBrushChangedEventArgs;

		private static readonly PropertyChangedEventArgs BackgroundBrushChangedEventArgs;

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
					_borderBrush = ((ForkPlusSettings.Default.Theme == ThemeType.Dark) ? (_borderBrush = _borderBrushesDark[num % _borderBrushesDark.Length]) : (_borderBrush = _borderBrushesLight[num % _borderBrushesLight.Length]));
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
					_backgroundBrush = ((ForkPlusSettings.Default.Theme == ThemeType.Dark) ? (_backgroundBrush = _backgroundBrushesDark[num % _backgroundBrushesDark.Length]) : (_backgroundBrush = _backgroundBrushesLight[num % _backgroundBrushesLight.Length]));
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
			BorderBrushChangedEventArgs = new PropertyChangedEventArgs("BorderBrush");
			BackgroundBrushChangedEventArgs = new PropertyChangedEventArgs("BackgroundBrush");
			_borderBrushesLight = new SolidColorBrush[13]
			{
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9502")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFCC00")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF3B30")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A2845E")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64DA38")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1CADF8")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CB73E1")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8E8E91")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF2968")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#30D5C8")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5856D6")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B4D435")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF6F61"))
			};
			_borderBrushesDark = new SolidColorBrush[13]
			{
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F28B1F")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BF9A2F")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CB2327")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A68357")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#27A649")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0082BA")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#9D53A0")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#6D6D6E")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CC1F56")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#20A89E")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A48B0")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#8DAA28")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E05A4D"))
			};
			_backgroundBrushesLight = new SolidColorBrush[13]
			{
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF2DD")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF9DC")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFE5E4")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F3F0EA")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E9FBE4")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DFF5FF")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FAECFC")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F1F1")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFE3EC")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DEF7F5")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E5E5F7")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F2F9DE")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFE5E2"))
			};
			_backgroundBrushesDark = new SolidColorBrush[13]
			{
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5D3D14")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5B4C0E")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5F2425")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#433A32")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#285224")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#13445B")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#503455")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3D3D3F")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#601E35")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A4A45")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#272650")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#3D4E1A")),
				new SolidColorBrush((Color)ColorConverter.ConvertFromString("#5D2A24"))
			};
			SolidColorBrush[] borderBrushesLight = _borderBrushesLight;
			for (int i = 0; i < borderBrushesLight.Length; i++)
			{
				borderBrushesLight[i].Freeze();
			}
			borderBrushesLight = _borderBrushesDark;
			for (int i = 0; i < borderBrushesLight.Length; i++)
			{
				borderBrushesLight[i].Freeze();
			}
			borderBrushesLight = _backgroundBrushesLight;
			for (int i = 0; i < borderBrushesLight.Length; i++)
			{
				borderBrushesLight[i].Freeze();
			}
			borderBrushesLight = _backgroundBrushesDark;
			for (int i = 0; i < borderBrushesLight.Length; i++)
			{
				borderBrushesLight[i].Freeze();
			}
		}

		public BranchViewModel(int graphColumn)
			: base(graphColumn)
		{
		}

		public void RefreshBrushes()
		{
			_borderBrush = null;
			_backgroundBrush = null;
		}
	}
}
