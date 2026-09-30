using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Helpers;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Dialogs
{
	// WS7（2026-09-30）：本窗口原是手写静态清单（ShortcutSection/ShortcutRow 数组），
	// 已两次发生漂移：New Tag 手势 Ctrl+Shift+T→Ctrl+Shift+G 改命令后要人肉同步窗口；
	// ReopenClosedTab 新增也要手写补行。E2e28 已有「命令手势↔注册绑定」清单测试，但
	// 「命令↔窗口展示」无防护。现改为反射生成：枚举各 CommandContainer 的 IUICommand
	// （Title/Shortcut/SecondaryShortcut），按容器分节渲染；非命令类快捷键保留手写补充。
	// 防漂移测试见 ForkPlus.Tests/KeyboardShortcutsWindowReflectionTests.cs。
	public class KeyboardShortcutsWindow : ForkPlusDialogWindow
	{
		internal sealed class ShortcutSection
		{
			public string Title { get; }

			public ShortcutRow[] Rows { get; }

			public ShortcutSection(string title, params ShortcutRow[] rows)
			{
				Title = title;
				Rows = rows;
			}
		}

		internal sealed class ShortcutRow
		{
			public string Keys { get; }

			public string Description { get; }

			public ShortcutRow(string keys, string description)
			{
				Keys = keys;
				Description = description;
			}
		}

		// 反射枚举的容器清单。CommandContainer 共 6 个子类，其中 FileDiffControlCommands /
		// TextContentControlCommands 只装上下文菜单辅助类（非 IUICommand、无手势），不产生
		// 任何行，不列。枚举顺序同时是「同一命令在多个容器声明时的归属优先级」（先到先得）：
		// 作用域窄的容器先处理——New Branch.../New Tag.../Pull... 等在 Main 和 Repo 都声明，
		// 归入 Repository（第一个声明的容器）；Main 独有的命令默认落 General Navigation，
		// 个别按原窗口分组改派（见 CommandSectionOverrides）。
		// （容器类型, 区段标题）二元组——不用独立描述符类：ClassCoverageManifest 要求生产代码
		// 每个类型声明都登记清单，元组避免新增类型。
		private static readonly (Type ContainerType, string SectionTitle)[] CommandContainerSections = new (Type, string)[]
		{
			(typeof(CommitUserControlCommands), "Changes View"),
			(typeof(RepositoryUserControlCommands), "Repository"),
			(typeof(RepositoryManagerUserControlCommands), "Repository Manager"),
			(typeof(MainWindowCommands), "General Navigation")
		};

		// 区段渲染顺序（沿用原窗口布局）。
		private static readonly string[] SectionOrder = new string[]
		{
			"General Navigation",
			"All Commits View",
			"Changes View",
			"Repository",
			"Repository Manager"
		};

		// 区段归属改派表（仅影响展示分组，手势/标题数据仍全部来自反射）：部分命令只在
		// MainWindowCommands 声明（窗口级绑定），但按原窗口的信息架构属于 Repository /
		// All Commits View 区段——用命令类型→区段标题的小表改派，避免这些行全部落进
		// General Navigation 而打散原有分组。
		private static readonly Dictionary<Type, string> CommandSectionOverrides = new Dictionary<Type, string>
		{
			{ typeof(QuickFetchCommand), "Repository" },
			{ typeof(QuickPullCommand), "Repository" },
			{ typeof(QuickPushCommand), "Repository" },
			{ typeof(ShowFetchWindowCommand), "Repository" },
			{ typeof(ShowPushWindowCommand), "Repository" },
			{ typeof(ShowSaveStashWindowCommand), "Repository" },
			{ typeof(RefreshRepositoryDataCommand), "Repository" },
			{ typeof(OpenRepositoryCommand), "Repository" },
			{ typeof(ShowCloneWindowCommand), "Repository" },
			{ typeof(ShowInitRepositoryWindowCommand), "Repository" },
			{ typeof(ShowInitGitMmRepositoryWindowCommand), "Repository" },
			{ typeof(OpenRepositoryInFileExplorerCommand), "Repository" },
			{ typeof(OpenRepositoryInShellToolCommand), "Repository" },
			// 修订列表作用域命令（E2e28：不注册 window 级，只绑定修订列表/文件历史），
			// 原窗口归 All Commits View。
			{ typeof(CopyRevisionShaCommand), "All Commits View" },
			{ typeof(CopyRevisionInfoCommand), "All Commits View" },
			{ typeof(ToggleReferenceFilterCommand), "All Commits View" }
		};

		// Ctrl+Click（工具栏按钮按住 Ctrl 点击，见 ToolbarUserControl 的 IsCtrlDown 分支）是
		// 鼠标手势，不在 IUICommand 的 KeyGesture 里（Quick* 的 SecondaryShortcut 均为 null），
		// 反射拿不到——用命令类型→附加按键文本的小表补充，与原手写版展示一致
		//（CreateKeysPanel 按 ", " 拆分，"Ctrl+Click" 渲染成 [Ctrl][Click]）。
		private static readonly Dictionary<Type, string> ExtraCommandGestures = new Dictionary<Type, string>
		{
			{ typeof(QuickFetchCommand), "Ctrl+Click" },
			{ typeof(QuickPullCommand), "Ctrl+Click" },
			{ typeof(QuickPushCommand), "Ctrl+Click" }
		};

		// 非命令类快捷键（不走 IUICommand 体系）：控件级行为（搜索框/焦点导航）或无法反射
		// 对应的命令。与反射区段合并渲染：同区段内反射行在前、手写行在后。已由反射覆盖的
		// 手写行（同手势同命令）已删除，防双列——删行清单见 WS7 提交说明（Zoom In/Out、
		// Reopen closed tab、New tag、Quick* 等改由反射生成；Backspace 丢弃文件是手写版陈旧
		// 信息，命令真相为 Delete + Ctrl+Shift+D）。
		private static readonly Dictionary<string, ShortcutRow[]> HandwrittenRowsBySection = new Dictionary<string, ShortcutRow[]>
		{
			{
				"All Commits View",
				new ShortcutRow[]
				{
					new ShortcutRow("Ctrl+F", "Commit search"),
					new ShortcutRow("Enter, F3", "Jump to next search result"),
					new ShortcutRow("Shift+Enter, Shift+F3", "Jump to previous search result"),
					// RemoveReferenceCommand / ShowRemoveStashWindowCommand 的 Title 为 null，
					// 反射行无法命名展示——该 Delete 手势的语义由本行承载。
					new ShortcutRow("Delete", "Remove branch/stash")
				}
			},
			{
				"Changes View",
				new ShortcutRow[]
				{
					new ShortcutRow("Ctrl+1", "Focus commit message field"),
					new ShortcutRow("Ctrl+F", "Filter"),
					new ShortcutRow("Ctrl+O", "Open selected file")
				}
			}
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

		/// <summary>
		/// 构建全部区段（反射命令区段 + 手写补充行），internal 供防漂移测试断言。
		/// 每次调用重新反射（命令 ~100，毫秒级；CommandContainer.Lazy 构造无副作用），无需缓存。
		/// </summary>
		internal static List<ShortcutSection> BuildSections()
		{
			Dictionary<string, List<ShortcutRow>> reflectedRowsBySection = new Dictionary<string, List<ShortcutRow>>();
			HashSet<Type> renderedCommandTypes = new HashSet<Type>();
			HashSet<string> renderedRowIdentities = new HashSet<string>();
			foreach ((Type containerType, string containerSectionTitle) in CommandContainerSections)
			{
				CommandContainer container = (CommandContainer)Activator.CreateInstance(containerType);
				foreach (PropertyInfo property in container.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
					.Where((PropertyInfo p) => typeof(IUICommand).IsAssignableFrom(p.PropertyType)).OrderBy((PropertyInfo p) => p.Name))
				{
					if (!(property.GetValue(container) is IUICommand command))
					{
						continue;
					}
					// Title 为 null/空的命令（RemoveReferenceCommand、ShowRemoveStashWindowCommand）
					// 无法命名展示，跳过——其手势语义由手写行 "Delete Remove branch/stash" 承载。
					if (string.IsNullOrEmpty(command.Title))
					{
						continue;
					}
					string keys = BuildCommandKeys(command);
					if (keys.Length == 0)
					{
						continue;
					}
					// 同一命令类型在多个容器声明（如 CopyRevisionShaCommand 同时在 Main/Repo）只列一次。
					if (!renderedCommandTypes.Add(command.GetType()))
					{
						continue;
					}
					// 完全重复行（同按键同描述，如本地/远程 "Delete Branch"）只列一次。
					if (!renderedRowIdentities.Add(keys + "\0" + command.Title))
					{
						continue;
					}
					string sectionTitle = CommandSectionOverrides.TryGetValue(command.GetType(), out string overrideTitle) ? overrideTitle : containerSectionTitle;
					if (!reflectedRowsBySection.TryGetValue(sectionTitle, out List<ShortcutRow> sectionRows))
					{
						sectionRows = new List<ShortcutRow>();
						reflectedRowsBySection[sectionTitle] = sectionRows;
					}
					sectionRows.Add(new ShortcutRow(keys, command.Title));
				}
			}
			List<ShortcutSection> sections = new List<ShortcutSection>();
			foreach (string title in SectionOrder)
			{
				List<ShortcutRow> rows = new List<ShortcutRow>();
				if (reflectedRowsBySection.TryGetValue(title, out List<ShortcutRow> reflectedRows))
				{
					rows.AddRange(reflectedRows);
				}
				if (HandwrittenRowsBySection.TryGetValue(title, out ShortcutRow[] handwrittenRows))
				{
					rows.AddRange(handwrittenRows);
				}
				if (rows.Count > 0)
				{
					sections.Add(new ShortcutSection(title, rows.ToArray()));
				}
			}
			return sections;
		}

		private static string BuildCommandKeys(IUICommand command)
		{
			List<string> parts = new List<string>();
			if (command.Shortcut != null)
			{
				parts.Add(command.Shortcut.ToFriendlyString());
			}
			if (command.SecondaryShortcut != null)
			{
				parts.Add(command.SecondaryShortcut.ToFriendlyString());
			}
			if (ExtraCommandGestures.TryGetValue(command.GetType(), out string extra))
			{
				parts.Add(extra);
			}
			return string.Join(", ", parts);
		}

		private static Grid CreateContent()
		{
			Grid grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.0) });
			grid.ColumnDefinitions.Add(new ColumnDefinition());
			grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			// Migration note（2026-09-11）：普通 ScrollViewer → TouchpadAwareScrollViewer，
			// 与主界面一致修复"滚轮滚动有用但 thumb 不跟随 / 点过滚动条后滚轮失效"问题。
			// 中间区域必须是 *，否则 ScrollViewer 可能被 Auto 行撑开导致无法滚动。
			grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
			grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

			TouchpadAwareScrollViewer scrollViewer = new TouchpadAwareScrollViewer
			{
				HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
				VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Visible,
				HorizontalAlignment = HorizontalAlignment.Stretch,
				VerticalAlignment = VerticalAlignment.Stretch,
				Focusable = true,
				Margin = new Thickness(0.0, 4.0, 0.0, 0.0)
			};
			StackPanel stackPanel = new StackPanel();
			foreach (ShortcutSection section in BuildSections())
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
			// 修复（2026-09-29，"快捷键 Ctrl+,（打开偏好设置）渲染成 [Ctrl][空键][,][空键]"）：
			// 原来按 ',' 拆分"备选按键"列表，而数据里备选分隔符统一是 ", "（逗号+空格），
			// "Ctrl+," 末尾的逗号是快捷键本身 → 被拆出空串，AddChord 渲染成空徽章。
			// 改为按 ", " 拆分：既保留多备选（"Enter, F3" 等）语义，又不吞掉 Ctrl+, 的逗号。
			string[] alternatives = keys.Split(new string[] { ", " }, System.StringSplitOptions.None);
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
