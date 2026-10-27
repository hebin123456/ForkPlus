using System.Collections.Generic;
using ForkPlus.UI.Plugins;
using ForkPlus.UI.UserControls.Preferences;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// v5.2.2：偏好设置 → 插件页搜索框过滤逻辑回归测试。
	/// 匹配口径：名称 / 描述 / 每个匹配扩展名做不区分大小写子串匹配；
	/// 空白搜索词匹配全部；扩展名带不带点均可命中（".png"/"png" 同条）。
	/// <see cref="PluginsUserControl.MatchesPluginFilter"/> 为纯函数，
	/// 用 <see cref="DiffViewPluginInfo"/> 样例直接断言，不依赖 Headless 与真实 plugins/ 目录。
	/// </summary>
	public class PluginsSearchFilterTests
	{
		private static DiffViewPluginInfo Info(
			string name,
			string description,
			string[] extensions,
			ForkPlus.UI.Plugins.DiffViewPluginStatus status = ForkPlus.UI.Plugins.DiffViewPluginStatus.Enabled)
		{
			return new DiffViewPluginInfo(
				"ForkPlus.Plugins.Sample", name, "1.0.0", description, 10,
				extensions, status, null, isBuiltIn: true);
		}

		private static readonly string[] Png = { ".png" };
		private static readonly string[] PngJpg = { ".png", ".jpg", ".jpeg" };
		private static readonly string[] Empty = { };

		[Fact]
		public void BlankFilter_MatchesEverything()
		{
			DiffViewPluginInfo info = Info("图像预览", "按扩展名路由", Png);
			Assert.True(PluginsUserControl.MatchesPluginFilter(info, null));
			Assert.True(PluginsUserControl.MatchesPluginFilter(info, ""));
			Assert.True(PluginsUserControl.MatchesPluginFilter(info, "   "));
		}

		[Fact]
		public void Name_MatchesSubstring_CaseInsensitive()
		{
			DiffViewPluginInfo info = Info("Image Preview", "显示图片", Png);
			Assert.True(PluginsUserControl.MatchesPluginFilter(info, "image"));
			Assert.True(PluginsUserControl.MatchesPluginFilter(info, "PREVIEW"));
			Assert.False(PluginsUserControl.MatchesPluginFilter(info, "video"));
		}

		[Fact]
		public void Description_MatchesSubstring()
		{
			DiffViewPluginInfo info = Info("图像预览", "在对比视图中渲染图片差异", Png);
			Assert.True(PluginsUserControl.MatchesPluginFilter(info, "渲染"));
			Assert.True(PluginsUserControl.MatchesPluginFilter(info, "图片差异"));
			Assert.False(PluginsUserControl.MatchesPluginFilter(info, "视频"));
		}

		[Fact]
		public void Extension_MatchesWithOrWithoutDot()
		{
			DiffViewPluginInfo info = Info("图像预览", "图片插件", Png);
			Assert.True(PluginsUserControl.MatchesPluginFilter(info, ".png"));
			Assert.True(PluginsUserControl.MatchesPluginFilter(info, "png"));
			Assert.True(PluginsUserControl.MatchesPluginFilter(info, "PNG"));
			Assert.False(PluginsUserControl.MatchesPluginFilter(info, ".jpg"));
		}

		[Fact]
		public void Extension_MatchesEachExtensionIndividually()
		{
			DiffViewPluginInfo info = Info("图像预览", "图片插件", PngJpg);
			Assert.True(PluginsUserControl.MatchesPluginFilter(info, "jpeg"));
			Assert.True(PluginsUserControl.MatchesPluginFilter(info, "jpg"));
			Assert.False(PluginsUserControl.MatchesPluginFilter(info, "gif"));
		}

		[Fact]
		public void AnyField_Hit_ReturnsMatch()
		{
			// 名称不中、描述不中、扩展名中 → 命中；三者全不中 → 不命中。
			DiffViewPluginInfo hit = Info("Hex 查看", "十六进制查看器", new[] { ".bin", ".dat" });
			Assert.True(PluginsUserControl.MatchesPluginFilter(hit, "dat"));

			DiffViewPluginInfo miss = Info("Hex 查看", "十六进制查看器", new[] { ".bin" });
			Assert.False(PluginsUserControl.MatchesPluginFilter(miss, "audio"));
		}

		[Fact]
		public void EmptyExtensions_DoesNotMatchExtensionQuery()
		{
			// 无扩展名声明（如加载失败条目）不因扩展名搜索命中，但仍可按名称/描述命中。
			DiffViewPluginInfo info = Info("Bad Plugin", "加载失败的坏条目", Empty, ForkPlus.UI.Plugins.DiffViewPluginStatus.Failed);
			Assert.False(PluginsUserControl.MatchesPluginFilter(info, "png"));
			Assert.True(PluginsUserControl.MatchesPluginFilter(info, "bad"));
		}

		[Fact]
		public void FilterIsTrimmed()
		{
			DiffViewPluginInfo info = Info("图像预览", "图片", Png);
			Assert.True(PluginsUserControl.MatchesPluginFilter(info, "  png  "));
			Assert.False(PluginsUserControl.MatchesPluginFilter(info, "  image  "));
		}
	}
}
