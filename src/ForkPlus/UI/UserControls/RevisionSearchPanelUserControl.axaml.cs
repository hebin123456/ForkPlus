using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup;
using Avalonia.Media;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Helpers;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls
{
	public partial class RevisionSearchPanelUserControl : UserControl
	{
		private static readonly double SearchPanelHeight = 30.0;

		public static readonly global::Avalonia.StyledProperty<Grid> SearchPanelPlaceholderProperty =
    global::Avalonia.AvaloniaProperty.Register<RevisionSearchPanelUserControl, Grid>("SearchPanelPlaceholder");

		private bool _isBusyIndicatorVisible;

             // Migration note：TranslateTransform 非 StyledElement，NameGenerator 不生成字段，
             // 手动声明（字段名与类型同名，C# "Color Color" 规则下成员访问优先）。
             internal global::Avalonia.Media.TranslateTransform TranslateTransform;

		public Grid SearchPanelPlaceholder
		{
			get
			{
				return (Grid)GetValue(SearchPanelPlaceholderProperty);
			}
			set
			{
				SetValue(SearchPanelPlaceholderProperty, value);
			}
		}

		public string SearchString => SearchTextBox.Text.Trim();

		public bool IsTextBoxFocused => SearchTextBox.IsFocused;

		public bool IsBusyIndicatorVisible
		{
			get
			{
				return _isBusyIndicatorVisible;
			}
			set
			{
				_isBusyIndicatorVisible = value;
				RefreshBusyIndicator();
			}
		}

		public bool IsSearchBarVisible { get; private set; }

		public event EventHandler SearchQueryChanged;

		public event EventHandler JumpToPreviousSearchResult;

		public event EventHandler JumpToNextSearchResult;

		public event EventHandler SearchSubmitted;

		public event EventHandler Closed;

		public RevisionSearchPanelUserControl()
		{
			InitializeComponent();
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			TranslateTransform = (global::Avalonia.Media.TranslateTransform)((global::Avalonia.Controls.Border)base.Content).RenderTransform;
			base.Loaded += delegate
			{
				TranslateTransform.Y = 0.0 - SearchPanelHeight;
				SearchPanelPlaceholder.Height = 0.0;
			};
			SearchTextBox.AddHandler(global::Avalonia.Input.InputElement.KeyDownEvent,delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Return || e.Key == Key.F3)
				{
					if (e.Key == Key.Return)
					{
						this.SearchSubmitted?.Invoke(this, EventArgs.Empty);
					}
					if (KeyboardHelper.IsShiftDown)
					{
						this.JumpToPreviousSearchResult?.Invoke(this, EventArgs.Empty);
					}
					else
					{
						this.JumpToNextSearchResult?.Invoke(this, EventArgs.Empty);
					}
					e.Handled = true;
				}
			},global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
		}

		public void ShowSearchBar()
		{
			if (SlidingPanelHelper.ShowPanel(SearchPanelPlaceholder, TranslateTransform, SearchPanelHeight))
			{
				SearchTextBox.Clear();
			}
			SearchTextBox.SelectAll();
			SearchTextBox.Focus();
			IsSearchBarVisible = true;
		}

		public void HideSearchBar()
		{
			if (IsSearchBarVisible)
			{
				SlidingPanelHelper.HidePanel(SearchPanelPlaceholder, TranslateTransform, SearchPanelHeight);
				this.Closed?.Invoke(this, EventArgs.Empty);
				IsSearchBarVisible = false;
			}
		}

		// v5.3.0（搜索进度显示）：当前匹配总数缓存——UpdateCurrentMatchIndex 需要它拼
		// "当前索引/总数"进度文案；MatchesTextBlock 由两者共用刷新。
		private int? _matchCount;

		public void UpdateMatchesCount(int? matches)
		{
			_matchCount = matches;
			UpdateMatchesText(null);
		}

		// v5.3.0：跳转到第 currentIndex 个匹配（1-based）后更新"匹配 {current}/{total}"
		// 进度；传 null 时回退为只显示总数（用户手动选择非匹配行等场景）。
		public void UpdateCurrentMatchIndex(int? currentIndex)
		{
			UpdateMatchesText(currentIndex);
		}

		private void UpdateMatchesText(int? currentIndex)
		{
			if (!_matchCount.HasValue)
			{
				MatchesTextBlock.Text = "";
				return;
			}
			int total = _matchCount.GetValueOrDefault();
			// Migration note：原文案硬编码英文（"{n} match(es)"），v5.3.0 起走本地化
			//（key 即英文文案，en 无需词条；各语言 json 见 Languages/*.json）。
			if (currentIndex.HasValue && currentIndex.Value >= 1 && currentIndex.Value <= total)
			{
				MatchesTextBlock.Text = PreferencesLocalization.FormatCurrent("{0}/{1} matches", currentIndex.Value, total);
			}
			else if (total == 1)
			{
				MatchesTextBlock.Text = PreferencesLocalization.FormatCurrent("1 match");
			}
			else
			{
				MatchesTextBlock.Text = PreferencesLocalization.FormatCurrent("{0} matches", total);
			}
		}

		private void SearchTextBox_TextChanged(object sender, TextChangedEventArgs e)
		{
			this.SearchQueryChanged?.Invoke(this, EventArgs.Empty);
			RefreshBusyIndicator();
		}

		private void CloseSearchContainerButton_Click(object sender, RoutedEventArgs e)
		{
			HideSearchBar();
		}

		private void JumpToNextSearchResultButton_Click(object sender, RoutedEventArgs e)
		{
			this.JumpToNextSearchResult?.Invoke(this, EventArgs.Empty);
		}

		private void JumpToPreviousSearchResultButton_Click(object sender, RoutedEventArgs e)
		{
			this.JumpToPreviousSearchResult?.Invoke(this, EventArgs.Empty);
		}

		private void RefreshBusyIndicator()
		{
			if (_isBusyIndicatorVisible)
			{
				BusyIndicator.Show();
				SearchTextBox.Padding = new Thickness(2.0, 1.0, 18.0, 1.0);
			}
			else
			{
				BusyIndicator.Collapse();
				SearchTextBox.Padding = new Thickness(2.0, 1.0, 2.0, 1.0);
			}
		}

	}
}
