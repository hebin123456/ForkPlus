using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.UserControls
{
	public partial class RevisionSearchPanelUserControl : UserControl
	{
		private static readonly double SearchPanelHeight = 30.0;

		public static readonly DependencyProperty SearchPanelPlaceholderProperty = DependencyProperty.Register("SearchPanelPlaceholder", typeof(Grid), typeof(RevisionSearchPanelUserControl), new PropertyMetadata((object)null));

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
			SearchTextBox.PreviewKeyDown += delegate(object s, KeyEventArgs e)
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
