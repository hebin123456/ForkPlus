// v4.5.0（插件注册机制）的测试。
// 覆盖"插件清单 / 发现 / 线协议"这三层静态契约——它们是插件仓与主程序之间唯一的耦合面，
// 也是第三方（可含 GPL 代码）接入时最容易写错的地方：
//   1) PluginManifest.Parse：必填项、apiVersion 硬闸门、viewers 非空；
//   2) PluginViewerDescriptor.MatchesExtension：按扩展名做廉价预筛（大小写不敏感）；
//   3) PluginDiscovery.Discover：缺目录返回空、坏插件只报错不抛异常、好插件正常解析；
//   4) PluginChannel：LSP 风格分帧的写-读回环、连续多报文、坏报文头报错；
//   5) PluginProtocol：hello / render 请求的字段序列化契约。
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using ForkPlus.Plugins;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ForkPlus.Tests
{
	public class PluginSdkTests : IDisposable
	{
		private readonly string _tempRoot;

		public PluginSdkTests()
		{
			_tempRoot = Path.Combine(Path.GetTempPath(), "forkplus-plugin-tests-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(_tempRoot);
		}

		public void Dispose()
		{
			try
			{
				if (Directory.Exists(_tempRoot))
				{
					Directory.Delete(_tempRoot, recursive: true);
				}
			}
			catch (IOException)
			{
				// 测试清理尽力而为。
			}
		}

		private static string ValidManifestJson(string id = "com.example.demo")
		{
			return @"{
  ""id"": """ + id + @""",
  ""name"": ""Demo"",
  ""version"": ""1.0.0"",
  ""apiVersion"": " + PluginProtocol.Version + @",
  ""host"": { ""executable"": ""Demo.dll"" },
  ""viewers"": [
    { ""id"": ""view"", ""displayName"": ""Demo View"", ""priority"": 150, ""extensions"": ["".demo""] }
  ]
}";
		}

		private static string WildcardManifestJson()
		{
			return @"{
  ""id"": ""com.example.binary"",
  ""name"": ""Binary"",
  ""version"": ""1.0.0"",
  ""apiVersion"": " + PluginProtocol.Version + @",
  ""host"": { ""executable"": ""Binary.dll"" },
  ""viewers"": [
    { ""id"": ""binary"", ""displayName"": ""Binary View"", ""priority"": 100, ""extensions"": [""*""] }
  ]
}";
		}

		private string WritePlugin(string directoryName, string manifestJson)
		{
			string directory = Path.Combine(_tempRoot, directoryName);
			Directory.CreateDirectory(directory);
			File.WriteAllText(Path.Combine(directory, PluginDiscovery.ManifestFileName), manifestJson);
			return directory;
		}

		// ---- 清单校验 ----

		[Fact]
		public void Parse_ValidManifest_SucceedsAndResolvesFields()
		{
			PluginManifest manifest = PluginManifest.Parse(ValidManifestJson(), _tempRoot, out string error);

			Assert.Null(error);
			Assert.NotNull(manifest);
			Assert.Equal("com.example.demo", manifest.Id);
			Assert.Equal(_tempRoot, manifest.BaseDirectory);
			Assert.Single(manifest.ViewersOrEmpty);
			Assert.Equal("view", manifest.ViewersOrEmpty[0].Id);
			// host.executable 是相对路径，应相对清单目录解析出绝对路径。
			Assert.Equal(Path.GetFullPath(Path.Combine(_tempRoot, "Demo.dll")), manifest.ResolveHostExecutable());
		}

		[Theory]
		[InlineData(@"{ ""name"": ""x"", ""apiVersion"": 1, ""host"": { ""executable"": ""a.dll"" }, ""viewers"": [{ ""id"": ""v"" }] }")] // 缺 id
		[InlineData(@"{ ""id"": ""a"", ""apiVersion"": 1, ""viewers"": [{ ""id"": ""v"" }] }")] // 缺 host.executable
		[InlineData(@"{ ""id"": ""a"", ""apiVersion"": 1, ""host"": { ""executable"": ""a.dll"" }, ""viewers"": [] }")] // 无 viewers
		[InlineData(@"{ ""id"": ""a"", ""apiVersion"": 1, ""host"": { ""executable"": ""a.dll"" }, ""viewers"": [{ ""displayName"": ""no id"" }] }")] // viewer 缺 id
		public void Parse_MissingRequiredFields_ReturnsErrorAndNull(string json)
		{
			PluginManifest manifest = PluginManifest.Parse(json, _tempRoot, out string error);

			Assert.Null(manifest);
			Assert.False(string.IsNullOrWhiteSpace(error));
		}

		[Fact]
		public void Parse_ApiVersionMismatch_IsRejectedBeforeRunning()
		{
			// 协议版本是硬闸门：不符的插件直接拒载，而不是等到渲染时才炸。
			string json = ValidManifestJson().Replace("\"apiVersion\": " + PluginProtocol.Version, "\"apiVersion\": " + (PluginProtocol.Version + 1));

			PluginManifest manifest = PluginManifest.Parse(json, _tempRoot, out string error);

			Assert.Null(manifest);
			Assert.Contains("api version", error, StringComparison.OrdinalIgnoreCase);
		}

		[Fact]
		public void Parse_InvalidJson_ReturnsErrorInsteadOfThrowing()
		{
			PluginManifest manifest = PluginManifest.Parse("{ this is not json", _tempRoot, out string error);

			Assert.Null(manifest);
			Assert.False(string.IsNullOrWhiteSpace(error));
		}

		// ---- 扩展名预筛 ----

		[Fact]
		public void MatchesExtension_IsCaseInsensitiveAndRejectsUnknown()
		{
			PluginManifest manifest = PluginManifest.Parse(ValidManifestJson(), _tempRoot, out _);
			PluginViewerDescriptor viewer = manifest.ViewersOrEmpty[0];

			Assert.True(viewer.MatchesExtension("a.demo"));
			Assert.True(viewer.MatchesExtension("A.DEMO"));
			Assert.True(viewer.MatchesExtension("/repo/dir/file.demo"));
			Assert.False(viewer.MatchesExtension("a.other"));
			Assert.False(viewer.MatchesExtension("noextension"));
			Assert.False(viewer.MatchesExtension(null));
		}

		[Fact]
		public void MatchesPath_WildcardClaimsAnythingOnlyWhenAllowed()
		{
			// 通配 "*" 是"二进制兜底"视图：只在宿主已确认该文件是二进制、且允许兜底时才命中。
			PluginManifest manifest = PluginManifest.Parse(WildcardManifestJson(), _tempRoot, out string error);
			Assert.Null(error);
			PluginViewerDescriptor viewer = manifest.ViewersOrEmpty[0];

			Assert.True(viewer.IsWildcard);
			// 允许兜底：任何路径都命中，无扩展名的文件（Makefile）也覆盖。
			Assert.True(viewer.MatchesPath("a.dat", allowWildcard: true));
			Assert.True(viewer.MatchesPath("dir/Makefile", allowWildcard: true));
			// 不允许兜底（PathHelper.IsImagePath 走的就是这条路）：通配必须完全不生效——
			// 否则每个文本文件都会被判成"图片"，文本/二进制 diff 的分流会崩，
			// "跳过加载超大未跟踪文件"的性能闸门也会跟着失效。
			Assert.False(viewer.MatchesPath("a.dat", allowWildcard: false));
			Assert.False(viewer.MatchesExtension("a.dat"));
			Assert.False(viewer.MatchesExtension("dir/Makefile"));
		}

		[Theory]
		[InlineData(@"""extensions"": [""png""]")] // 漏了点：会静默永不命中，故装载时就拒
		[InlineData(@"""extensions"": [""*"", ""png""]")]
		[InlineData(@"""extensions"": []")]
		[InlineData(@"""extensions"": [""""]")]
		public void Parse_InvalidExtensionEntries_AreRejectedAtLoad(string extensionsFragment)
		{
			string json = @"{ ""id"": ""a"", ""apiVersion"": " + PluginProtocol.Version
				+ @", ""host"": { ""executable"": ""a.dll"" }, ""viewers"": [{ ""id"": ""v"", " + extensionsFragment + @" }] }";

			PluginManifest manifest = PluginManifest.Parse(json, _tempRoot, out string error);

			Assert.Null(manifest);
			Assert.False(string.IsNullOrWhiteSpace(error));
		}

		// ---- 目录发现 ----

		[Fact]
		public void Discover_MissingRoot_ReturnsEmpty()
		{
			List<PluginDiscoveryResult> results = PluginDiscovery.Discover(Path.Combine(_tempRoot, "does-not-exist"));

			Assert.Empty(results);
		}

		[Fact]
		public void Discover_LoadsGoodPluginAndReportsBadOneSeparately()
		{
			WritePlugin("good", ValidManifestJson());
			WritePlugin("bad", "{ not json");
			Directory.CreateDirectory(Path.Combine(_tempRoot, "no-manifest")); // 目录存在但没有 plugin.json

			List<PluginDiscoveryResult> results = PluginDiscovery.Discover(_tempRoot);

			Assert.Equal(3, results.Count);
			PluginDiscoveryResult good = results.Find(r => r.Manifest != null);
			Assert.NotNull(good);
			Assert.True(good.IsUsable);
			// 坏插件只报错，不抛异常、不影响其它插件。
			Assert.Equal(2, results.FindAll(r => !r.IsUsable && !string.IsNullOrWhiteSpace(r.Error)).Count);
		}

		// ---- 线协议分帧 ----

		[Fact]
		public void Channel_WriteThenRead_RoundTripsPayload()
		{
			using MemoryStream stream = new MemoryStream();
			using (PluginChannel writer = new PluginChannel(stream, ownsStream: false))
			{
				writer.Write("{\"hello\":1}");
			}
			stream.Position = 0;
			using PluginChannel reader = new PluginChannel(stream, ownsStream: false);

			Assert.Equal("{\"hello\":1}", reader.Read());
			// 流末尾：对端正常关闭 → null，而不是异常。
			Assert.Null(reader.Read());
		}

		[Fact]
		public void Channel_ReadsConsecutiveMessages()
		{
			using MemoryStream stream = new MemoryStream();
			using (PluginChannel writer = new PluginChannel(stream, ownsStream: false))
			{
				writer.Write("first");
				writer.Write("second");
			}
			stream.Position = 0;
			using PluginChannel reader = new PluginChannel(stream, ownsStream: false);

			Assert.Equal("first", reader.Read());
			Assert.Equal("second", reader.Read());
			Assert.Null(reader.Read());
		}

		[Fact]
		public void Channel_MalformedHeader_ThrowsIOException()
		{
			byte[] bytes = Encoding.ASCII.GetBytes("Content-Length: abc\r\n\r\n{}");
			using MemoryStream stream = new MemoryStream(bytes);
			using PluginChannel reader = new PluginChannel(stream, ownsStream: false);

			Assert.Throws<IOException>(() => reader.Read());
		}

		// ---- 请求序列化契约 ----

		[Fact]
		public void Protocol_CreateHello_CarriesProtocolVersionAndHostInfo()
		{
			PluginRequest request = PluginProtocol.CreateHello(1, "ForkPlus", "4.5.0");

			Assert.Equal(1, request.Id);
			Assert.Equal(PluginProtocol.MethodHello, request.Method);
			HelloParams parameters = request.Params.ToObject<HelloParams>();
			Assert.Equal(PluginProtocol.Version, parameters.ProtocolVersion);
			Assert.Equal("ForkPlus", parameters.HostName);
			Assert.Equal("4.5.0", parameters.HostVersion);
		}

		[Fact]
		public void Protocol_CreateRender_SerializesDataAsBase64AndDecodesBack()
		{
			byte[] data = new byte[] { 1, 2, 3, 250 };
			PluginRequest request = PluginProtocol.CreateRender(2, new RenderParams
			{
				ViewerId = "view",
				Path = "a.demo",
				Data = data,
				Width = 512,
				Height = 256,
				Theme = "dark"
			});

			// 线格式：Data 编成 base64 字符串（Newtonsoft 默认），插件解码后即拿到原字节。
			string json = JsonConvert.SerializeObject(request);
			Assert.Contains(Convert.ToBase64String(data), json, StringComparison.Ordinal);

			RenderParams roundTripped = JObject.Parse(json)["params"].ToObject<RenderParams>();
			Assert.Equal("view", roundTripped.ViewerId);
			Assert.Equal("a.demo", roundTripped.Path);
			Assert.Equal(data, roundTripped.Data);
			Assert.Equal(512, roundTripped.Width);
			Assert.Equal(256, roundTripped.Height);
			Assert.Equal("dark", roundTripped.Theme);
		}
	}
}