// WS7 防漂移测试（2026-09-30）：KeyboardShortcutsWindow 从手写静态清单改为反射生成
// （枚举 CommandContainer 的 IUICommand，按容器分节 + 手写非命令行合并）。本文件锁三条不变量：
//  ① 完整性：每个命令容器里所有带手势的命令（Shortcut/SecondaryShortcut）都出现在窗口区段里
//     （按手势匹配；容器清单由测试独立从程序集反射 CommandContainer 子类获取，不与窗口共享清单——
//     新增容器/命令忘了入窗在这里红）；
//  ② 防重复：窗口内不存在重复行（同按键同描述），且每个有 Title 的容器命令恰好对应一行
//     （防"手写行 + 反射行"双列回归——历史漂移：New Tag 手势改动、ReopenClosedTab 新增都靠人肉同步）；
//  ③ 金步行：非命令类手写行（搜索导航/焦点等）仍在；历史漂移案例（New Tag Ctrl+Shift+G /
//     Reopen Closed Tab Ctrl+Shift+T / Zoom / Quick* 的 Ctrl+Click）由反射稳定生成。
// 渲染验证（headless 打开窗口收集文本/键位徽章）参考 E2e25 的手法（AllTexts/KeyBadgeTexts）。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.Helpers;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class KeyboardShortcutsWindowReflectionTests
	{
		private sealed class Row
		{
			public string Keys { get; }

			public string Description { get; }

			public Row(string keys, string description)
			{
				Keys = keys;
				Description = description;
			}

			/// <summary>行的备选按键集合（CreateKeysPanel 同口径按 ", " 拆分；手写行的 " / " 保留原样不拆）。</summary>
			public bool HasAlternative(string gesture)
			{
				return Keys.Split(new string[] { ", " }, StringSplitOptions.None).Contains(gesture);
			}
		}

		// ============================ 共享助手 ============================

		private static List<Row> BuildRows()
		{
			return KeyboardShortcutsWindow.BuildSections()
				.SelectMany((KeyboardShortcutsWindow.ShortcutSection s) => s.Rows)
				.Select((KeyboardShortcutsWindow.ShortcutRow r) => new Row(r.Keys, r.Description))
				.ToList();
		}

		/// <summary>独立枚举全部 CommandContainer 子类（与窗口的静态清单解耦——窗口漏登记容器时 ① 会红）。
		/// FileDiffControlCommands/TextContentControlCommands 也是子类，但其属性不是 IUICommand、
		/// 无手势，不产生断言（有手势的命令容器只有 4 个）。</summary>
		private static List<Type> CollectContainerTypes()
		{
			return typeof(CommandContainer).Assembly.GetTypes()
				.Where((Type t) => !t.IsAbstract && typeof(CommandContainer).IsAssignableFrom(t))
				.OrderBy((Type t) => t.FullName)
				.ToList();
		}

		private static List<IUICommand> CollectContainerCommands(CommandContainer container)
		{
			List<IUICommand> commands = new List<IUICommand>();
			foreach (PropertyInfo property in container.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where((PropertyInfo p) => typeof(IUICommand).IsAssignableFrom(p.PropertyType))
				.OrderBy((PropertyInfo p) => p.Name))
			{
				if (property.GetValue(container) is IUICommand command)
				{
					commands.Add(command);
				}
			}
			return commands;
		}

		// ============================ ① ② 反射清单完整性 + 防重复 ============================

		[Fact]
		public void Reflection_EveryContainerCommandAppearsExactlyOnce()
		{
			List<Row> rows = BuildRows();
			Assert.True(rows.Count > 0, "反射生成的快捷键行不应为空");

			// ② 防重复行：同按键同描述的行全窗口只允许出现一次
			var duplicated = rows.GroupBy((Row r) => r.Keys + "\0" + r.Description)
				.Where((IGrouping<string, Row> g) => g.Count() > 1)
				.ToList();
			Assert.True(duplicated.Count == 0,
				"快捷键窗口存在重复行（同按键同描述，防手写+反射双列）: "
				+ string.Join("; ", duplicated.Select((IGrouping<string, Row> g) => g.First().Keys + " → " + g.First().Description)));

			foreach (Type containerType in CollectContainerTypes())
			{
				CommandContainer container = (CommandContainer)Activator.CreateInstance(containerType);
				foreach (IUICommand command in CollectContainerCommands(container))
				{
					// ① 每个带手势的命令都出现在窗口（按手势匹配；Title 为 null 的命令——如
					// RemoveReferenceCommand——不产生反射行，但其手势必须由其他行覆盖）
					foreach (KeyGesture gesture in new KeyGesture[] { command.Shortcut, command.SecondaryShortcut }
						.Where((KeyGesture g) => g != null))
					{
						string friendly = gesture.ToFriendlyString();
						Assert.True(rows.Any((Row r) => r.HasAlternative(friendly)),
							containerType.Name + " 的命令（Title=" + command.Title + "）手势 " + friendly
							+ " 未出现在快捷键窗口任何行（命令改了手势/新增命令而窗口未跟进）");
					}
					if (string.IsNullOrEmpty(command.Title) || command.Shortcut == null)
					{
						continue;
					}
					// ② 每个有 Title 的容器命令恰好对应一行（同描述 + 主手势匹配）
					string primary = command.Shortcut.ToFriendlyString();
					int matches = rows.Count((Row r) => r.Description == command.Title && r.HasAlternative(primary));
					Assert.True(matches == 1,
						containerType.Name + "（" + command.Title + " / " + primary
						+ "）应恰好对应一行，实际 " + matches + " 行（0=丢展示，>1=双列）");
				}
			}
		}

		// ============================ ③ 金步行 + 区段结构 ============================

		[Fact]
		public void Reflection_GoldenRowsAndSectionStructure()
		{
			List<KeyboardShortcutsWindow.ShortcutSection> sections = KeyboardShortcutsWindow.BuildSections();
			List<Row> rows = sections
				.SelectMany((KeyboardShortcutsWindow.ShortcutSection s) => s.Rows)
				.Select((KeyboardShortcutsWindow.ShortcutRow r) => new Row(r.Keys, r.Description))
				.ToList();

			// 区段齐全且顺序稳定（沿用原窗口布局）
			Assert.Equal(new string[]
			{
				"General Navigation",
				"All Commits View",
				"Changes View",
				"Repository",
				"Repository Manager"
			}, sections.Select((KeyboardShortcutsWindow.ShortcutSection s) => s.Title).ToArray());

			// 非命令类手写金步行仍在（不走 IUICommand 体系，反射拿不到）
			Assert.Contains(rows, (Row r) => r.Keys == "Ctrl+F" && r.Description == "Commit search");
			Assert.Contains(rows, (Row r) => r.Keys == "Enter, F3" && r.Description == "Jump to next search result");
			Assert.Contains(rows, (Row r) => r.Keys == "Shift+Enter, Shift+F3" && r.Description == "Jump to previous search result");
			// RemoveReferenceCommand/ShowRemoveStashWindowCommand 的 Title 为 null（反射行无法命名），
			// 该 Delete 手势语义由手写行承载
			Assert.Contains(rows, (Row r) => r.Keys == "Delete" && r.Description == "Remove branch/stash");
			Assert.Contains(rows, (Row r) => r.Keys == "Ctrl+1" && r.Description == "Focus commit message field");
			Assert.Contains(rows, (Row r) => r.Keys == "Ctrl+F" && r.Description == "Filter");
			Assert.Contains(rows, (Row r) => r.Keys == "Ctrl+O" && r.Description == "Open selected file");

			// 历史漂移案例由反射稳定生成（命令手势改动 → 窗口自动跟进，不再人肉同步）
			// New Tag：Ctrl+Shift+T → Ctrl+Shift+G（漂移实例 1）
			Assert.Contains(rows, (Row r) => r.Description == "New Tag..." && r.HasAlternative("Ctrl+Shift+G"));
			// Reopen closed tab：新增命令自动入窗（漂移实例 2）
			Assert.Contains(rows, (Row r) => r.Description == "Reopen Closed Tab" && r.HasAlternative("Ctrl+Shift+T"));
			// Zoom：窗口管理类，由 Increase/DecreaseLayoutScaleCommand 反射生成
			Assert.Contains(rows, (Row r) => r.Description == "Zoom In" && r.HasAlternative("Ctrl+="));
			Assert.Contains(rows, (Row r) => r.Description == "Zoom Out" && r.HasAlternative("Ctrl+-"));
			// Quick Push：键盘手势 + 工具栏 Ctrl+Click（鼠标手势经静态小表补充，SecondaryShortcut 为 null）。
			// 键序注意：ToFriendlyString 修饰键输出顺序为 Ctrl+Shift+Alt+字母。
			Assert.Contains(rows, (Row r) => r.Description == "Quick Push"
				&& r.HasAlternative("Ctrl+Shift+Alt+P") && r.HasAlternative("Ctrl+Click"));
			// Ctrl+,（偏好设置）：CreateKeysPanel 按 ", " 拆分的回归锁——逗号是键名不是分隔符
			Assert.Contains(rows, (Row r) => r.Description == "Preferences..." && r.HasAlternative("Ctrl+,"));
		}

		// ============================ 渲染验证（headless 打开窗口） ============================

		[Fact]
		public void Rendered_WindowShowsSectionsAndKeyBadges()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				var window = new KeyboardShortcutsWindow();
				window.Show();
				Dispatcher.UIThread.RunJobs();
				try
				{
					// 段落标题经 Translate 本地化（与 E2e25 同口径断言全部 5 段）
					List<string> texts = UiClick.FindAll<TextBlock>(window)
						.Select((TextBlock t) => t.Text)
						.Where((string t) => !string.IsNullOrEmpty(t))
						.ToList();
					Assert.Contains(E2eMainWindowHarness.Tr("General Navigation"), texts);
					Assert.Contains(E2eMainWindowHarness.Tr("All Commits View"), texts);
					Assert.Contains(E2eMainWindowHarness.Tr("Changes View"), texts);
					Assert.Contains(E2eMainWindowHarness.Tr("Repository"), texts);
					Assert.Contains(E2eMainWindowHarness.Tr("Repository Manager"), texts);

					// 键位徽章原文（键名不翻译）：Border 包 TextBlock 的徽章结构
					List<string> badges = UiClick.FindAll<Avalonia.Controls.Border>(window)
						.Select((Avalonia.Controls.Border b) => b.Child as TextBlock)
						.Where((TextBlock tb) => tb != null && !string.IsNullOrEmpty(tb.Text))
						.Select((TextBlock tb) => tb.Text)
						.ToList();
					Assert.Contains("Ctrl", badges);
					Assert.Contains("Delete", badges); // 回归锁：自动本地化曾把 Delete 翻成"删除"
					Assert.True(HasAdjacentKeys(badges, "Ctrl", "Shift", "G"),
						"应存在 Ctrl+Shift+G 键位徽章序列（New Tag，反射生成），实际徽章: " + string.Join(",", badges));
					Assert.True(HasAdjacentKeys(badges, "Ctrl", ","),
						"应存在 Ctrl+, 键位徽章序列（Preferences，逗号是键名不是备选分隔符），实际徽章: " + string.Join(",", badges));

					// 渲染行数 == 反射+手写数据行数（BuildSections 与渲染管线一致，无丢行）
					List<KeyboardShortcutsWindow.ShortcutSection> sections = KeyboardShortcutsWindow.BuildSections();
					int expectedRows = sections.Sum((KeyboardShortcutsWindow.ShortcutSection s) => s.Rows.Length);
					int renderedRows = UiClick.FindAll<Avalonia.Controls.WrapPanel>(window).Count;
					Assert.True(renderedRows >= expectedRows,
						"渲染的按键面板数（" + renderedRows + "）应不少于数据行数（" + expectedRows + "）");
				}
				finally
				{
					window.Close();
					Dispatcher.UIThread.RunJobs();
				}
			});
		}

		/// <summary>徽章序列中是否存在给定的相邻按键 token 序列（E2e25 同款：按键按 '+' 拆成独立徽章，
		/// 组合串不出现在任何单一 TextBlock 里）。</summary>
		private static bool HasAdjacentKeys(List<string> badges, params string[] keys)
		{
			for (int i = 0; i + keys.Length <= badges.Count; i++)
			{
				bool match = true;
				for (int j = 0; j < keys.Length; j++)
				{
					if (badges[i + j] != keys[j])
					{
						match = false;
						break;
					}
				}
				if (match)
				{
					return true;
				}
			}
			return false;
		}
	}
}
