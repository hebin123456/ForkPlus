using System.Collections.Generic;
using System.Linq;
using ForkPlus.Plugins.Abstractions;
using ForkPlus.Plugins.BuiltIn.HexDiff;
using ForkPlus.Plugins.BuiltIn.ImageDiff;
using Xunit;

namespace ForkPlus.Plugins.Tests
{
	/// <summary>
	/// v5.0.0：内置对比视图插件契约测试——扩展名表、优先级、通配兜底声明。
	/// v5.0.0：两个内置插件——图片（forkplus.image，精确扩展名）+ Hex（forkplus.hex，
	/// 二进制通配兜底，原 forkplus.binary 职责并入）。路由正确性（用户绑定 &gt; 精确
	/// 扩展名 &gt; 通配）由宿主 DiffViewPluginRegistry 保证，这里锁定插件自身对外
	/// 声明的元数据不被意外改动。
	/// </summary>
	public class ImageDiffPluginTests
	{
		[Fact]
		public void ImageExtensions_MatchHostImagePathTable()
		{
			string[] expected = new string[8] { ".bmp", ".gif", ".png", ".jpg", ".jpeg", ".ico", ".tga", ".webp" };
			Assert.Equal(expected, ImageDiffPlugin.ImageExtensions);
		}

		[Fact]
		public void ImageDiffPlugin_Metadata()
		{
			ImageDiffPlugin plugin = new ImageDiffPlugin();
			Assert.Equal("forkplus.image", plugin.Id);
			Assert.Equal("Image", plugin.DisplayNameKey);
			Assert.Equal(100, plugin.Priority);
			Assert.Equal(ImageDiffPlugin.ImageExtensions, plugin.FileExtensions);
		}

		[Fact]
		public void ImageDiffPlugin_CanHandle_AlwaysTrue()
		{
			ImageDiffPlugin plugin = new ImageDiffPlugin();
			Assert.True(plugin.CanHandle(new DiffViewRequest("a.png", 1L, 2L)));
			Assert.True(plugin.CanHandle(new DiffViewRequest(null, null, null)));
		}

		[Fact]
		public void HexDiffPlugin_IsWildcardFallback()
		{
			HexDiffPlugin plugin = new HexDiffPlugin();
			Assert.Equal("forkplus.hex", plugin.Id);
			Assert.Equal("Hex", plugin.DisplayNameKey);
			Assert.Equal(0, plugin.Priority);
			Assert.NotNull(plugin.FileExtensions);
			Assert.Contains("*", plugin.FileExtensions);
			Assert.True(plugin.CanHandle(new DiffViewRequest("a.otf", null, null)));
		}

		[Fact]
		public void ImageDiffPlugin_PriorityAboveHexFallback()
		{
			Assert.True(new ImageDiffPlugin().Priority > new HexDiffPlugin().Priority);
		}

		[Theory]
		[InlineData(".png")]
		[InlineData(".PNG")]
		[InlineData(".jpg")]
		[InlineData(".jpeg")]
		[InlineData(".gif")]
		[InlineData(".webp")]
		[InlineData(".bmp")]
		[InlineData(".ico")]
		[InlineData(".tga")]
		public void ImageExtensions_CaseInsensitiveLookup(string extension)
		{
			IReadOnlyList<string> extensions = ImageDiffPlugin.ImageExtensions;
			bool contains = extensions.Any((string x) => string.Equals(x, extension, System.StringComparison.OrdinalIgnoreCase));
			Assert.True(contains, $"image extension table should contain {extension}");
		}
	}
}
