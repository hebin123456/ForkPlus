using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Controls.Metadata;
using Avalonia.Interactivity;

namespace ForkPlus.UI.Controls
{
	// 阶段 4.5：WPF TemplatePart 特性在 Avalonia 同样存在（Avalonia.Controls.Primitives.TemplatedControl）。
	// GetTemplateChild / OnApplyTemplate 在 Avalonia 11 中保留，签名兼容 WPF。
	[TemplatePart(Name = "PART_ClearButton", Type = typeof(Button))]
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

		// 阶段 4.5：WPF DependencyProperty.Register → Avalonia StyledProperty.Register。
		public static readonly StyledProperty<Grid> AnimationPlaceholderProperty =
			AvaloniaProperty.Register<FilterTextBox, Grid>(nameof(AnimationPlaceholder));

		public static readonly StyledProperty<bool> UseSecondaryTextBoxBackgroundProperty =
			AvaloniaProperty.Register<FilterTextBox, bool>(nameof(UseSecondaryTextBoxBackground));

		public static readonly StyledProperty<bool> ShowDropdownProperty =
			AvaloniaProperty.Register<FilterTextBox, bool>(nameof(ShowDropdown));

		public static readonly StyledProperty<string> HintProperty =
			AvaloniaProperty.Register<FilterTextBox, string>(nameof(Hint));

		public string FilterRequest => base.Text;

		public bool IsAnimationPlaceholderVisible { get; private set; }

		public Grid AnimationPlaceholder
		{
			get => GetValue(AnimationPlaceholderProperty);
			set => SetValue(AnimationPlaceholderProperty, value);
		}

		public bool UseSecondaryTextBoxBackground
		{
			get => GetValue(UseSecondaryTextBoxBackgroundProperty);
			set => SetValue(UseSecondaryTextBoxBackgroundProperty, value);
		}

		public bool ShowDropdown
		{
			get => GetValue(ShowDropdownProperty);
			set => SetValue(ShowDropdownProperty, value);
		}

		public string Hint
		{
			get => GetValue(HintProperty);
			set => SetValue(HintProperty, value);
		}

		public event EventHandler FilterRequestChanged;

		public event EventHandler DropdownContextMenuOpened;

		public event EventHandler ClearButtonClicked;

		public FilterTextBox()
		{
			// 阶段 4.5：WPF PreviewKeyDown (tunneling) → Avalonia KeyDown。Avalonia 事件路由是 bubbling，
			// 没有 Preview 变体；这里只需在普通 KeyDown 中处理即可，因为 _dropdownButton 在模板加载后才存在。
			base.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Down && _dropdownButton != null)
				{
					_dropdownButton.IsChecked = true;
				}
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
			if (_dropdownButton?.ContextMenu != null)
			{
				_dropdownButton.ContextMenu.Opened += delegate(object s, EventArgs e)
				{
					this.DropdownContextMenuOpened?.Invoke(s, e);
				};
			}
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
				_dropdownButton?.Show();
				_iconImage?.Collapse();
			}
			else
			{
				_dropdownButton?.Collapse();
				_iconImage?.Show();
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

		// 阶段 4.5：WPF BeginAnimation(UIElement.OpacityProperty, DoubleAnimation)
		// → Avalonia Animation.RunAsync。Avalonia 用 KeyFrame + Cue 描述关键帧，
		// 取代 WPF 的 From/To/Duration 三元组。返回的 Task 丢弃即可（fire-and-forget）。
		private void UpdateOpacity(double from, double to, TimeSpan duration)
		{
			Animation animation = new Animation
			{
				Duration = duration,
				IterationCount = new IterationCount(1),
				PlaybackDirection = PlaybackDirection.Normal,
				FillMode = FillMode.Forward
			};
			KeyFrame fromFrame = new KeyFrame
			{
				Cue = new Cue(0.0)
			};
			fromFrame.Setters.Add(new Setter(Visual.OpacityProperty, from));
			KeyFrame toFrame = new KeyFrame
			{
				Cue = new Cue(1.0)
			};
			toFrame.Setters.Add(new Setter(Visual.OpacityProperty, to));
			animation.Children.Add(fromFrame);
			animation.Children.Add(toFrame);
			_ = animation.RunAsync(this);
		}
	}
}
