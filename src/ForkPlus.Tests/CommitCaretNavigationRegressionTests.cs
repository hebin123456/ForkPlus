// 回归测试（2026-09-16，"行末 CJK 字符后光标无法越过/无法删除"，commit message box）：
// 根因链：TextBox 在 Windows 上按 Enter 插入 "\r\n" → TextCharacters.TryGetShapeableLength
// 把 run 末尾换行符无条件并入当前 run（CJK 回退 run 吞入 "：\r\n"）→
// HarfBuzzTextShaper.MergeBreakPair 成形前把末尾 \r\n 改写为 U+200C（default-ignorable）
// → HarfBuzz 对 default-ignorable 做"替换为空格字形"处理：字体有空格字形则保留
// cluster（零宽），没有则整个丢弃。系统字体（如 Consolas）有空格字形所以纯 ASCII 行
// 正常；内嵌 Noto CJK 子集没有 U+0020 映射 → 回退 run 的换行被丢 → GlyphRun 丢失
// 换行 cluster → FindNearestCharacterHit 把 run 剩余字符全部算给最后一个 cluster
// （"：\r\n" 成一个 3 字符导航单元）→ 光标跳过行末 CJK 字符后的停靠点、删除错乱。
// 修复：两个内嵌 Noto 子集补 cmap[U+0020] = 零宽空字形 cid65531（空格是 ASCII，由主
// 字体排版，永远不会出现在 CJK 回退 run 里；此映射唯一消费者是 HB 的 ignorable 替换，
// 目标字形 hmtx advance=0，即使替换不置零宽度也零宽）。同时子集含零宽 U+200C 字形。
// 本测试锁定：字体资产映射 + 光标停靠序列 + 换行删除行为，防止字体重新子集化时回退。
using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class CommitCaretNavigationRegressionTests
	{
		// 字体资产回归：空格与 ZWNJ 必须映射到零宽字形（缺失会复发"光标跳过"）。
		[Fact]
		public void CjkFallbackFont_SpaceAndZwnj_MapToZeroWidthGlyph()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var embedded = new Typeface(FontFamily.Parse(ForkPlus.FontSetup.CjkFallbackFontUri));
				var cmap = embedded.GlyphTypeface.CharacterToGlyphMap;

				Assert.True(cmap.TryGetGlyph(0x0020, out var spaceGlyph),
					"U+0020 缺失映射：HarfBuzz 会丢弃回退 run 末尾换行符（光标 bug 复发）");
				Assert.True(embedded.GlyphTypeface.TryGetHorizontalGlyphAdvance(spaceGlyph, out var adv)
					&& adv == 0, "空格映射的字形必须零宽（hmtx advance=0）");
				Assert.True(cmap.TryGetGlyph(0x200C, out _), "U+200C(ZWNJ) 缺失映射");
				return 0;
			}).GetAwaiter().GetResult();
		}

		[Theory]
		[InlineData("234324 \uFF1A\r\nqweqwe", "全角冒号")]   // 用户原始复现
		[InlineData("中文测试\r\nqweqwe", "纯中文行")]        // 用户澄清的泛化场景
		[InlineData("修复：问题单\r\n第二行", "CJK+全角标点")]
		public void CjkLineEnd_CaretWalk_VisitsEveryStop(string text, string label)
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var tb = MakeBox(text);
				var seq = new List<int>();
				for (int i = 0; i < text.Length + 3; i++)
				{
					Press(tb, Key.Right);
					seq.Add(tb.CaretIndex);
					if (tb.CaretIndex >= text.Length) break;
				}

				// 期望序列：除 "\r\n" 内部（\r 与 \n 之间）外每个字符位置都是停靠点。
				var expected = new List<int>();
				for (int p = 1; p <= text.Length; p++)
				{
					bool insideCrLf = p > 0 && p < text.Length && text[p - 1] == '\r' && text[p] == '\n';
					if (!insideCrLf) expected.Add(p);
				}

				Assert.True(tb.CaretIndex >= text.Length,
					$"{label}: 光标卡在 {tb.CaretIndex}/{text.Length}（walk={string.Join(",", seq)}）");
				Assert.Equal(string.Join(",", expected), string.Join(",", seq));
				return 0;
			}).GetAwaiter().GetResult();
		}

		[Fact]
		public void CjkLineEnd_BackspaceAndDelete_RemoveCrLfUnitOnly()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				const string text = "234324 \uFF1A\r\nqweqwe";

				// 行首 Backspace：只删 "\r\n" 一个单元，冒号保留
				var tb = MakeBox(text);
				tb.CaretIndex = 10; // 第二行首字符前
				Press(tb, Key.Back);
				Assert.Equal("234324 \uFF1Aqweqwe", tb.Text);
				Assert.Equal(8, tb.CaretIndex);

				// 冒号后 Delete：删掉 "\r\n" 单元，而非吞掉冒号或第二行首字符
				var tb2 = MakeBox(text);
				tb2.CaretIndex = 8; // 冒号后
				Press(tb2, Key.Delete);
				Assert.Equal("234324 \uFF1Aqweqwe", tb2.Text);

				// 光标可停留在冒号后（用户报障：无法越过冒号）
				var tb3 = MakeBox(text);
				tb3.CaretIndex = 8;
				Assert.Equal(8, tb3.CaretIndex);
				Press(tb3, Key.Right);
				Assert.Equal(10, tb3.CaretIndex); // 越过 \r\n 单元到第二行
				Press(tb3, Key.Left);
				Assert.Equal(8, tb3.CaretIndex); // 退回冒号后
				return 0;
			}).GetAwaiter().GetResult();
		}

		private static TextBox MakeBox(string text)
		{
			var tb = new global::ForkPlus.UI.Controls.CommitDescriptionTextBox
			{
				Text = text,
				Width = 200,
				Height = 80,
				TextWrapping = Avalonia.Media.TextWrapping.Wrap,
				AcceptsReturn = true
			};
			var window = new Window { Width = 300, Height = 150, Content = tb };
			window.Show();
			Dispatcher.UIThread.RunJobs();
			tb.CaretIndex = 0;
			return tb;
		}

		private static void Press(TextBox tb, Key key)
		{
			tb.RaiseEvent(new KeyEventArgs
			{
				RoutedEvent = InputElement.KeyDownEvent,
				Key = key,
				KeyModifiers = KeyModifiers.None
			});
			Dispatcher.UIThread.RunJobs();
		}
	}
}
