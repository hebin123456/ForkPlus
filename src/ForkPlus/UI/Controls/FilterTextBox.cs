using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using global::Avalonia.Animation;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;

namespace ForkPlus.UI.Controls
{
	[global::Avalonia.Controls.Metadata.TemplatePartAttribute(Name = "PART_ClearButton", Type = typeof(global::Avalonia.Controls.Control))]
	[global::Avalonia.Controls.Metadata.TemplatePartAttribute(Name = "PART_TranslateTransform", Type = typeof(TranslateTransform))]
	[global::Avalonia.Controls.Metadata.TemplatePartAttribute(Name = "PART_DropDownButton", Type = typeof(DropDownButton))]
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

		public static readonly global::Avalonia.StyledProperty<Grid> AnimationPlaceholderProperty =
    global::Avalonia.AvaloniaProperty.Register<FilterTextBox, Grid>("AnimationPlaceholder", null);

		public static readonly global::Avalonia.StyledProperty<bool> UseSecondaryTextBoxBackgroundProperty =
    global::Avalonia.AvaloniaProperty.Register<FilterTextBox, bool>("UseSecondaryTextBoxBackground", false);

		public static readonly global::Avalonia.StyledProperty<bool> ShowDropdownProperty =
    global::Avalonia.AvaloniaProperty.Register<FilterTextBox, bool>("ShowDropdown", false);

		public static readonly global::Avalonia.StyledProperty<string> HintProperty =
    global::Avalonia.AvaloniaProperty.Register<FilterTextBox, string>("Hint", null);

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

		public event EventHandler EnterPressed;

		public FilterTextBox()
		{
			base.AddHandler(global::Avalonia.Input.InputElement.KeyDownEvent,delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Down)
				{
					if (_dropdownButton != null)
					{
						_dropdownButton.IsChecked = true;
					}
				}
			},global::Avalonia.Interactivity.RoutingStrategies.Tunnel | global::Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);
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

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (HandleEnterKey(e))
			{
				return;
			}
			base.OnKeyDown(e);
		}

		private bool HandleEnterKey(KeyEventArgs e)
		{
			if (e.Key != Key.Return && e.Key != Key.Enter)
			{
				return false;
			}
			e.Handled = true;
			EnterPressed?.Invoke(this, EventArgs.Empty);
			return true;
		}

		protected override void OnApplyTemplate(global::Avalonia.Controls.Primitives.TemplateAppliedEventArgs e)
		{
			base.OnApplyTemplate(e);
			if (base.Placeholder == "Filter")
			{
				base.Placeholder = PreferencesLocalization.Translate("Filter", ForkPlusSettings.Default.UiLanguage);
			}
			_iconImage = this.GetTemplateChild("PART_Icon") as Image;
			_dropdownButton = this.GetTemplateChild("PART_DropDownButton") as DropDownButton;
			_dropdownButton.ContextMenu.Opened += delegate(object s, RoutedEventArgs e)
			{
				this.DropdownContextMenuOpened?.Invoke(s, e);
			};
			_clearButton = this.GetTemplateChild("PART_ClearButton") as Button;
			if (_clearButton != null)
			{
				_clearButton.Click += ClearButton_Click;
			}
			_translateTransform = this.GetTemplateChild("PART_TranslateTransform") as TranslateTransform;
			if (AnimationPlaceholder != null && _translateTransform != null && !IsAnimationPlaceholderVisible)
			{
				_translateTransform.Y = 0.0 - FilterTextBoxAnimationHeight;
				AnimationPlaceholder.Height = 0.0;
				base.Opacity = 0.0;
			}
			if (UseSecondaryTextBoxBackground)
			{
				base.Background = global::ForkPlus.UI.Theme.FilterPanelSecondaryBackground;
				base.BorderBrush = global::ForkPlus.UI.Theme.FilterPanelSecondaryBorder;
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
				bool changed;
				if (_translateTransform == null)
				{
					changed = AnimationPlaceholder.Height != FilterTextBoxAnimationHeight;
					AnimationPlaceholder.Height = FilterTextBoxAnimationHeight;
					Opacity = 1.0;
				}
				else
				{
					changed = SlidingPanelHelper.ShowPanel(AnimationPlaceholder, _translateTransform, FilterTextBoxAnimationHeight);
				}
				if (changed)
				{
					Clear();
				}
				if (_translateTransform != null)
				{
					UpdateOpacity(0.0, 1.0, ShowAnimationDuration);
				}
				FocusAndSelectAllText();
				IsAnimationPlaceholderVisible = true;
			}
		}

		public void HideWithAnimation()
		{
			if (AnimationPlaceholder != null && IsAnimationPlaceholderVisible)
			{
				Clear();
				if (_translateTransform == null)
				{
					AnimationPlaceholder.Height = 0.0;
					Opacity = 0.0;
				}
				else
				{
					SlidingPanelHelper.HidePanel(AnimationPlaceholder, _translateTransform, FilterTextBoxAnimationHeight);
					UpdateOpacity(1.0, 0.0, HideAnimationDuration);
				}
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
			global::ForkPlus.UI.WpfCompat.WpfAnimation.BeginAnimation(this,global::Avalonia.Input.InputElement.OpacityProperty,animation);
		}
	}
}
