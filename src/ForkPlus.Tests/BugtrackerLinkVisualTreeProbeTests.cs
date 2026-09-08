// 回归测试（2026-09-04，"提交详情 SHA/父提交/Commit 内容与作者信息不对齐（部分节点缩进）"修复产物）：
// 根因（探针实证 TextInlinesSyncProbeTests）：Avalonia 的 InlineCollection 首次 Add 时，
// 若 TextBlock.Text 非空，会把 Text 隐式转成 Run 插到 Inlines 开头并清空 Text。
// WPF 的 Text 与 Inlines 是同步视图、无此行为。按 WPF 语义写的 bugtracker/搜索高亮
// （Inlines.Clear 后分段 Add）因此把全文重复渲染一遍，issue 链接按钮（HyperlinkButton）
// 被推到重复文本后面——含 #123 等引用的提交 subject/description 出现整体缩进。
// 修复：HighlightingTextBlockExtensions 分段前保存原文（ConditionalWeakTable）并置
// Text=null 阻止隐式插入；RestoreText 从弱表取原文用 Text 赋值恢复。
// 本测试守卫：1) 分段结构正确（无全文重复 Run）；2) 链接按钮几何（起点 X=0，Padding=0）；
// 3) 链接在开头时首元素无缩进；4) 高亮→清除→再高亮的往返（原文不丢）。
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI.Controls;
using Xunit;
using SelTB = ForkPlus.UI.Controls.SelectableTextBlock;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class BugtrackerLinkVisualTreeProbeTests
	{
		private static BugtrackerLinkDefinition MakeRule()
		{
			return BugtrackerLinkDefinition.Create("GitHub", ForkPlus.Level.Shared, "#[0-9]+", "https://github.com/x/y/issues/${0}");
		}

		private static void ApplyBugtracker(TextBlock tb, string highlight, BugtrackerLinkDefinition[] rules)
		{
			var mi = typeof(HighlightingTextBlockExtensions).GetMethod("ApplySearchAndButrackerHighlighting",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
			Assert.NotNull(mi);
			mi.Invoke(null, new object[] { tb, highlight, rules });
		}

		private static void ApplySearch(TextBlock tb, string highlight)
		{
			var mi = typeof(HighlightingTextBlockExtensions).GetMethod("ApplySearchHighlighting",
				System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
			Assert.NotNull(mi);
			mi.Invoke(null, new object[] { tb, highlight });
		}

		[Fact]
		public void BugtrackerLink_Inlines_NoDuplicatedFullTextRun()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				var rule = MakeRule();

				var linked = new SelTB { Text = "#123 fixed the bug", Foreground = Brushes.Black };
				ApplyBugtracker(linked, null, new[] { rule });

				var host = new Border { Width = 400, Height = 40, Background = Brushes.White, Child = linked };
				var window = new Window { Width = 500, Height = 100, Content = host, Background = Brushes.White };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				// 1) 分段结构：[Run''][InlineUIContainer(链接)][Run' fixed the bug']——无全文重复 Run
				var inl = linked.Inlines;
				Assert.Equal(3, inl.Count);
				Assert.IsType<Run>(inl[0]);
				Assert.True(string.IsNullOrEmpty(((Run)inl[0]).Text));
				Assert.IsType<InlineUIContainer>(inl[1]);
				Assert.IsType<Run>(inl[2]);
				Assert.Equal(" fixed the bug", ((Run)inl[2]).Text);
				foreach (var i in inl.OfType<Run>())
				{
					Assert.NotEqual("#123 fixed the bug", i.Text); // 全文 Run 是本 bug 的形态
				}

				// 2) 链接按钮几何：起点 X=0（不被重复文本推后）、零内边距
				var hb = linked.GetVisualDescendants().OfType<HyperlinkButton>().Single();
				Assert.Equal(new Thickness(0), hb.Padding);
				Assert.Equal(new Thickness(0), hb.BorderThickness);
				var pos = hb.TranslatePoint(new Point(0, 0), linked);
				Assert.NotNull(pos);
				Assert.True(pos!.Value.X < 1.0, $"链接按钮起点 X={pos.Value.X}（应为 0，无缩进）");

				// 3) 链接内容是分段文本（"#123"），不是全文
				Assert.IsType<Run>(hb.Content);
				Assert.Equal("#123", ((Run)hb.Content).Text);

				window.Close();
			});
		}

		[Fact]
		public void BugtrackerLink_LinkAtStart_FirstElementNotIndented()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				var rule = MakeRule();

				var linked = new SelTB { Text = "#123 fixed the bug" };
				ApplyBugtracker(linked, null, new[] { rule });

				var host = new Border { Width = 400, Height = 40, Child = linked };
				var window = new Window { Width = 500, Height = 100, Content = host };
				window.Show();
				Dispatcher.UIThread.RunJobs();
				Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);
				Dispatcher.UIThread.RunJobs();

				double firstX = -1;
				string firstType = "<none>";
				foreach (var d in linked.GetVisualDescendants())
				{
					if (d is Visual v && v != linked)
					{
						var p = v.TranslatePoint(new Point(0, 0), linked);
						if (p.HasValue)
						{
							firstX = p.Value.X;
							firstType = d.GetType().Name;
							break;
						}
					}
				}
				window.Close();

				// 首个可视元素不能有显著左偏移（修复前实测 136px：隐式全文 Run 把链接推后）
				Assert.True(firstX < 4, $"issue 引用开头的提交信息首元素偏移 {firstX}px（{firstType}）");
			});
		}

		[Fact]
		public void SearchHighlight_RestoreRoundTrip_KeepsText()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				var rule = MakeRule();

				var tb = new SelTB { Text = "Fix #123 now" };
				var host = new Border { Width = 400, Height = 40, Child = tb };
				var window = new Window { Width = 500, Height = 100, Content = host };
				window.Show();
				Dispatcher.UIThread.RunJobs();

				// 高亮（进入分段模式）→ 清除（RestoreText）→ 文本必须完整保留
				ApplyBugtracker(tb, null, new[] { rule });
				Dispatcher.UIThread.RunJobs();
				Assert.True(tb.Inlines.Count > 1, "分段模式应有多于 1 个 inline");

				ApplyBugtracker(tb, null, new BugtrackerLinkDefinition[0]); // 无规则 → RestoreText
				Dispatcher.UIThread.RunJobs();
				Assert.Equal("Fix #123 now", tb.Text);
				Assert.True(tb.Inlines.Count <= 1, "恢复后应回到 Text 模式");

				// 搜索高亮同一路径（SHA/作者名走 ApplySearchHighlighting）
				ApplySearch(tb, "123");
				Dispatcher.UIThread.RunJobs();
				var runs = tb.Inlines.OfType<Run>().Select((Run r) => r.Text ?? "").ToList();
				Assert.Equal(string.Concat(runs), "Fix #123 now"); // 分段拼接 == 原文，无重复

				ApplySearch(tb, null);
				Dispatcher.UIThread.RunJobs();
				Assert.Equal("Fix #123 now", tb.Text);
				Assert.True(tb.Inlines.Count <= 1);

				window.Close();
			});
		}
	}
}
