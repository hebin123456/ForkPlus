using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Controls
{
	[TemplatePart(Name = "PART_ClearButton", Type = typeof(FrameworkElement))]
	[TemplatePart(Name = "PART_TranslateTransform", Type = typeof(TranslateTransform))]
	[TemplatePart(Name = "PART_DropDownButton", Type = typeof(DropDownButton))]
	public class FilterTextBox : PlaceholderTextBox
	{
		private static readonly double FilterTextBoxAnimationHeight = 30.0;

		private static readonly TimeSpan ShowAnimationDuration = TimeSpan.FromSeconds(0.1);

		private static readonly TimeSpan HideAnimationDuration = TimeSpan.FromSeconds(0.5);

		private const string PartNameClearButton = "PART_ClearButton";

		private const string PartNameIconImage = "PART_Icon";

		private const string PartNameTranslateTransform = "PART_TranslateTransform";

		private const string PartDropDownButton = "PART_DropDownButton";

		private Button _clearButton;

		private Image _iconImage;

		private TranslateTransform _translateTransform;

		private DropDownButton _dropdownButton;

		public static readonly DependencyProperty AnimationPlaceholderProperty = DependencyProperty.Register("AnimationPlaceholder", typeof(Grid), typeof(FilterTextBox), new PropertyMetadata(null));

		public static readonly DependencyProperty UseSecondaryTextBoxBackgroundProperty = DependencyProperty.Register("UseSecondaryTextBoxBackground", typeof(bool), typeof(FilterTextBox), new PropertyMetadata(false));

		public static readonly DependencyProperty ShowDropdownProperty = DependencyProperty.Register("ShowDropdown", typeof(bool), typeof(FilterTextBox), new PropertyMetadata(false));

		public static readonly DependencyProperty HintProperty = DependencyProperty.Register("Hint", typeof(string), typeof(FilterTextBox), new PropertyMetadata(null));

		public string FilterRequest => base.Text;

		public bool IsAnimationPlaceholderVisible { get; private set; }

		public Grid AnimationPlaceholder
		{
			get
			{
				return (Grid)GetValue(AnimationPlaceholderProperty);
			}
			set
			{
				SetValue(AnimationPlaceholderProperty, value);
			}
		}

		public bool UseSecondaryTextBoxBackground
		{
			get
			{
				return (bool)GetValue(UseSecondaryTextBoxBackgroundProperty);
			}
			set
			{
				SetValue(UseSecondaryTextBoxBackgroundProperty, value);
			}
		}

		public bool ShowDropdown
		{
			get
			{
				return (bool)GetValue(ShowDropdownProperty);
			}
			set
			{
				SetValue(ShowDropdownProperty, value);
			}
		}

		public string Hint
		{
			get
			{
				return (string)GetValue(HintProperty);
			}
			set
			{
				SetValue(HintProperty, value);
			}
		}

		public event EventHandler FilterRequestChanged;

		public event EventHandler DropdownContextMenuOpened;

		public event EventHandler ClearButtonClicked;

		public FilterTextBox()
		{
			base.PreviewKeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Down)
				{
					_dropdownButton.IsChecked = true;
				}
			};
			base.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Escape && !string.IsNullOrEmpty(base.Text))
				{
					Clear();
					e.Handled = true;
				}
			};
			base.TextChanged += delegate
			{
				this.FilterRequestChanged?.Invoke(this, EventArgs.Empty);
			};
		}

		public override void OnApplyTemplate()
		{
			base.OnApplyTemplate();
			if (base.Placeholder == "Filter")
			{
				base.Placeholder = PreferencesLocalization.Translate("Filter", ForkPlusSettings.Default.UiLanguage);
			}
			_iconImage = GetTemplateChild("PART_Icon") as Image;
			_dropdownButton = GetTemplateChild("PART_DropDownButton") as DropDownButton;
			_dropdownButton.ContextMenu.Opened += delegate(object s, RoutedEventArgs e)
			{
				this.DropdownContextMenuOpened?.Invoke(s, e);
			};
			_clearButton = GetTemplateChild("PART_ClearButton") as Button;
			if (_clearButton != null)
			{
				_clearButton.Click += ClearButton_Click;
			}
			_translateTransform = GetTemplateChild("PART_TranslateTransform") as TranslateTransform;
			if (AnimationPlaceholder != null && _translateTransform != null && !IsAnimationPlaceholderVisible)
			{
				_translateTransform.Y = 0.0 - FilterTextBoxAnimationHeight;
				AnimationPlaceholder.Height = 0.0;
				base.Opacity = 0.0;
			}
			if (UseSecondaryTextBoxBackground)
			{
				base.Background = Theme.FilterPanelSecondaryBackground;
				base.BorderBrush = Theme.FilterPanelSecondaryBorder;
			}
			if (ShowDropdown)
			{
				_dropdownButton.Show();
				_iconImage.Collapse();
			}
			else
			{
				_dropdownButton.Collapse();
				_iconImage.Show();
			}
		}

		public void FocusAndSelectAllText()
		{
			SelectAll();
			Focus();
		}

		public void ShowWithAnimation()
		{
			if (AnimationPlaceholder != null)
			{
				if (SlidingPanelHelper.ShowPanel(AnimationPlaceholder, _translateTransform, FilterTextBoxAnimationHeight))
				{
					Clear();
				}
				UpdateOpacity(0.0, 1.0, ShowAnimationDuration);
				FocusAndSelectAllText();
				IsAnimationPlaceholderVisible = true;
			}
		}

		public void HideWithAnimation()
		{
			if (AnimationPlaceholder != null && IsAnimationPlaceholderVisible)
			{
				Clear();
				SlidingPanelHelper.HidePanel(AnimationPlaceholder, _translateTransform, FilterTextBoxAnimationHeight);
				UpdateOpacity(1.0, 0.0, HideAnimationDuration);
				IsAnimationPlaceholderVisible = false;
			}
		}

		private void ClearButton_Click(object sender, RoutedEventArgs e)
		{
			if (AnimationPlaceholder != null)
			{
				HideWithAnimation();
			}
			else
			{
				Clear();
				Focus();
			}
			this.ClearButtonClicked?.Invoke(this, EventArgs.Empty);
		}

		private void UpdateOpacity(double from, double to, TimeSpan duration)
		{
			DoubleAnimation animation = new DoubleAnimation(from, to, duration);
			BeginAnimation(UIElement.OpacityProperty, animation);
		}
	}
}
