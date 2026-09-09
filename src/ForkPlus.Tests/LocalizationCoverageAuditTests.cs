// 审计用（临时，不入最终回归集）：zh-Hans 下打开真实 MainWindow + 仓库，
// 扫视觉树里"语言包存在译文但界面仍显示英文原文"的控件，定位重构后丢失的国际化。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Settings;
using ForkPlus.UI;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class LocalizationCoverageAuditTests
	{
		[Fact]
		public void Audit_MainWindow_UntranslatedStrings()
		{
			string repoPath = TestRepoFactory.CreateConflict();
			try
			{
				Dispatcher.UIThread.InvokeAsync(delegate
				{
					string original = ForkPlusSettings.Default.UiLanguage;
					try
					{
						ForkPlusSettings.Default.UiLanguage = "zh-Hans";
						RepositoryUserControl repo = E2eMainWindowHarness.OpenRepository(repoPath, out MainWindow window);
						try
						{
							repo.ActivateCommitView();
							Dispatcher.UIThread.RunJobs();
							// 选中冲突文件，触发 MergeConflictUserControl 动态装配（CommitFileDiffControl 分发路径）
							CommitUserControl commit = repo.Content.CommitUserControl;
							StageFileUserControl stage = commit.StageFileUserControl;
							Assert.True(UiClick.WaitFor(delegate
							{
								return stage.AllUnstagedFiles.Any(f => f.Path.Contains("conflict", StringComparison.OrdinalIgnoreCase));
							}), "工作区状态未装配");
							stage.UnstagedFilesFileListUserControl.SelectFile(
							stage.AllUnstagedFiles.First(f => f.Path.Contains("conflict", StringComparison.OrdinalIgnoreCase)).Path);
						// 等 MergeConflictUserControl 动态装配（CommitFileDiffControl 分发路径）
						MergeConflictUserControl conflict = null;
						Assert.True(UiClick.WaitFor(delegate
						{
							conflict = UiClick.FindAll<MergeConflictUserControl>(window).FirstOrDefault();
							return conflict != null;
						}), "选中 Unmerged 文件后应出现 MergeConflictUserControl");
						Dispatcher.UIThread.RunJobs();
						var missing = CollectUntranslated(window);
						foreach (string line in missing)
						{
							Console.WriteLine("[MISSING] " + line);
						}
						}
						finally
						{
							E2eMainWindowHarness.CloseRepositoryTab(window, repoPath);
						}
					}
					finally
					{
						ForkPlusSettings.Default.UiLanguage = original;
					}
					return 0;
				}).GetAwaiter().GetResult();
			}
			finally
			{
				TestRepoFactory.Cleanup(repoPath);
			}
		}

		internal static List<string> CollectUntranslated(Visual root)
		{
			var missing = new List<string>();
			var seen = new HashSet<string>();
			var translations = ForkPlus.UI.UserControls.Preferences.PreferencesLocalization.Translate;
			foreach (Control c in root.GetVisualDescendants().OfType<Control>())
			{
				string text = null;
				string kind = null;
				if (c is TextBlock tb && tb.Text is string s)
				{
					text = s; kind = "TextBlock";
				}
				else if ((c is Button || c is ToggleButton || c is CheckBox || c is RadioButton || c is MenuItem)
					&& c is ContentControl cc && cc.Content is string s2)
				{
					text = s2; kind = c.GetType().Name;
				}
				else if (c is ToolTip tip && tip.Content is string s3)
				{
					text = s3; kind = "ToolTip";
				}
				else if (c is TextBox textBox && textBox.Watermark is string s4)
				{
					text = s4; kind = "Watermark";
				}
				else if (c is ForkPlus.UI.Controls.PlaceholderTextBox ptx && ptx.Placeholder is string s5)
				{
					text = s5; kind = "Placeholder";
				}
				if (string.IsNullOrWhiteSpace(text) || text.Length < 3)
				{
					continue;
				}
				bool looksEnglish = text.All(delegate (char ch) { return ch < 128; }) && text.Any(char.IsLetter);
				if (!looksEnglish)
				{
					continue;
				}
				// 排除误报：Workspaces 工具栏按钮显示的是"当前工作区名称"（用户数据，
				// 默认 "Work"/"Home"，可被用户重命名），与下拉菜单/配置窗口一致地原样
				// 显示，不属于 UI 文案，不做翻译。
				if (c.GetVisualAncestors().OfType<Control>().Any(delegate (Control a)
				{
					return a.Name == "WorkspacesToolbarDropdownButton";
				}))
				{
					continue;
				}
				string translated = translations(text, "zh-Hans");
				if (translated != null && translated != text && seen.Add(kind + "|" + text))
				{
					missing.Add(kind + ": \"" + text + "\" (应为 \"" + translated + "\") 位置: " + AncestorChain(c));
				}
			}
			return missing;
		}

		private static string AncestorChain(Control c)
		{
			var sb = new StringBuilder();
			Visual v = c;
			int depth = 0;
			while (v != null && depth < 5)
			{
				sb.Append(v.GetType().Name);
				sb.Append(" < ");
				v = v.GetVisualParent();
				depth++;
			}
			return sb.ToString();
		}
	}
}
