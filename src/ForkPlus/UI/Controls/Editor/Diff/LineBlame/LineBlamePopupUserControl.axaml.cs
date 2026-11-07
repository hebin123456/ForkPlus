using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Controls.Editor.Diff.LineBlame
{
	/// <summary>
	/// 行号 blame 浮窗的视图层。只负责状态渲染与交互事件，数据装配在
	/// LineBlameHoverController 中完成。
	/// </summary>
	internal partial class LineBlamePopupUserControl : UserControl
	{
		private const int MaxHistoryRows = 5;

		public event Action<Sha, string> ShaClicked;

		public event Action HistoryExpanded;

		public event Action ViewFullHistoryRequested;

		[Null]
		private LineBlamePopupData _data;

		private string _historyPath;

		private bool _historyExpanded;

		private bool _historyRequested;

		public LineBlamePopupUserControl()
		{
			InitializeComponent();
			OtherHeaderTextBlock.Text = Current("Older version");
			ViewFullHistoryButton.Content = Current("View full history...");
			RefreshHistoryToggleText();
		}

		public void ShowLoading()
		{
			BusyPanel.IsVisible = true;
			ErrorPanel.IsVisible = false;
			ContentPanel.IsVisible = false;
			LoadingTextBlock.Text = Current("Loading commit info...");
		}

		public void ShowError(string message)
		{
			BusyPanel.IsVisible = false;
			ContentPanel.IsVisible = false;
			ErrorPanel.IsVisible = true;
			ErrorTextBlock.Text = message;
		}

		public void ShowData(LineBlamePopupData data)
		{
			_data = data;
			_historyPath = data.HistoryPath ?? data.FilePath;
			_historyExpanded = false;
			_historyRequested = false;
			HistoryPanel.IsVisible = false;
			HistoryItemsPanel.Children.Clear();
			HistoryLoadingTextBlock.IsVisible = false;
			HistoryEmptyTextBlock.IsVisible = false;
			ViewFullHistoryButton.IsVisible = false;
			RefreshHistoryToggleText();

			BusyPanel.IsVisible = false;
			ErrorPanel.IsVisible = false;
			ContentPanel.IsVisible = true;

			LineBlameLineItem primary = data.Primary;
			UncommittedPanel.IsVisible = primary.IsUncommitted;
			PrimaryPanel.IsVisible = !primary.IsUncommitted;
			if (primary.IsUncommitted)
			{
				SetUncommittedText(primary.UncommittedKind);
			}
			else
			{
				BindRevisionCard(primary.Revision, PrimaryAvatar, PrimaryAuthorTextBlock, PrimaryDateTextBlock, PrimaryShaButton, PrimarySubjectTextBlock);
			}

			LineBlameLineItem other = data.Other;
			OtherBorder.IsVisible = other != null && other.Revision != null;
			if (other != null && other.Revision != null)
			{
				BindRevisionCard(other.Revision, OtherAvatar, OtherAuthorTextBlock, OtherDateTextBlock, OtherShaButton, OtherSubjectTextBlock);
			}

			HistoryBorder.IsVisible = data.HasHistory;
		}

		public void ShowHistoryLoading()
		{
			HistoryLoadingTextBlock.Text = Current("Loading commit history...");
			HistoryLoadingTextBlock.IsVisible = true;
			HistoryEmptyTextBlock.IsVisible = false;
			HistoryItemsPanel.Children.Clear();
			ViewFullHistoryButton.IsVisible = false;
		}

		public void ShowHistory(RevisionWithFiles[] revisions)
		{
			HistoryLoadingTextBlock.IsVisible = false;
			HistoryItemsPanel.Children.Clear();
			if (revisions == null || revisions.Length == 0)
			{
				HistoryEmptyTextBlock.Text = Current("No history found for this line");
				HistoryEmptyTextBlock.IsVisible = true;
				ViewFullHistoryButton.IsVisible = false;
				return;
			}
			HistoryEmptyTextBlock.IsVisible = false;
			int count = Math.Min(MaxHistoryRows, revisions.Length);
			for (int i = 0; i < count; i++)
			{
				HistoryItemsPanel.Children.Add(CreateHistoryRow(revisions[i].Revision));
			}
			ViewFullHistoryButton.IsVisible = true;
		}

		public void ShowHistoryError(string message)
		{
			HistoryLoadingTextBlock.IsVisible = false;
			HistoryItemsPanel.Children.Clear();
			HistoryEmptyTextBlock.Text = message;
			HistoryEmptyTextBlock.IsVisible = true;
			ViewFullHistoryButton.IsVisible = false;
		}

		private void SetUncommittedText(LineBlameUncommittedKind kind)
		{
			switch (kind)
			{
			case LineBlameUncommittedKind.Staged:
				UncommittedTitleTextBlock.Text = Current("Staged, not committed yet");
				UncommittedHintTextBlock.Text = Current("This line will be included in the next commit");
				break;
			case LineBlameUncommittedKind.Untracked:
				UncommittedTitleTextBlock.Text = Current("Untracked new file");
				UncommittedHintTextBlock.Text = Current("This file has not been added to the repository yet");
				break;
			default:
				UncommittedTitleTextBlock.Text = Current("Uncommitted line");
				UncommittedHintTextBlock.Text = Current("This line has not been committed yet");
				break;
			}
		}

		private void BindRevisionCard(Revision revision, AvatarImage avatar, TextBlock authorTextBlock, TextBlock dateTextBlock, HyperlinkButton shaButton, TextBlock subjectTextBlock)
		{
			if (revision == null)
			{
				authorTextBlock.Text = string.Empty;
				dateTextBlock.Text = string.Empty;
				shaButton.Content = string.Empty;
				subjectTextBlock.Text = string.Empty;
				return;
			}
			avatar.UserIdentity = revision.Author;
			authorTextBlock.Text = revision.Author?.Name ?? string.Empty;
			dateTextBlock.Text = DateTimeHelper.ToRelativeString(revision.AuthorDate);
			ToolTip.SetTip(dateTextBlock, revision.AuthorDate.ToString("yyyy-MM-dd HH:mm:ss"));
			shaButton.Content = new TextBlock
			{
				Text = revision.Sha.ToAbbreviatedString(),
				TextDecorations = TextDecorations.Underline
			};
			ToolTip.SetTip(shaButton, Current("Navigate to") + " '" + revision.Sha.ToAbbreviatedString() + "'");
			subjectTextBlock.Text = revision.Message ?? string.Empty;
			ToolTip.SetTip(subjectTextBlock, revision.Message);
		}

		private Button CreateHistoryRow(Revision revision)
		{
			Button button = new Button
			{
				// TextButtonStyle 的模板把 Content 当作字符串塞给 TextBlock（Grid 会显示成
				// "Avalonia.Controls.Grid"）。历史行内容是多列 Grid，必须用带 ContentPresenter
				// 的 TransparentButtonStyle。
				Theme = Application.Current?.TryFindResource("TransparentButtonStyle") as ControlTheme,
				IsTabStop = false,
				Focusable = false,
				Height = 20,
				Margin = new Thickness(0),
				Padding = new Thickness(0, 1, 0, 1),
				HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch,
				HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Left
			};
			Grid grid = new Grid
			{
				ColumnDefinitions = new ColumnDefinitions("58,*,Auto"),
				Width = 420
			};
			TextBlock shaText = new TextBlock
			{
				Text = revision.Sha.ToAbbreviatedString(),
				FontSize = 11,
				FontFamily = new FontFamily("Consolas, Courier New, monospace"),
				Foreground = LinkBrush,
				TextDecorations = TextDecorations.Underline,
				VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
			};
			TextBlock subjectText = new TextBlock
			{
				Text = revision.Message ?? string.Empty,
				FontSize = 12,
				TextTrimming = TextTrimming.CharacterEllipsis,
				Margin = new Thickness(4, 0, 4, 0),
				VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
			};
			TextBlock dateText = new TextBlock
			{
				Text = DateTimeHelper.ToRelativeString(revision.AuthorDate),
				FontSize = 11,
				Foreground = GrayBrush,
				VerticalAlignment = global::Avalonia.Layout.VerticalAlignment.Center
			};
			ToolTip.SetTip(subjectText, revision.Message);
			ToolTip.SetTip(dateText, revision.AuthorDate.ToString("yyyy-MM-dd HH:mm:ss"));
			grid.Children.Add(shaText);
			Grid.SetColumn(subjectText, 1);
			grid.Children.Add(subjectText);
			Grid.SetColumn(dateText, 2);
			grid.Children.Add(dateText);
			button.Content = grid;
			button.Click += delegate
			{
				ShaClicked?.Invoke(revision.Sha, _historyPath);
			};
			return button;
		}

		private void PrimaryShaButton_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
		{
			if (_data?.Primary?.Revision != null)
			{
				ShaClicked?.Invoke(_data.Primary.Revision.Sha, _data.Primary.Path);
			}
		}

		private void OtherShaButton_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
		{
			if (_data?.Other?.Revision != null)
			{
				ShaClicked?.Invoke(_data.Other.Revision.Sha, _data.Other.Path);
			}
		}

		private void HistoryToggleButton_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
		{
			_historyExpanded = !_historyExpanded;
			HistoryPanel.IsVisible = _historyExpanded;
			RefreshHistoryToggleText();
			if (_historyExpanded && !_historyRequested)
			{
				_historyRequested = true;
				HistoryExpanded?.Invoke();
			}
		}

		private void ViewFullHistoryButton_Click(object sender, global::Avalonia.Interactivity.RoutedEventArgs e)
		{
			ViewFullHistoryRequested?.Invoke();
		}

		private void RefreshHistoryToggleText()
		{
			HistoryToggleButton.Content = Current("Change history") + (_historyExpanded ? "  ▴" : "  ▸");
		}

		private static IBrush GrayBrush
		{
			get
			{
				return Application.Current?.TryFindResource("ForegroundBrush.Gray") as IBrush ?? Brushes.Gray;
			}
		}

		private static IBrush LinkBrush
		{
			get
			{
				return Application.Current?.TryFindResource("CodeEditorLinkForeground") as IBrush ?? Brushes.DodgerBlue;
			}
		}

		private static string Current(string text)
		{
			return PreferencesLocalization.Current(text);
		}
	}
}
