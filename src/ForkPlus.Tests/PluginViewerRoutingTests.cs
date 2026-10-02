// v4.5.0（插件视图路由）的测试。守的是两条最容易写反的规则：
//   1) 通配 "*" 只做二进制兜底，绝不参与"该文件是不是图片"的判定（PathHelper.IsImagePath）——
//      否则每个文本文件都会被判成图片，文本/二进制 diff 的分流会崩，并让
//      GetWorkingDirectoryFileChangesGitCommand 里"跳过加载超大未跟踪文件"的性能闸门失效；
//   2) 具体扩展名永远优先于通配兜底——否则"支持所有二进制"的插件会从图片插件手里抢走 .png。
// 与 BinaryViewerRegistryTests 同集合：两者都改全局状态（查看器注册表 / PathHelper 认领钩子），
// 必须串行执行。
using System;
using System.Collections.Generic;
using System.IO;
using ForkPlus.Plugins;
using ForkPlus.UI.UserControls.BinaryDiff;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class PluginViewerRoutingTests : IDisposable
	{
		private readonly string _tempRoot;

		/// <summary>只放通配二进制插件的独立根：用来验证"没有图片插件时，通配也不能顶掉内置图片查看器"。</summary>
		private readonly string _wildcardOnlyRoot;

		private readonly List<PluginManager> _managers = new List<PluginManager>();

		public PluginViewerRoutingTests()
		{
			_tempRoot = Path.Combine(Path.GetTempPath(), "forkplus-plugin-routing-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_tempRoot);
			// 通配插件的优先级刻意给得比图片插件高：只要实现退化成"只看优先级"，
			// .png 就会被兜底插件抢走，用例随即失败。
			WritePlugin(_tempRoot, "binary", ManifestJson("com.example.binary", 300, @"[""*""]"));
			WritePlugin(_tempRoot, "image", ManifestJson("com.example.image", 100, @"["".png"", "".gif""]"));

			_wildcardOnlyRoot = Path.Combine(_tempRoot, "wildcard-only");
			Directory.CreateDirectory(_wildcardOnlyRoot);
			WritePlugin(_wildcardOnlyRoot, "binary", ManifestJson("com.example.binary", 150, @"[""*""]"));
		}

		public void Dispose()
		{
			foreach (PluginManager manager in _managers)
			{
				try
				{
					manager.Shutdown();
				}
				catch (Exception)
				{
				}
			}
			// Shutdown 会撤掉 PathHelper 的认领钩子，但注册表里的桥接查看器要自己复位，
			// 否则会污染后续用例的内置查看器链。
			BinaryViewerRegistry.ResetToDefaults();
			try
			{
				if (Directory.Exists(_tempRoot))
				{
					Directory.Delete(_tempRoot, recursive: true);
				}
			}
			catch (IOException)
			{
			}
		}

		private static string ManifestJson(string id, int priority, string extensionsJson)
		{
			return @"{
  ""id"": """ + id + @""",
  ""name"": """ + id + @""",
  ""version"": ""1.0.0"",
  ""apiVersion"": " + PluginProtocol.Version + @",
  ""host"": { ""executable"": ""view.dll"" },
  ""viewers"": [
    { ""id"": ""view"", ""priority"": " + priority + @", ""extensions"": " + extensionsJson + @" }
  ]
}";
		}

		private void WritePlugin(string root, string directoryName, string manifestJson)
		{
			string directory = Path.Combine(root, directoryName);
			Directory.CreateDirectory(directory);
			File.WriteAllText(Path.Combine(directory, PluginDiscovery.ManifestFileName), manifestJson);
		}

		/// <summary>用独立实例而非共享单例：Initialize 是"一次生效"的，单例会被前面的用例占住。</summary>
		private PluginManager NewManager(string pluginsRoot = null)
		{
			PluginManager manager = new PluginManager();
			manager.Initialize(pluginsRoot ?? _tempRoot, "ForkPlus.Tests", "0.0.0");
			_managers.Add(manager);
			return manager;
		}

		[Fact]
		public void ExplicitExtensionBeatsWildcard_EvenWhenWildcardHasHigherPriority()
		{
			PluginManager manager = NewManager();

			// .png 同时被两边"命中"：图片插件用具体后缀、二进制插件只有 "*"。
			// 显式更具体 → 图片插件赢，尽管通配插件的优先级（300）更高。
			Assert.Equal("com.example.image", manager.FindRenderViewer("shot.png").Manifest.Id);
			Assert.Equal("com.example.image", manager.FindRenderViewer("anim.gif").Manifest.Id);
			// 没人显式认领的二进制 → 通配兜底接管（含无扩展名的文件）。
			Assert.Equal("com.example.binary", manager.FindRenderViewer("firmware.dat").Manifest.Id);
			Assert.Equal("com.example.binary", manager.FindRenderViewer("Makefile").Manifest.Id);
		}

		[Fact]
		public void WildcardNeverCountsAsImagePathClaim()
		{
			PluginManager manager = NewManager();

			// PathHelper.IsImagePath 走的是 ClaimsPath：只认具体后缀。通配若在这里算数，
			// 每个文件都会变成"图片"。
			Assert.True(manager.ClaimsPath("shot.png"));
			Assert.False(manager.ClaimsPath("firmware.dat"));
			Assert.False(manager.ClaimsPath("Makefile"));
			// "能否交给插件渲染"是含通配的另一种问法。
			Assert.True(manager.ClaimsRenderablePath("shot.png"));
			Assert.True(manager.ClaimsRenderablePath("firmware.dat"));
			Assert.True(manager.ClaimsRenderablePath("Makefile"));
		}

		[Fact]
		public void BridgeViewer_RequiresBytesAndAdvertisesHighestPluginPriority()
		{
			PluginManager manager = NewManager();
			IBinaryViewer viewer = new PluginBinaryViewer(manager);
			using MemoryStream data = new MemoryStream(new byte[] { 1, 2, 3 });

			// 优先级取插件声明里的最高者：必须能压过内置动图（200），GIF 这类才轮到插件。
			Assert.Equal(300, viewer.Priority);
			// 有字节 → 插件接管（含只有 "*" 的 .dat）。
			Assert.True(viewer.CanHandle(new BinaryViewerRequest("firmware.dat", data)));
			// 没字节（LFS 指针未 smudge / 超大文件未预载）→ 交回内置文件卡片：
			// 插件拿不到内容只能渲出空白，不如老实显示卡片。
			Assert.False(viewer.CanHandle(new BinaryViewerRequest("firmware.dat", null)));
		}

		[Fact]
		public void WildcardDoesNotShadowBuiltInImageViewer()
		{
			// 只装通配二进制插件（没有图片插件）：宿主仍会把 .ico / .png 当作图片走内置查看器，
			// 通配若在这里也算数，就会把内置静态图顶掉、把图标渲成二进制结构图。
			PluginManager manager = NewManager(_wildcardOnlyRoot);
			IBinaryViewer viewer = new PluginBinaryViewer(manager);
			using MemoryStream data = new MemoryStream(new byte[] { 1, 2, 3 });

			// .ico 内置认、图片插件没声明 → 交回内置查看器，通配不接管。
			Assert.False(viewer.CanHandle(new BinaryViewerRequest("app.ico", data)));
			Assert.False(viewer.CanHandle(new BinaryViewerRequest("shot.png", data)));
			// 真正的未知二进制（无内置图片查看器可兜底）才轮到通配。
			Assert.True(viewer.CanHandle(new BinaryViewerRequest("firmware.dat", data)));
		}
	}
}