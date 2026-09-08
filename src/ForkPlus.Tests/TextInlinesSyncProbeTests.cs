// 微型探针：定位 Avalonia TextBlock Text ↔ Inlines 同步语义
// （bugtracker 链接修复的前置诊断）
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI.Controls;
using Xunit;
using SelTB = ForkPlus.UI.Controls.SelectableTextBlock;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class TextInlinesSyncProbeTests
	{
		private static void DumpInlines(string tag, TextBlock tb)
		{
			var inl = tb.Inlines;
			if (inl == null)
			{
				Console.WriteLine($"[{tag}] Inlines=null Text='{tb.Text}'");
				return;
			}
			var items = string.Join(" | ", inl.Select(i => i switch
			{
				Run r => $"Run'{r.Text}'",
				InlineUIContainer c => $"UI({c.Child?.GetType().Name})",
				_ => i.GetType().Name
			}));
			Console.WriteLine($"[{tag}] Inlines.Count={inl.Count} [{items}] Text='{tb.Text}'");
		}

		[Fact]
		public void Probe_TextInlines_Sync()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				// 步骤1：构造后（未渲染、未访问 Inlines）
				var tb = new SelTB { Text = "hello world" };
				DumpInlines("1-after-ctor", tb);

				// 步骤2：访问 Inlines getter
				var c1 = tb.Inlines;
				DumpInlines("2-after-get", tb);

				// 步骤3：Clear
				tb.Inlines.Clear();
				DumpInlines("3-after-clear", tb);

				// 步骤4：加自定义 inline
				tb.Inlines.Add(new Run("XYZ"));
				DumpInlines("4-after-add", tb);

				// 步骤5：渲染
				var host = new Border { Width = 200, Height = 40, Child = tb };
				var window = new Window { Width = 300, Height = 100, Content = host };
				window.Show();
				Dispatcher.UIThread.RunJobs();
				DumpInlines("5-after-render", tb);

				// 步骤6：再次 Clear 并渲染
				tb.Inlines.Clear();
				tb.Inlines.Add(new Run("ABC"));
				Dispatcher.UIThread.RunJobs();
				DumpInlines("6-after-reclear", tb);

				// 步骤7：修复方案验证——先 Text=null 再 Add，是否阻止隐式插入
				tb.Inlines.Clear();
				tb.Text = null;
				tb.Inlines.Add(new Run("PQR"));
				DumpInlines("7-text-null-add", tb);

				// 步骤8：Inlines 非空时设置 Text 会怎样（RestoreText 场景）
				var tb2 = new TextBlock { Text = "orig" };
				tb2.Inlines.Add(new Run("X"));
				DumpInlines("8a-add-x", tb2);
				tb2.Inlines.Clear();
				tb2.Text = "restored";
				DumpInlines("8b-restore-via-text", tb2);

				// 步骤9：inline 模式（Text 已被清空）后外部重设 Text（Refresh 场景）
				var tb3 = new TextBlock { Text = "old" };
				tb3.Inlines.Clear();
				tb3.Text = null;
				tb3.Inlines.Add(new Run("seg1"));
				tb3.Inlines.Add(new Run("seg2"));
				DumpInlines("9a-inline-mode", tb3);
				tb3.Text = "new";
				DumpInlines("9b-refresh-set-text", tb3);
				window.Close();
			});
		}

		[Fact]
		public void Probe_PlainTextBlock_Sync()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				// 对照组：原生 Avalonia TextBlock（非 Selectable）
				var tb = new TextBlock { Text = "hello world" };
				DumpInlines("A-after-ctor", tb);
				tb.Inlines.Clear();
				DumpInlines("B-after-clear", tb);
				tb.Inlines.Add(new Run("XYZ"));
				var host = new Border { Width = 200, Height = 40, Child = tb };
				var window = new Window { Width = 300, Height = 100, Content = host };
				window.Show();
				Dispatcher.UIThread.RunJobs();
				DumpInlines("C-after-render", tb);
				window.Close();
			});
		}
	}
}
