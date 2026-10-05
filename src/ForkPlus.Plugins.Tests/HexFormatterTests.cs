using System.Text;
using ForkPlus.Plugins.BuiltIn.HexDiff;
using Xunit;

namespace ForkPlus.Plugins.Tests
{
	/// <summary>
	/// v5.0.0：Hex 视图格式化逻辑测试——行布局（offset/hex/ascii 列宽）、半行分组空格、
	/// 追加段 startOffset 连续编号、选中字符区间反推字节区间（“复制为原始字节”用）。
	/// </summary>
	public class HexFormatterTests
	{
		[Fact]
		public void Format_Empty_ReturnsEmpty()
		{
			Assert.Equal("", HexFormatter.Format(new byte[0], 16, showOffset: true, showAscii: true));
		}

		[Fact]
		public void Format_Null_ReturnsEmpty()
		{
			Assert.Equal("", HexFormatter.Format(null, 16, showOffset: true, showAscii: true));
		}

		[Fact]
		public void Format_FullRow_KnownLayout()
		{
			// "Hello World!\n" = 13 字节；bytesPerRow=16 → 单行，行尾 3 字节空位补齐。
			byte[] bytes = Encoding.ASCII.GetBytes("Hello World!\n");
			string formatted = HexFormatter.Format(bytes, 16, showOffset: true, showAscii: true);
			// offset 列 10 字符 + hex 列（含半行分组空格）+ 9 个行尾空位 + ascii 前导 2 空格 + ascii。
			// 字节 12 之后：分隔 1 + 空位 3+3+2 = 9 空格；ascii 列前导 2 空格 → 共 11 空格。
			string expected = "00000000  48 65 6C 6C 6F 20 57 6F  72 6C 64 21 0A"
				+ new string(' ', 11)
				+ "Hello World!.";
			Assert.Equal(expected, formatted);
		}

		[Fact]
		public void Format_WithoutOffsetColumn_OmitsOffset()
		{
			byte[] bytes = new byte[3] { 0x41, 0x42, 0x43 };
			string formatted = HexFormatter.Format(bytes, 16, showOffset: false, showAscii: false);
			// i=0..2 各 3 字符（hex+分隔）；i=3..14 各 3 空格；i=7 处多 1 分组空格；i=15 两空格。
			// "43" 之后合计 1 + 36 + 1 + 2 = 40 空格。
			Assert.Equal("41 42 43" + new string(' ', 40), formatted);
		}

		[Fact]
		public void Format_StartOffset_ContinuesRowNumbering()
		{
			// “加载更多”追加段：offset 必须接续前段（00000010 起），否则视觉与选中反推均错位。
			byte[] bytes = Encoding.ASCII.GetBytes("ABC");
			string formatted = HexFormatter.Format(bytes, 16, showOffset: true, showAscii: false, startOffset: 16);
			Assert.StartsWith("00000010  41 42 43", formatted);
		}

		[Fact]
		public void Format_MultipleRows_JoinsWithNewline()
		{
			byte[] bytes = new byte[20];
			for (int i = 0; i < bytes.Length; i++)
			{
				bytes[i] = (byte)i;
			}
			string formatted = HexFormatter.Format(bytes, 16, showOffset: true, showAscii: true);
			string[] lines = formatted.Split('\n');
			Assert.Equal(2, lines.Length);
			Assert.StartsWith("00000000  00 01 02", lines[0]);
			Assert.StartsWith("00000010  10 11 12", lines[1]);
		}

		[Fact]
		public void CharOffsetsToByteRange_Row0Selection_MapsToByteIndices()
		{
			// 行长 = 10（offset）+ 47（hex，含半行分组）+ 18（ascii 前导+16）= 76；行距 77。
			// 字节 0 起始列 10；字节 5 起始列 10 + 5*3 = 25。
			ByteRange range = HexFormatter.CharOffsetsToByteRange(10, 25, 16, showOffset: true, showAscii: true);
			Assert.Equal(0, range.Start);
			Assert.Equal(5, range.End);
		}

		[Fact]
		public void CharOffsetsToByteRange_SecondRow_AddsRowBase()
		{
			// 第二行字节 0 起始字符 = 77 + 10；字节 3 起始 = 77 + 10 + 3*3。
			ByteRange range = HexFormatter.CharOffsetsToByteRange(87, 96, 16, showOffset: true, showAscii: true);
			Assert.Equal(16, range.Start);
			Assert.Equal(19, range.End);
		}

		[Fact]
		public void CharOffsetsToByteRange_OffsetColumn_ClampsToZero()
		{
			// 选区落在 offset 列内 → 字节 0。
			ByteRange range = HexFormatter.CharOffsetsToByteRange(0, 5, 16, showOffset: true, showAscii: true);
			Assert.Equal(0, range.Start);
			Assert.Equal(0, range.End);
		}
	}
}
