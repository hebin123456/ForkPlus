using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace ForkPlus.UI.UserControls
{
	public partial class FallbackUserControl : UserControl
	{
		public static readonly DependencyProperty FallbackTitleProperty = DependencyProperty.RegisterAttached("FallbackTitle", typeof(string), typeof(FallbackUserControl));

		public static readonly DependencyProperty HideFallbackImageProperty = DependencyProperty.RegisterAttached("HideFallbackImage", typeof(bool), typeof(FallbackUserControl));

		public static readonly DependencyProperty FallbackMessageProperty = DependencyProperty.RegisterAttached("FallbackMessage", typeof(string), typeof(FallbackUserControl));

		public static readonly DependencyProperty IsMonospaceProperty = DependencyProperty.RegisterAttached("IsMonospace", typeof(bool), typeof(FallbackUserControl));

		public static readonly DependencyProperty Button1TitleProperty = DependencyProperty.RegisterAttached("Button1Title", typeof(string), typeof(FallbackUserControl));

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

		protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
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
