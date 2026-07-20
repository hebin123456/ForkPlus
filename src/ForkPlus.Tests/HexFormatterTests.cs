using ForkPlus.UI.Controls.Editor.Hex;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// v3.1.0 Hex 格式化器单元测试。覆盖 Format（offset/hex/ascii 列布局、半行分组、不可打印字符）、
	/// CharOffsetsToByteRange（字符偏移反推字节区间）。零外部依赖。
	/// </summary>
	public class HexFormatterTests
	{
		[Fact]
		public void Format_NullBytes_ReturnsEmpty()
		{
			Assert.Equal("", HexFormatter.Format(null, 16, true, true));
		}

		[Fact]
		public void Format_EmptyBytes_ReturnsEmpty()
		{
			Assert.Equal("", HexFormatter.Format(new byte[0], 16, true, true));
		}

		[Fact]
		public void Format_SingleByte_IncludesHexAndAscii()
		{
			// 'A' = 0x41
			string result = HexFormatter.Format(new byte[] { 0x41 }, 16, true, true);
			// offset 00000000 + 2 空格 + hex "41" + 14 个空格位 + 半行分组空格 + 2 空格 + ascii "A"
			Assert.Contains("00000000", result);
			Assert.Contains("41", result);
			Assert.Contains("A", result);
			Assert.False(result.EndsWith("\n"));
		}

		[Fact]
		public void Format_MultipleRows_SeparatedByNewline()
		{
			// 17 字节，bytesPerRow=16，应有 2 行
			byte[] bytes = new byte[17];
			for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)('A' + (i % 26));
			string result = HexFormatter.Format(bytes, 16, true, true);
			string[] lines = result.Split('\n');
			Assert.Equal(2, lines.Length);
		}

		[Fact]
		public void Format_ShowOffsetFalse_OmitsOffsetColumn()
		{
			string withOffset = HexFormatter.Format(new byte[] { 0x41 }, 16, true, false);
			string withoutOffset = HexFormatter.Format(new byte[] { 0x41 }, 16, false, false);
			Assert.StartsWith("00000000", withOffset);
			Assert.False(withoutOffset.StartsWith("00000000"));
		}

		[Fact]
		public void Format_ShowAsciiFalse_OmitsAsciiColumn()
		{
			string withAscii = HexFormatter.Format(new byte[] { 0x41 }, 16, false, true);
		string withoutAscii = HexFormatter.Format(new byte[] { 0x41 }, 16, false, false);
		Assert.Contains("A", withAscii);
		Assert.DoesNotContain("A", withoutAscii);
		}

		[Fact]
		public void Format_UnprintableByte_ShowsAsDot()
		{
			// 0x00 不可打印
			string result = HexFormatter.Format(new byte[] { 0x00 }, 16, false, true);
			Assert.Contains("00", result);  // hex 列
			// ascii 列应以 . 表示
			Assert.Contains(".", result);
		}

		[Fact]
		public void Format_0x1F_Unprintable_ShowsAsDot()
		{
			string result = HexFormatter.Format(new byte[] { 0x1F }, 16, false, true);
			// ascii 列应是 . 不是 0x1F 字符
			Assert.EndsWith(".", result);
		}

		[Fact]
		public void Format_0x7F_Unprintable_ShowsAsDot()
		{
			string result = HexFormatter.Format(new byte[] { 0x7F }, 16, false, true);
			Assert.EndsWith(".", result);
		}

		[Fact]
		public void Format_0x20_Printable_ShowsAsSpace()
		{
			// 0x20 是空格，可打印
			string result = HexFormatter.Format(new byte[] { 0x20 }, 16, false, true);
			Assert.Contains(" ", result);
		}

		[Fact]
		public void Format_0x7E_Printable_ShowsAsTilde()
		{
			// 0x7E = '~'，可打印边界
			string result = HexFormatter.Format(new byte[] { 0x7E }, 16, false, true);
			Assert.EndsWith("~", result);
		}

		[Fact]
		public void Format_HalfRowGrouping_HasExtraSpace()
		{
			// bytesPerRow=16，半行在第 8 字节后应有额外空格
			byte[] bytes = new byte[16];
			for (int i = 0; i < 16; i++) bytes[i] = 0x41;
			string result = HexFormatter.Format(bytes, 16, false, false);
			// 第 8 字节后应有 2 个连续空格（一个分隔符 + 半行分组空格）
			// 形如 "41 41 41 41 41 41 41 41  41 ..."
			Assert.Contains("41 41 41 41 41 41 41 41  41", result);
		}

		[Fact]
		public void Format_LastRowPartial_PadsHexColumnWithSpaces()
		{
			// bytesPerRow=16，3 字节，末行 hex 列应补 13 个字节位的空格
			byte[] bytes = new byte[] { 0x41, 0x42, 0x43 };
			string result = HexFormatter.Format(bytes, 16, false, true);
			// ascii 列只显示 "ABC"，不应有补齐字符
			Assert.EndsWith("ABC", result);
		}

		[Fact]
		public void Format_BytesPerRow8_NoHalfRowGrouping()
		{
			// bytesPerRow=8，half=4，半行分组应在第 4 字节后
			byte[] bytes = new byte[8];
			for (int i = 0; i < 8; i++) bytes[i] = 0x41;
			string result = HexFormatter.Format(bytes, 8, false, false);
			Assert.Contains("41 41 41 41  41 41 41 41", result);
		}

		[Fact]
		public void Format_OffsetIs8HexChars()
		{
			// 多行场景，offset 应是 8 位 16 进制
			byte[] bytes = new byte[32];
			string result = HexFormatter.Format(bytes, 16, true, false);
			string[] lines = result.Split('\n');
			Assert.StartsWith("00000000", lines[0]);
			Assert.StartsWith("00000010", lines[1]);
		}

		[Fact]
		public void CharOffsetsToByteRange_BothZero_ReturnsZeroRange()
		{
			ByteRange range = HexFormatter.CharOffsetsToByteRange(0, 0, 16, true, true);
			Assert.Equal(0, range.Start);
			Assert.Equal(0, range.End);
		}

		[Fact]
		public void CharOffsetsToByteRange_OffsetColumn_ReturnsZeroByte()
		{
			// 列 0~9 是 offset（8 hex + 2 空格），都映射到字节 0
			ByteRange range = HexFormatter.CharOffsetsToByteRange(0, 5, 16, true, false);
			Assert.Equal(0, range.Start);
			Assert.Equal(0, range.End);
		}

		[Fact]
		public void CharOffsetsToByteRange_SecondRow_AccountsForNewline()
		{
			// 第二行起始字节应是 bytesPerRow
			ByteRange range = HexFormatter.CharOffsetsToByteRange(0, 0, 16, false, false);
			// 第一行第一字节
			Assert.Equal(0, range.Start);
		}

		[Fact]
		public void CharOffsetsToByteRange_NoOffset_NoOffsetWidth()
		{
			// showOffset=false，col 0 直接是 hex 列起始
			ByteRange range = HexFormatter.CharOffsetsToByteRange(0, 3, 16, false, false);
			// col 0~2 是第一字节（"41"），col 3 是分隔空格
			Assert.Equal(0, range.Start);
			Assert.Equal(1, range.End);
		}
	}
}
