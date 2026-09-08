using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;

namespace ForkPlus.UI.UserControls
{
	public partial class FallbackUserControl : UserControl
	{
		public static readonly global::Avalonia.StyledProperty<string> FallbackTitleProperty =
    global::Avalonia.AvaloniaProperty.RegisterAttached<FallbackUserControl, global::Avalonia.AvaloniaObject, string>("FallbackTitle");

		public static readonly global::Avalonia.StyledProperty<bool> HideFallbackImageProperty =
    global::Avalonia.AvaloniaProperty.RegisterAttached<FallbackUserControl, global::Avalonia.AvaloniaObject, bool>("HideFallbackImage");

		public static readonly global::Avalonia.StyledProperty<string> FallbackMessageProperty =
    global::Avalonia.AvaloniaProperty.RegisterAttached<FallbackUserControl, global::Avalonia.AvaloniaObject, string>("FallbackMessage");

		public static readonly global::Avalonia.StyledProperty<bool> IsMonospaceProperty =
    global::Avalonia.AvaloniaProperty.RegisterAttached<FallbackUserControl, global::Avalonia.AvaloniaObject, bool>("IsMonospace");

		public static readonly global::Avalonia.StyledProperty<string> Button1TitleProperty =
    global::Avalonia.AvaloniaProperty.RegisterAttached<FallbackUserControl, global::Avalonia.AvaloniaObject, string>("Button1Title");

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

		public event EventHandler<RoutedEventArgs> Button1Click;

		public FallbackUserControl()
		{
			InitializeComponent();
		}

		public void ResetEvents()
		{
			this.Button1Click = null;
		}

		protected override void OnPropertyChanged(global::Avalonia.AvaloniaPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (e.Property.Name == "FallbackTitle")
			{
				if (string.IsNullOrWhiteSpace(FallbackTitle))
				{
					FallbackTitleTextBlock.Collapse();
					return;
				}
				FallbackTitleTextBlock.Text = FallbackTitle;
				FallbackTitleTextBlock.Show();
			}
			else if (e.Property.Name == "FallbackMessage")
			{
				if (string.IsNullOrWhiteSpace(FallbackMessage))
				{
					FallbackMessageTextBlock.Collapse();
					return;
				}
				FallbackMessageTextBlock.Text = FallbackMessage;
				FallbackMessageTextBlock.Show();
			}
			else if (e.Property.Name == "IsMonospace")
			{
				FallbackMessageTextBlock.FontFamily = FontConstants.MonospaceFontFamily;
				FallbackMessageTextBlock.FontSize = 14.0;
				FallbackMessageTextBlock.TextAlignment = TextAlignment.Left;
				FallbackMessageTextBlock.HorizontalAlignment = HorizontalAlignment.Left;
			}
			else if (e.Property.Name == "Button1Title")
			{
				if (string.IsNullOrWhiteSpace(Button1Title))
				{
					Button1.Collapse();
					return;
				}
				Button1.Content = Button1Title;
				Button1.Show();
			}
			else if (e.Property.Name == "HideFallbackImage")
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
