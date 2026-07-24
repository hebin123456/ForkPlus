// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Interactivity（RoutedEventArgs/RoutedEventHandler）
// - using System.Windows.Controls → using Avalonia.Controls
// - using System.Windows.Markup → 移除
// - TextAlignment → Avalonia.Media.TextAlignment（需 using Avalonia.Media）
// - HorizontalAlignment → Avalonia.Layout.HorizontalAlignment（需 using Avalonia.Layout）
// - DependencyProperty.RegisterAttached → StyledProperty<T> + AvaloniaProperty.Register<TOwner, TType>
//   （这些属性仅在 FallbackUserControl 自身用 GetValue/SetValue 访问，非真正附加属性，故用 StyledProperty）
// - DependencyPropertyChangedEventArgs → AvaloniaPropertyChangedEventArgs
// - OnPropertyChanged(DependencyPropertyChangedEventArgs) → OnPropertyChanged(AvaloniaPropertyChangedEventArgs)
// - e.Property.Name == "X" → e.Property == XProperty（直接比较属性字段，参考 FileContentControl）
using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;

namespace ForkPlus.UI.UserControls
{
	public partial class FallbackUserControl : UserControl
	{
		// 阶段 4.5：WPF DependencyProperty.RegisterAttached → Avalonia StyledProperty + AvaloniaProperty.Register<TOwner, TType>。
		public static readonly StyledProperty<string> FallbackTitleProperty = AvaloniaProperty.Register<FallbackUserControl, string>(nameof(FallbackTitle));

		public static readonly StyledProperty<bool> HideFallbackImageProperty = AvaloniaProperty.Register<FallbackUserControl, bool>(nameof(HideFallbackImage));

		public static readonly StyledProperty<string> FallbackMessageProperty = AvaloniaProperty.Register<FallbackUserControl, string>(nameof(FallbackMessage));

		public static readonly StyledProperty<bool> IsMonospaceProperty = AvaloniaProperty.Register<FallbackUserControl, bool>(nameof(IsMonospace));

		public static readonly StyledProperty<string> Button1TitleProperty = AvaloniaProperty.Register<FallbackUserControl, string>(nameof(Button1Title));

		public string FallbackTitle
		{
			get
			{
				return (string)GetValue(FallbackTitleProperty);
			}
			set
			{
				SetValue(FallbackTitleProperty, value);
			}
		}

		public bool HideFallbackImage
		{
			get
			{
				return (bool)GetValue(HideFallbackImageProperty);
			}
			set
			{
				SetValue(HideFallbackImageProperty, value);
			}
		}

		public string FallbackMessage
		{
			get
			{
				return (string)GetValue(FallbackMessageProperty);
			}
			set
			{
				SetValue(FallbackMessageProperty, value);
			}
		}

		public bool IsMonospace
		{
			get
			{
				return (bool)GetValue(IsMonospaceProperty);
			}
			set
			{
				SetValue(IsMonospaceProperty, value);
			}
		}

		public string Button1Title
		{
			get
			{
				return (string)GetValue(Button1TitleProperty);
			}
			set
			{
				SetValue(Button1TitleProperty, value);
			}
		}

		public double FallbackMessageFontSize
		{
			get
			{
				return FallbackMessageTextBlock.FontSize;
			}
			set
			{
				FallbackMessageTextBlock.FontSize = value;
			}
		}

		public event RoutedEventHandler Button1Click;

		public FallbackUserControl()
		{
			InitializeComponent();
		}

		public void ResetEvents()
		{
			this.Button1Click = null;
		}

		// 阶段 4.5：WPF OnPropertyChanged(DependencyPropertyChangedEventArgs) → Avalonia OnPropertyChanged(AvaloniaPropertyChangedEventArgs)。
		// e.Property.Name == "X" → e.Property == XProperty（参考 FileContentControl）。
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (e.Property == FallbackTitleProperty)
			{
				if (string.IsNullOrWhiteSpace(FallbackTitle))
				{
					FallbackTitleTextBlock.Collapse();
					return;
				}
				FallbackTitleTextBlock.Text = FallbackTitle;
				FallbackTitleTextBlock.Show();
			}
			else if (e.Property == FallbackMessageProperty)
			{
				if (string.IsNullOrWhiteSpace(FallbackMessage))
				{
					FallbackMessageTextBlock.Collapse();
					return;
				}
				FallbackMessageTextBlock.Text = FallbackMessage;
				FallbackMessageTextBlock.Show();
			}
			else if (e.Property == IsMonospaceProperty)
			{
				FallbackMessageTextBlock.FontFamily = FontConstants.MonospaceFontFamily;
				FallbackMessageTextBlock.FontSize = 14.0;
				FallbackMessageTextBlock.TextAlignment = TextAlignment.Left;
				FallbackMessageTextBlock.HorizontalAlignment = HorizontalAlignment.Left;
			}
			else if (e.Property == Button1TitleProperty)
			{
				if (string.IsNullOrWhiteSpace(Button1Title))
				{
					Button1.Collapse();
					return;
				}
				Button1.Content = Button1Title;
				Button1.Show();
			}
			else if (e.Property == HideFallbackImageProperty)
			{
				if (HideFallbackImage)
				{
					FallbackImage.Collapse();
				}
				else
				{
					FallbackImage.Show();
				}
			}
		}

		private void Button1_Click(object sender, RoutedEventArgs e)
		{
			this.Button1Click?.Invoke(sender, e);
		}

	}
}
