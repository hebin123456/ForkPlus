// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Interactivity（RoutedEventArgs）
// - using System.Windows.Controls → using Avalonia.Controls（Grid/TextChangedEventArgs）
// - using System.Windows.Input → using Avalonia.Input（Key/KeyEventArgs）
// - using System.Windows.Markup → 移除
// - using System.Windows.Media → using Avalonia.Media（TranslateTransform 类型）
// - DependencyProperty.Register + PropertyMetadata → StyledProperty<T> + AvaloniaProperty.Register<TOwner, TType>
// - SearchTextBox.PreviewKeyDown → SearchTextBox.KeyDown（Avalonia 无 Preview 变体，参考 FilterTextBox）
using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.UserControls
{
	public partial class RevisionSearchPanelUserControl : UserControl
	{
		private static readonly double SearchPanelHeight = 30.0;

		// 阶段 4.5：WPF DependencyProperty.Register + PropertyMetadata → Avalonia StyledProperty + AvaloniaProperty.Register<TOwner, TType>。
		public static readonly StyledProperty<Grid> SearchPanelPlaceholderProperty = AvaloniaProperty.Register<RevisionSearchPanelUserControl, Grid>(nameof(SearchPanelPlaceholder));

		private bool _isBusyIndicatorVisible;

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

		public event EventHandler Closed;

		public RevisionSearchPanelUserControl()
		{
			InitializeComponent();
			base.Loaded += delegate
			{
				TranslateTransform.Y = 0.0 - SearchPanelHeight;
				SearchPanelPlaceholder.Height = 0.0;
			};
			// 阶段 4.5：WPF PreviewKeyDown → Avalonia KeyDown（无 Preview 变体，参考 FilterTextBox）。
			SearchTextBox.KeyDown += delegate(object s, KeyEventArgs e)
			{
				if (e.Key == Key.Return || e.Key == Key.F3)
				{
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
			};
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

		public void UpdateMatchesCount(int? matches)
		{
			if (matches.HasValue)
			{
				int valueOrDefault = matches.GetValueOrDefault();
				if (valueOrDefault == 1)
				{
					MatchesTextBlock.Text = $"{valueOrDefault} match";
				}
				else
				{
					MatchesTextBlock.Text = $"{valueOrDefault} matches";
				}
			}
			else
			{
				MatchesTextBlock.Text = "";
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
