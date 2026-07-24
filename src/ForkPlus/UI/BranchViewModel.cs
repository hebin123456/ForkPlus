using System.ComponentModel;
using Avalonia.Media;
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
					_borderBrush = (ForkPlusSettings.Default.Theme.IsDarkBase() ? (_borderBrush = _borderBrushesDark[num % _borderBrushesDark.Length]) : (_borderBrush = _borderBrushesLight[num % _borderBrushesLight.Length]));
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
					_backgroundBrush = (ForkPlusSettings.Default.Theme.IsDarkBase() ? (_backgroundBrush = _backgroundBrushesDark[num % _backgroundBrushesDark.Length]) : (_backgroundBrush = _backgroundBrushesLight[num % _backgroundBrushesLight.Length]));
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
			// Avalonia SolidColorBrush 没有 Freeze() 方法（WPF 概念）。
			// 这些画刷创建后存储在 static readonly 字段中且不再修改，
			// 因此无需冻结即可安全使用。
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
