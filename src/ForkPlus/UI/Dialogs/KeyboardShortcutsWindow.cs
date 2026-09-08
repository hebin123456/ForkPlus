using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Dialogs
{
	public class KeyboardShortcutsWindow : ForkPlusDialogWindow
	{
		private sealed class ShortcutSection
		{
			public string Title { get; }

			public ShortcutRow[] Rows { get; }

			public ShortcutSection(string title, params ShortcutRow[] rows)
			{
				Title = title;
				Rows = rows;
			}
		}

		private sealed class ShortcutRow
		{
			public string Keys { get; }

			public string Description { get; }

			public ShortcutRow(string keys, string description)
			{
				Keys = keys;
				Description = description;
			}
		}

		private static readonly ShortcutSection[] Sections = new ShortcutSection[]
		{
			new ShortcutSection("General Navigation",
				new ShortcutRow("Ctrl+1", "Show Changes view (second press will focus commit field)"),
				new ShortcutRow("Ctrl+2", "Show All Commits view (second press will jump to HEAD)"),
				new ShortcutRow("Ctrl+0", "Reveal HEAD"),
				new ShortcutRow("Ctrl+P", "Show Quick Launch window"),
				new ShortcutRow("Ctrl+Tab", "Select next tab"),
				new ShortcutRow("Ctrl+Shift+Tab", "Select previous tab"),
				new ShortcutRow("Ctrl+T", "Open new tab"),
				new ShortcutRow("Ctrl+W", "Close current tab"),
				new ShortcutRow("Ctrl+= / Ctrl+-", "Zoom in / Zoom out"),
				new ShortcutRow("Ctrl+,", "Open ForkPlus preferences")),
			new ShortcutSection("All Commits View",
				new ShortcutRow("Ctrl+0", "Jump to HEAD"),
				new ShortcutRow("Ctrl+F", "Commit search"),
				new ShortcutRow("Enter, F3", "Jump to next search result"),
				new ShortcutRow("Shift+Enter, Shift+F3", "Jump to previous search result"),
				new ShortcutRow("Ctrl+C", "Copy commit info"),
				new ShortcutRow("Delete", "Remove branch/stash"),
				new ShortcutRow("Ctrl+Shift+A", "Filter by active branch")),
			new ShortcutSection("Changes View",
				new ShortcutRow("Ctrl+Enter", "Commit"),
				new ShortcutRow("Ctrl+Shift+Enter", "Commit and push"),
				new ShortcutRow("Ctrl+1", "Focus commit message field"),
				new ShortcutRow("Ctrl+F", "Filter"),
				new ShortcutRow("Enter, Ctrl+Shift+S", "Stage/unstage selected file (or lines)"),
				new ShortcutRow("Ctrl+Alt+Shift+S", "Stage/unstage all files"),
				new ShortcutRow("Backspace, Ctrl+Shift+D", "Discard selected file (or lines)"),
				new ShortcutRow("Ctrl+O", "Open selected file"),
				new ShortcutRow("Ctrl+D", "Open selected file in external diff tool"),
				new ShortcutRow("Ctrl+C", "Copy selected file full path")),
			new ShortcutSection("Repository",
				new ShortcutRow("F5", "Refresh"),
				new ShortcutRow("Ctrl+Shift+N", "Init new repository"),
				new ShortcutRow("Ctrl+N", "Clone new repository"),
				new ShortcutRow("Ctrl+G", "Initialize git mm Repository"),
				new ShortcutRow("Ctrl+O", "Open repository"),
				new ShortcutRow("Ctrl+Shift+F", "Fetch"),
				new ShortcutRow("Ctrl+Alt+Shift+F, Ctrl+Click", "Quick Fetch"),
				new ShortcutRow("Ctrl+Shift+L", "Pull"),
				new ShortcutRow("Ctrl+Alt+Shift+L, Ctrl+Click", "Quick Pull"),
				new ShortcutRow("Ctrl+Shift+P", "Push"),
				new ShortcutRow("Ctrl+Alt+Shift+P, Ctrl+Click", "Quick Push"),
				new ShortcutRow("Ctrl+Shift+B", "New branch"),
				new ShortcutRow("Ctrl+Shift+T", "New tag"),
				new ShortcutRow("Ctrl+Shift+H", "Create stash"),
				new ShortcutRow("Ctrl+Alt+O", "Open in File Explorer"),
				new ShortcutRow("Ctrl+Alt+T", "Open in Terminal")),
			new ShortcutSection("Repository Manager",
				new ShortcutRow("F2", "Rename Repository"),
				new ShortcutRow("Delete", "Remove Repository"),
				new ShortcutRow("Enter", "Open Repository"))
		};

		// Migration note（2026-09-06 生产 bug）：键位徽章展示的是键名原文（Delete/Ctrl/Enter...），
		// 不能参与 ForkPlusDialogWindow 的 Loaded 期自动本地化——zh-Hans 字典里有 "Delete"→"删除"，
		// 会把键位徽章错误翻译成中文。窗口全部可见文案（段落标题/描述/标题栏 chrome）已在构造期
		// 显式 Translate，关闭自动本地化无副作用（PreferencesWindow 同款口径）。
		protected override bool ApplyAutomaticLocalization => false;

		public KeyboardShortcutsWindow()
		{
			base.Title = PreferencesLocalization.Current("Keyboard Shortcuts");
			base.ShowLogo = false;
			// 这是“只读信息”窗口：按原版语义只需要一个 Close（Cancel）按钮，不需要 Submit。
			base.ShowSubmitButton = false;
			base.ShowCancelButton = true;
			base.Width = 720.0;
			base.Height = 620.0;
			base.SizeToContent = global::Avalonia.Controls.SizeToContent.Manual;
			Content = CreateContent();
			// Migration note（根因，模块25 E2E 实证，FileHistoryWindow/SaveAsPatchWindow 同类 bug）：
			// WPF 原版在 Initialized 事件里 ApplyDialogChrome——WPF 的 Initialized 在构造完成后
			// 触发。Avalonia 12 的 Initialized 在 TopLevel 基类构造链中就触发（早于本构造器
			// 的订阅语句），下方订阅是死代码：ApplyDialogChrome 从不执行 → Cancel 按钮恒显示
			// "Cancel"（WPF 原版为 "Close"）+ 描述文本丢失。Avalonia 等价时机 = 构造器尾部。
			ApplyDialogChrome();
		}

		private void ApplyDialogChrome()
		{
			base.DialogTitle = Translate("Keyboard Shortcuts");
			base.DialogDescription = Translate("Available keyboard shortcuts");
			base.CancelButtonTitle = Translate("Close");
			base.ShowSubmitButton = false;
			base.ShowCancelButton = true;
		}

		private static Grid CreateContent()
		{
			Grid grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.0) });
			grid.ColumnDefinitions.Add(new ColumnDefinition());
			grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			// 中间区域必须是 *，否则 ScrollViewer 可能被 Auto 行撑开导致无法滚动。
			grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
			grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

			ScrollViewer scrollViewer = new ScrollViewer
			{
				HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
				VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Visible,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch,
				Focusable = true,
				Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
			};
			StackPanel stackPanel = new StackPanel();
			foreach (ShortcutSection section in Sections)
			{
				stackPanel.Children.Add(CreateSectionHeader(section.Title));
				foreach (ShortcutRow row in section.Rows)
				{
					stackPanel.Children.Add(CreateShortcutRow(row));
				}
			}
			scrollViewer.Content = stackPanel;
			Grid.SetRow(scrollViewer, 1);
			Grid.SetColumn(scrollViewer, 1);
			grid.Children.Add(scrollViewer);
			return grid;
		}

		private static TextBlock CreateSectionHeader(string title)
		{
			TextBlock textBlock = new TextBlock
			{
				Text = Translate(title),
				FontSize = 14.0,
				FontWeight = FontWeights.Medium,
				Margin = new Thickness(0.0, 12.0, 0.0, 5.0)
			};
			return textBlock;
		}

		private static Grid CreateShortcutRow(ShortcutRow row)
		{
			Grid grid = new Grid
			{
				Margin = new Thickness(0.0, 2.0, 0.0, 2.0)
			};
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230.0) });
			grid.ColumnDefinitions.Add(new ColumnDefinition());
			WrapPanel keysPanel = CreateKeysPanel(row.Keys);
			Grid.SetColumn(keysPanel, 0);
			grid.Children.Add(keysPanel);
			TextBlock descriptionTextBlock = new TextBlock
			{
				Text = Translate(row.Description),
				FontSize = 13.0,
				VerticalAlignment = VerticalAlignment.Center,
				TextWrapping = TextWrapping.Wrap,
				Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
			};
			descriptionTextBlock.SetResourceReference(TextBlock.ForegroundProperty, "ForegroundBrush");
			Grid.SetColumn(descriptionTextBlock, 1);
			grid.Children.Add(descriptionTextBlock);
			return grid;
		}

		private static WrapPanel CreateKeysPanel(string keys)
		{
			WrapPanel panel = new WrapPanel
			{
				VerticalAlignment = VerticalAlignment.Center
			};
			string[] alternatives = keys.Split(',');
			for (int i = 0; i < alternatives.Length; i++)
			{
				if (i > 0)
				{
					panel.Children.Add(CreateSeparatorText(","));
				}
				string[] chords = alternatives[i].Trim().Split(new string[] { " / " }, System.StringSplitOptions.None);
				for (int j = 0; j < chords.Length; j++)
				{
					if (j > 0)
					{
						panel.Children.Add(CreateSeparatorText("/"));
					}
					AddChord(panel, chords[j].Trim());
				}
			}
			return panel;
		}

		private static void AddChord(WrapPanel panel, string chord)
		{
			string[] keys = chord.Split('+');
			for (int i = 0; i < keys.Length; i++)
			{
				if (i > 0)
				{
					panel.Children.Add(CreateSeparatorText("+"));
				}
				panel.Children.Add(CreateKeyBadge(keys[i].Trim()));
			}
		}

		private static Border CreateKeyBadge(string key)
		{
			TextBlock textBlock = new TextBlock
			{
				Text = key,
				FontFamily = FontConstants.MonospaceFontFamily,
				FontSize = 12.0,
				VerticalAlignment = VerticalAlignment.Center
			};
			Border border = new Border
			{
				Child = textBlock,
				CornerRadius = new CornerRadius(3.0),
				BorderThickness = new Thickness(1.0),
				Padding = new Thickness(5.0, 1.0, 5.0, 2.0),
				Margin = new Thickness(1.0, 1.0, 1.0, 1.0)
			};
			border.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
			border.SetResourceReference(Border.BackgroundProperty, "TextBox.Static.Background");
			return border;
		}

		private static TextBlock CreateSeparatorText(string text)
		{
			TextBlock textBlock = new TextBlock
			{
				Text = text,
				Margin = new Thickness(3.0, 0.0, 3.0, 0.0),
				VerticalAlignment = VerticalAlignment.Center,
				FontSize = 12.0
			};
			textBlock.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryLabelBrush");
			return textBlock;
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
