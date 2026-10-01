// v4.4.0（二进制对比：查看器注册表架构）的测试。
// 覆盖注册表的判定与扩展点：
//   1) 内置查看器按优先级解析——GIF 字节归动图、静态图字节归静态图、无字节归文件卡片兜底；
//   2) 自定义查看器 Register 后可按优先级抢占内置实现，未命中时仍走内置链；
//   3) ResetToDefaults 恢复内置链（Dispose 里再兜一次，避免自定义注册污染其它用例）。
using System;
using System.IO;
using ForkPlus.UI.UserControls.BinaryDiff;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class BinaryViewerRegistryTests : IDisposable
	{
		// 与 ImageAnimationTests 同一份 2 帧 4×4 动图（每帧 120ms，loop=0 无限循环）。
		private const string AnimatedGifBase64 =
			"R0lGODlhBAAEAIEAAP8AAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQEDAAAACwAAAAABAAEAAAICQABCBxIsCCAgAAh+QQFDAABACwAAAAABAAEAIEA/wAAAAAAAAAAAAAICQABCBxIsCCAgAA7";

		// 4×4 静态 PNG（非动图）。
		private const string StaticPngBase64 =
			"iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAYAAACp8Z5+AAAAFUlEQVR4nGP8z8DwnwEJMDGgAcICAIPRAgYCkO9YAAAAAElFTkSuQmCC";

		/// <summary>只认 .probe 扩展名、优先级高于全部内置查看器的探针（验证注册即生效）。</summary>
		private sealed class ProbeViewer : IBinaryViewer
		{
			public BinaryViewerKind Kind => BinaryViewerKind.FileCard;

			public int Priority => 1000;

			public bool CanHandle(BinaryViewerRequest request)
			{
				return request?.Path != null && request.Path.EndsWith(".probe", StringComparison.OrdinalIgnoreCase);
			}
		}

		public void Dispose()
		{
			BinaryViewerRegistry.ResetToDefaults();
		}

		private static MemoryStream GifStream()
		{
			return new MemoryStream(Convert.FromBase64String(AnimatedGifBase64));
		}

		private static MemoryStream PngStream()
		{
			return new MemoryStream(Convert.FromBase64String(StaticPngBase64));
		}

		[Fact]
		public void Resolve_BuiltInViewers_PickAnimatedOverStatic()
		{
			// 动图优先：GIF 也是可解码位图，先问静态图就会被当成首帧。
			Assert.Equal(BinaryViewerKind.AnimatedImage, BinaryViewerRegistry.Resolve(new BinaryViewerRequest("anim.gif", GifStream())).Kind);
			Assert.Equal(BinaryViewerKind.StaticImage, BinaryViewerRegistry.Resolve(new BinaryViewerRequest("still.png", PngStream())).Kind);
		}

		[Fact]
		public void Resolve_WithoutBytes_FallsBackToFileCard()
		{
			// LFS 指针未 smudge / 大文件未预载：没有字节可判定 → 文件卡片。
			Assert.Equal(BinaryViewerKind.FileCard, BinaryViewerRegistry.Resolve(new BinaryViewerRequest("still.png", null)).Kind);
			Assert.Equal(BinaryViewerKind.FileCard, BinaryViewerRegistry.Resolve(new BinaryViewerRequest("blob.bin", null, isLfs: true, 1024L)).Kind);
		}

		[Fact]
		public void Register_CustomViewer_TakesPrecedenceUntilReset()
		{
			// 注册前：.probe 的图片字节按内置链归静态图。
			Assert.Equal(BinaryViewerKind.StaticImage, BinaryViewerRegistry.Resolve(new BinaryViewerRequest("a.probe", PngStream())).Kind);

			BinaryViewerRegistry.Register(new ProbeViewer());
			Assert.Equal(BinaryViewerKind.FileCard, BinaryViewerRegistry.Resolve(new BinaryViewerRequest("a.probe", PngStream())).Kind);
			// 探针不认的文件仍走内置链。
			Assert.Equal(BinaryViewerKind.StaticImage, BinaryViewerRegistry.Resolve(new BinaryViewerRequest("a.png", PngStream())).Kind);

			BinaryViewerRegistry.ResetToDefaults();
			Assert.Equal(BinaryViewerKind.StaticImage, BinaryViewerRegistry.Resolve(new BinaryViewerRequest("a.probe", PngStream())).Kind);
		}
	}
}