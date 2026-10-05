using ForkPlus.Plugins;
using Xunit;

namespace ForkPlus.Plugins.Tests
{
	/// <summary>
	/// v5.0.0：插件侧尺寸格式化测试。输出口径必须与主工程 FileSizeFormatter/FileHelper
	/// 完全一致（同一 UI 展示），否则同一文件在宿主卡片与插件视图显示两种大小。
	/// </summary>
	public class PluginSizeFormatTests
	{
		[Theory]
		[InlineData(0L, "0 bytes")]
		[InlineData(1L, "1 bytes")]
		[InlineData(512L, "512 bytes")]
		[InlineData(1023L, "1023 bytes")]
		public void Format_BelowOneKB_UsesBytes(long size, string expected)
		{
			Assert.Equal(expected, PluginSizeFormat.Format(size));
		}

		[Theory]
		[InlineData(1024L, "1.00 KB")]
		[InlineData(1536L, "1.50 KB")]
		[InlineData(9216L, "9.00 KB")]
		public void Format_BelowTenKB_TwoDecimals(long size, string expected)
		{
			Assert.Equal(expected, PluginSizeFormat.Format(size));
		}

		[Fact]
		public void Format_TenToHundredKB_OneDecimal()
		{
			Assert.Equal("10.0 KB", PluginSizeFormat.Format(10L * 1024L));
			Assert.Equal("99.9 KB", PluginSizeFormat.Format((long)(99.9 * 1024.0)));
		}

		[Fact]
		public void Format_AboveHundred_NoDecimal()
		{
			Assert.Equal("100 KB", PluginSizeFormat.Format(100L * 1024L));
			Assert.Equal("1.00 MB", PluginSizeFormat.Format(1024L * 1024L));
			Assert.Equal("1.00 GB", PluginSizeFormat.Format(1024L * 1024L * 1024L));
		}

		[Fact]
		public void ReadableFileSizeInBytes_GroupsThousands()
		{
			Assert.Equal("0 B", PluginSizeFormat.ReadableFileSizeInBytes(0L));
			Assert.Equal("1,234,567 B", PluginSizeFormat.ReadableFileSizeInBytes(1234567L));
		}

		[Fact]
		public void ReadableFileSize_AppendsByteCount()
		{
			Assert.Equal("1.00 KB (1,024 B)", PluginSizeFormat.ReadableFileSize(1024L));
			Assert.Equal("0 bytes (0 B)", PluginSizeFormat.ReadableFileSize(0L));
		}

		[Fact]
		public void ReadableFileSize_WithoutByteCount()
		{
			Assert.Equal("2 bytes", PluginSizeFormat.ReadableFileSize(2L, addSizeInBytes: false));
		}
	}
}
