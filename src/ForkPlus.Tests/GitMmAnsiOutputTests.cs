// 回归测试（2026-09-12，v4.0.12，git mm 三连修）：
// ① "活动管理器 git mm tab 的命令输出有一堆不可见字符被当成乱码输出（ESC 之类）"：
//    根因：StripAnsiEscapes 原正则只匹配 CSI 序列（ESC[...终符），git mm 管道输出里的
//    OSC（ESC]0;title BEL）、字符集指定（ESC(B）、两字符转义（ESC7/ESC=）、行内 CR、
//    BEL 等控制字符全部漏网，TextBlock 把不可见字符渲染成乱码/豆腐块。
//    修复：AnsiEscapeRegex 扩展为 CSI/OSC/字符集/其他四类 + ControlCharRegex 清残余 C0。
// ② git mm sync OutOfMemoryException（freeze-20260912 转储：StringBuilder.ToString()
//    at GitRequest.ExecuteLong）：git mm sync 凭据失败时 AskPass 按子仓库循环输出，
//    stdout/stderr 捕获缓冲无上限增长。修复：BoundedPipeCapture 滚动窗口（上限 4M 字符/
//    管道，丢头部保尾部 + 截断标记）。
// ③ "命令输出不会自动滚到最下面"：活动管理器 git-mm 视图 Text 整体替换重置视口，
//    新输出到来不跟随。修复：stick-to-bottom（底部附近→更新后 SetScrollPosition(MaxValue)
//    滚到底）。本文件同时验证 SetScrollPosition 通道在 headless 下真实生效（AvaloniaEdit
//    的 TextEditor.ScrollToVerticalOffset 是空操作的前车之鉴——WpfCompat.Batch3 注释）。
using System;
using System.Linq;
using System.Text;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git.Interaction;
using ForkPlus.UI.Controls.Editor;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class GitMmAnsiOutputTests
	{
		// ===== ① ANSI/控制字符清洗（StripAnsiEscapes，git mm 输出两条展示路径共用）=====

		// 注意：C# 的 \x 变长转义是贪婪匹配（"\x1B7" 会解析成 U+1B7 单字符而非 ESC+'7'，
		// "\x07b" 会吞掉 b），一律用 \u001B/\u007F 固定 4 位转义 + \a 单字符转义，杜绝歧义。
		[Theory]
		[InlineData("plain text", "plain text")]                                       // 无转义：原样
		[InlineData("\u001B[32mgreen\u001B[0m ok", "green ok")]                         // CSI SGR 颜色（原已支持，回归守卫）
		[InlineData("\u001B[?25lhidden cursor", "hidden cursor")]                      // CSI 私有序列（光标隐藏）
		[InlineData("\u001B[2Kprogress", "progress")]                                  // CSI 擦行
		[InlineData("\u001B]0;title\aafter", "after")]                                 // OSC 设标题（BEL 终结）
		[InlineData("\u001B]0;title\u001B\\after", "after")]                           // OSC 设标题（ST 终结）
		[InlineData("\u001B]0;unterminated", "")]                                      // OSC 未终结（行截断）：整段吞掉
		[InlineData("a\u001B(Bb", "ab")]                                               // 字符集指定 ESC(B（用户报的 ESC 乱码形态）
		[InlineData("x\u001B7y", "xy")]                                                // 两字符转义 ESC7（保存光标）
		[InlineData("a\u001B=b", "ab")]                                                // 两字符转义 ESC=（应用键盘模式）
		[InlineData("noisy\abell", "noisybell")]                                       // 孤立 BEL（响铃）
		[InlineData("del\u007Fete", "delete")]                                         // DEL
		[InlineData("tab\tkept", "tab\tkept")]                                         // \t 保留（排版意义）
		[InlineData("45%\r78%\r100%", "45%78%100%")]                                   // 行内 CR（git 进度条重绘）：删除
		[InlineData("\u001B[1;31mred\u001B[0m \u001B(B ok\a", "red  ok")]               // 混合形态（ESC(B 前后各一空格）
		public void StripAnsiEscapes_RemovesAllEscapeAndControlChars(string input, string expected)
		{
			Assert.Equal(expected, ForkPlus.UI.UserControls.GitMmUserControl.StripAnsiEscapes(input));
		}

		[Fact]
		public void StripAnsiEscapes_NullAndEmpty_Passthrough()
		{
			Assert.Null(ForkPlus.UI.UserControls.GitMmUserControl.StripAnsiEscapes(null));
			Assert.Equal("", ForkPlus.UI.UserControls.GitMmUserControl.StripAnsiEscapes(""));
		}

		// ===== ② 有界管道捕获（git mm sync OOM 修复的截断数学）=====

		[Fact]
		public void BoundedPipeCapture_UnderLimit_KeepsEverything()
		{
			var capture = new GitRequest.BoundedPipeCapture(maxChars: 1000);
			for (int i = 0; i < 10; i++)
			{
				capture.AppendLine("line " + i);
			}
			string result = capture.ToTruncatedString();
			Assert.DoesNotContain("truncated", result);
			Assert.StartsWith("line 0" + Environment.NewLine, result);
			Assert.EndsWith("line 9" + Environment.NewLine, result);
		}

		[Fact]
		public void BoundedPipeCapture_OverLimit_KeepsTailAndMarksTruncation()
		{
			// 上限 200 字符：40 行 × (7 字符 + 换行) 会触发滚动截断。
			var capture = new GitRequest.BoundedPipeCapture(maxChars: 200);
			for (int i = 0; i < 40; i++)
			{
				capture.AppendLine("line" + i.ToString("D2")); // "line00".."line39"，6 字符
			}
			string result = capture.ToTruncatedString();
			// 截断标记存在
			Assert.Contains("[output truncated: dropped ~", result);
			// 头部行已被丢弃（丢一半再继续追加，早期行最先滚出窗口）
			Assert.DoesNotContain("line00" + Environment.NewLine, result);
			// 尾部行必须保留——错误诊断价值最高的最近输出
			Assert.EndsWith("line39" + Environment.NewLine, result);
			// 捕获总量受控：标记 + 残余窗口 ≤ 上限 + 标记长度余量
			Assert.True(result.Length < 200 + 200,
				"截断后总长应受控（上限+标记），实际 " + result.Length);
		}

		[Fact]
		public void BoundedPipeCapture_TailRetentionKeepsLastLinesVerbatim()
		{
			// 明确语义验证：丢头部后，尾部 N 行必须逐字无损（git mm sync 认证循环场景下
			// 尾部是定位失败子仓库的关键信息）。
			var capture = new GitRequest.BoundedPipeCapture(maxChars: 300);
			for (int i = 0; i < 200; i++)
			{
				capture.AppendLine("row " + i.ToString("D3")); // 8 字符/行
			}
			string result = capture.ToTruncatedString();
			string[] lines = result.Split(new[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
			// 最后一行无损
			Assert.Equal("row 199", lines[lines.Length - 1]);
			// 相邻行保持连续（无中间空洞——截断只发生在头部边界）
			for (int i = 1; i < lines.Length - 1; i++)
			{
				if (lines[i].StartsWith("row ", StringComparison.Ordinal) && lines[i + 1].StartsWith("row ", StringComparison.Ordinal))
				{
					int current = int.Parse(lines[i].Substring(4));
					int next = int.Parse(lines[i + 1].Substring(4));
					Assert.Equal(current + 1, next);
				}
			}
		}

		// ===== ③ SetScrollPosition 滚动通道（活动管理器 git-mm 视图 stick-to-bottom 依赖）=====

		[Fact]
		public void CodeEditor_SetScrollPosition_MaxValue_ScrollsToDocumentBottom()
		{
			bool pass = HeadlessAppBootstrap.Run(delegate
			{
				var window = new Window { Width = 600, Height = 300 };
				var editor = new CodeEditor { Width = 580, Height = 280 };
				window.Content = editor;
				window.Show();
				Dispatcher.UIThread.RunJobs();

				// 超过一屏的长文档（500 行 >> 280px 视口）
				var sb = new StringBuilder();
				for (int i = 0; i < 500; i++)
				{
					sb.Append("line ").Append(i).Append('\n');
				}
				editor.Text = sb.ToString();
				Dispatcher.UIThread.RunJobs();
				Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);

				// 判定走模板 PART_ScrollViewer（与活动管理器 git-mm 视图 IsOutputEditorNearBottom
				// 同通道；AvaloniaEdit 12 的 TextView 不暴露 Extent/Viewport）
				ScrollViewer sv = editor.GetVisualDescendants().OfType<ScrollViewer>()
					.FirstOrDefault((ScrollViewer x) => x.Name == "PART_ScrollViewer");
				if (sv == null)
				{
					return false; // 模板未套用：滚动通道不存在，视为失败
				}
				double maxOffset = sv.Extent.Height - sv.Viewport.Height;
				// 修复前形态守卫：初始视口不在底部
				bool initiallyNotAtBottom = maxOffset > 0 && sv.Offset.Y < maxOffset - 24.0;

				// 滚到底（活动管理器 git-mm 视图修复的滚动通道，同款调用）
				editor.SetScrollPosition(double.MaxValue);
				Dispatcher.UIThread.RunJobs();
				Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick(3);

				bool scrolledToBottom = sv.Offset.Y >= maxOffset - 1.0;
				window.Close();
				return initiallyNotAtBottom && scrolledToBottom;
			});
			Assert.True(pass,
				"SetScrollPosition(MaxValue) 应把 CodeEditor 滚到文档底部（活动管理器 git-mm 视图自动跟随的滚动通道；AvaloniaEdit 自家 ScrollToVerticalOffset 是空操作，通道必须走 PART_ScrollViewer）");
		}
	}
}
