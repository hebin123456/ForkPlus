// 回归测试（2026-09-04，"二进制对比（Hex Diff）显示一片空白"）：
// 根因：Avalonia ControlTheme 不像 WPF DefaultStyleKey 沿基类链匹配派生类——
// HexEditor（: AvaloniaEdit.TextEditor）的 StyleKey 是自身类型，官方 {x:Type TextEditor}
// 主题匹配不到 → 无模板 → 无 ScrollViewer → TextArea 不挂视觉树：Text 已赋值但
// TextView.Bounds=0x0、VisualLines 永不重建，渲染一片空白（工具栏/MD5 行正常）。
// 修复：Commonresources.axaml 定义 {x:Type HexEditor} 专属 ControlTheme，
// HexEditor 构造函数取资源挂 Theme（与 CodeEditor 同模式）。
// 本测试在两种容器下验证模板真正应用：视觉行构建（vlines>0）+ ScrollViewer 在树。
// v5.0.0：HexDiffUserControl / HexEditor 迁移至插件工程（SetContent 直接收双侧字节流）。
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Plugins.BuiltIn.HexDiff;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class HexEditorThemeAppliedTests
	{
		private static MemoryStream MakeBytes(int len, int seed)
		{
			byte[] data = Enumerable.Range(0, len).Select(i => (byte)((i * seed + 13) % 253)).ToArray();
			return new MemoryStream(data);
		}

		private static async Task PumpAsync(int ms)
		{
			await Task.Delay(ms);
			Dispatcher.UIThread.RunJobs();
			Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
			await Task.Delay(ms);
			Dispatcher.UIThread.RunJobs();
		}

		// 理论：同一容器（Window）直接挂 HexDiffUserControl —— 修复前
		// text=2463 但 tvBounds=0x0 / vlines 不重建 / sv=null；修复后三者全部恢复。
		[Theory]
		[InlineData(true)]  // 普通 Window
		[InlineData(false)] // CustomWindow（真实窗口模板：LayoutTransformControl + VisualLayerManager）
		public async Task HexEditor_TemplateApplied_TextViewBuildsLines(bool plainWindow)
		{
			HeadlessAppBootstrap.EnsureStarted();
			var failures = new System.Collections.Generic.List<string>();
			await Dispatcher.UIThread.InvokeAsync(async delegate
			{
				var hex = new HexDiffUserControl();
				Window window = plainWindow
					? new Window { Width = 1000, Height = 600, Content = hex }
					: new ForkPlus.UI.CustomWindow { Width = 1000, Height = 600, Content = hex };
				window.Show();
				Dispatcher.UIThread.RunJobs();
				hex.SetContent(MakeBytes(512, 1), MakeBytes(640, 7));
				await PumpAsync(400);

				var editors = hex.GetVisualDescendants().OfType<HexEditor>().ToArray();
				if (editors.Length != 2)
				{
					failures.Add("应有 2 个 HexEditor，实际 " + editors.Length);
				}
				foreach (var ed in editors)
				{
					if (ed.Text == null || ed.Text.Length == 0)
					{
						failures.Add("编辑器文本为空");
					}
					// 模板应用的直接证据：视觉树里出现模板部件 ScrollViewer
					var sv = ed.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
					if (sv == null)
					{
						failures.Add("模板未应用：视觉树中无 ScrollViewer（修复前症状）");
					}
					// 布局与行构建：TextView 有尺寸且构建了视觉行
					var tv = ed.TextArea.TextView;
					if (tv.Bounds.Width <= 0 || tv.Bounds.Height <= 0)
					{
						failures.Add("TextView 尺寸为 0x0（修复前症状：" + tv.Bounds + "）");
					}
					try
					{
						if (tv.VisualLines == null || tv.VisualLines.Count == 0)
						{
							failures.Add("视觉行未构建（vlines=0，修复前症状）");
						}
					}
					catch (AvaloniaEdit.Rendering.VisualLinesInvalidException)
					{
						failures.Add("视觉行处于失效未重建状态（修复前症状）");
					}
				}
				window.Close();
				return 0;
			});

			Assert.True(failures.Count == 0, string.Join("; ", failures));
		}
	}
}
