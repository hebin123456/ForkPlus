using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls.BinaryDiff
{
	/// <summary>
	/// v4.3.2：动图播放控制条——播放/暂停、上/下一帧、播放速度、帧序号。
	/// 悬浮在并排视图各列图片区域底部居中，仅该侧为动图时显示（由 BinaryContentUserControl 控制）。
	/// 用代码构建（无 XAML），便于单测直接按名字取控件断言。
	/// </summary>
	public class AnimatedImagePlaybackBar : UserControl
	{
		private const string PlayGlyph = "\u25b6";

		private const string PauseGlyph = "\u275a\u275a";

		private const string PrevGlyph = "\u25c0";

		private const string NextGlyph = "\u25b6";

		private readonly Button _playPauseButton;

		private readonly Button _prevFrameButton;

		private readonly Button _nextFrameButton;

		private readonly ComboBox _speedComboBox;

		private readonly TextBlock _frameTextBlock;

		private bool _updatingSpeed;

		[Null]
		private AnimatedImagePlayer _player;

		public AnimatedImagePlaybackBar()
		{
			_playPauseButton = CreateButton(PlayGlyph, "Play", PlayPauseButton_Click);
			_playPauseButton.Name = "PlayPauseButton";
			_prevFrameButton = CreateButton(PrevGlyph, "Previous Frame", PrevFrameButton_Click);
			_prevFrameButton.Name = "PrevFrameButton";
			_nextFrameButton = CreateButton(NextGlyph, "Next Frame", NextFrameButton_Click);
			_nextFrameButton.Name = "NextFrameButton";
			_speedComboBox = new ComboBox
			{
				Name = "SpeedComboBox",
				Width = 74,
				Height = 28,
				MinHeight = 28,
				Padding = new Thickness(6.0, 0.0, 0.0, 0.0),
				VerticalAlignment = VerticalAlignment.Center,
				ItemsSource = new string[3] { "0.5\u00d7", "1\u00d7", "2\u00d7" },
				SelectedIndex = 1
			};
			ToolTip.SetTip(_speedComboBox, PreferencesLocalization.Current("Playback Speed"));
			_speedComboBox.SelectionChanged += SpeedComboBox_SelectionChanged;
			_frameTextBlock = new TextBlock
			{
				Name = "FrameTextBlock",
				Foreground = Brushes.White,
				FontSize = 12.0,
				MinWidth = 42.0,
				Margin = new Thickness(4.0, 0.0, 2.0, 0.0),
				VerticalAlignment = VerticalAlignment.Center,
				TextAlignment = TextAlignment.Center
			};
			StackPanel stackPanel = new StackPanel
			{
				Orientation = Orientation.Horizontal,
				Spacing = 2.0,
				VerticalAlignment = VerticalAlignment.Center
			};
			stackPanel.Children.Add(_playPauseButton);
			stackPanel.Children.Add(_prevFrameButton);
			stackPanel.Children.Add(_nextFrameButton);
			stackPanel.Children.Add(_frameTextBlock);
			stackPanel.Children.Add(_speedComboBox);
			Border content = new Border
			{
				Background = new SolidColorBrush(Color.FromArgb(179, 0, 0, 0)),
				BorderBrush = new SolidColorBrush(Color.FromArgb(64, 255, 255, 255)),
				BorderThickness = new Thickness(1.0),
				CornerRadius = new CornerRadius(14.0),
				Padding = new Thickness(6.0, 2.0, 6.0, 2.0),
				Child = stackPanel
			};
			base.Content = content;
		}

		public Button PlayPauseButton => _playPauseButton;

		public Button PrevFrameButton => _prevFrameButton;

		public Button NextFrameButton => _nextFrameButton;

		public ComboBox SpeedComboBox => _speedComboBox;

		public TextBlock FrameTextBlock => _frameTextBlock;

		[Null]
		public AnimatedImagePlayer Player
		{
			get
			{
				return _player;
			}
			set
			{
				if (ReferenceEquals(_player, value))
				{
					return;
				}
				if (_player != null)
				{
					_player.FrameChanged -= OnPlayerFrameChanged;
					_player.PlayStateChanged -= OnPlayerPlayStateChanged;
				}
				_player = value;
				if (_player != null)
				{
					_player.FrameChanged += OnPlayerFrameChanged;
					_player.PlayStateChanged += OnPlayerPlayStateChanged;
				}
				UpdateSpeedSelection();
				RefreshPlayGlyph();
				RefreshFrameText();
			}
		}

		/// <summary>语言切换后刷新按钮提示（播放/暂停提示随当前状态）。</summary>
		public void ApplyLocalization()
		{
			ToolTip.SetTip(_speedComboBox, PreferencesLocalization.Current("Playback Speed"));
			ToolTip.SetTip(_playPauseButton, PreferencesLocalization.Current(GetPlayGlyph() == PauseGlyph ? "Pause" : "Play"));
			ToolTip.SetTip(_prevFrameButton, PreferencesLocalization.Current("Previous Frame"));
			ToolTip.SetTip(_nextFrameButton, PreferencesLocalization.Current("Next Frame"));
		}

		private static Button CreateButton(string glyph, string toolTip, EventHandler<RoutedEventArgs> onClick)
		{
			Button button = new Button
			{
				Content = glyph,
				Width = 30,
				Height = 28,
				MinWidth = 30,
				MinHeight = 28,
				Padding = new Thickness(0.0),
				Background = Brushes.Transparent,
				BorderThickness = new Thickness(0.0),
				Foreground = Brushes.White,
				FontSize = 12.0,
				HorizontalContentAlignment = HorizontalAlignment.Center,
				VerticalContentAlignment = VerticalAlignment.Center,
				VerticalAlignment = VerticalAlignment.Center
			};
			ToolTip.SetTip(button, PreferencesLocalization.Current(toolTip));
			button.Click += onClick;
			return button;
		}

		private void PlayPauseButton_Click(object sender, RoutedEventArgs e)
		{
			_player?.TogglePlay();
		}

		private void PrevFrameButton_Click(object sender, RoutedEventArgs e)
		{
			_player?.StepBackward();
		}

		private void NextFrameButton_Click(object sender, RoutedEventArgs e)
		{
			_player?.StepForward();
		}

		private void SpeedComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_updatingSpeed || _player == null)
			{
				return;
			}
			int index = _speedComboBox.SelectedIndex;
			if (index >= 0 && index < AnimatedImagePlayer.SpeedOptions.Length)
			{
				_player.Speed = AnimatedImagePlayer.SpeedOptions[index];
			}
		}

		private void OnPlayerFrameChanged(object sender, EventArgs e)
		{
			RefreshFrameText();
		}

		private void OnPlayerPlayStateChanged(object sender, EventArgs e)
		{
			RefreshPlayGlyph();
		}

		private void UpdateSpeedSelection()
		{
			if (_player == null)
			{
				return;
			}
			int index = Array.IndexOf(AnimatedImagePlayer.SpeedOptions, _player.Speed);
			if (index < 0)
			{
				return;
			}
			_updatingSpeed = true;
			_speedComboBox.SelectedIndex = index;
			_updatingSpeed = false;
		}

		private void RefreshPlayGlyph()
		{
			string glyph = GetPlayGlyph();
			_playPauseButton.Content = glyph;
			ToolTip.SetTip(_playPauseButton, PreferencesLocalization.Current((glyph == PauseGlyph) ? "Pause" : "Play"));
		}

		private string GetPlayGlyph()
		{
			return ((_player != null && _player.IsPlaying) ? PauseGlyph : PlayGlyph);
		}

		private void RefreshFrameText()
		{
			if (_player == null)
			{
				_frameTextBlock.Text = "";
				return;
			}
			_frameTextBlock.Text = (_player.CurrentFrame + 1) + "/" + _player.FrameCount;
		}
	}
}