using System;
using System.IO;
using System.Linq;
using ForkPlus.Plugins.BuiltIn.ImageDiff;
using Xunit;

namespace ForkPlus.Plugins.Tests
{
	/// <summary>
	/// v5.0.0：二进制查看器注册表路由测试——动图优先于静态图、无字节走文件卡片兜底、
	/// 同 Kind 注册即替换。动图判定经 SkiaSharp SKCodec（无需图形平台，解码即可）。
	/// </summary>
	public class BinaryViewerRegistryTests
	{
		/// <summary>PIL 生成的 2x2 红蓝两帧 GIF89a（含 NETSCAPE2.0 循环扩展），供 SKCodec 解出 FrameCount=2。
		/// 注意两帧内容必须有可见差异：完全相同的帧（如两帧同色 1x1）SkiaSharp 会合并报告 FrameCount=1。
		/// 生成：Image.new('RGB',(2,2),(255,0,0)).save(save_all=True, append_images=[蓝帧], duration=100, loop=0)。</summary>
		private static readonly byte[] MinimalAnimatedGif = Convert.FromBase64String(
			"R0lGODlhAgACAIEAAP8AAAAAAAAAAAAAACH/C05FVFNDQVBFMi4wAwEAAAAh+QQACgAAACwAAAAAAgACAAAIBgABCAQQEAAh+QQBCgABACwAAAAAAgACAIEAAP8AAAAAAAAAAAAIBgABCAQQEAA7");

		private static MemoryStream StreamOf(params byte[] data)
		{
			return new MemoryStream(data.ToArray());
		}

		[Fact]
		public void Resolve_NullRequest_FallsBackToFileCard()
		{
			IBinaryViewer viewer = BinaryViewerRegistry.Resolve(null);
			Assert.Equal(BinaryViewerKind.FileCard, viewer.Kind);
		}

		[Fact]
		public void Resolve_WithoutData_FallsBackToFileCard()
		{
			IBinaryViewer viewer = BinaryViewerRegistry.Resolve(new BinaryViewerRequest("big.bin", null));
			Assert.Equal(BinaryViewerKind.FileCard, viewer.Kind);
		}

		[Fact]
		public void Resolve_AnyReadableData_IsStaticImage()
		{
			// 非图片字节：AnimatedImageViewer 探测失败（SKCodec 返回 null/异常）→ 静态图查看器兜住。
			IBinaryViewer viewer = BinaryViewerRegistry.Resolve(new BinaryViewerRequest("a.txt", StreamOf(0x01, 0x02, 0x03)));
			Assert.Equal(BinaryViewerKind.StaticImage, viewer.Kind);
		}

		[Fact]
		public void Resolve_MultiFrameGif_IsAnimatedImage()
		{
			IBinaryViewer viewer = BinaryViewerRegistry.Resolve(new BinaryViewerRequest("anim.gif", StreamOf(MinimalAnimatedGif)));
			Assert.Equal(BinaryViewerKind.AnimatedImage, viewer.Kind);
		}

		[Fact]
		public void Register_SameKind_ReplacesBuiltIn()
		{
			try
			{
				StubViewer stub = new StubViewer(BinaryViewerKind.StaticImage, 999);
				BinaryViewerRegistry.Register(stub);
				IBinaryViewer viewer = BinaryViewerRegistry.Resolve(new BinaryViewerRequest("a.txt", StreamOf(0x01)));
				Assert.Same(stub, viewer);
			}
			finally
			{
				BinaryViewerRegistry.ResetToDefaults();
			}
		}

		[Fact]
		public void Register_HigherPriority_WinsOverLower()
		{
			try
			{
				StubViewer low = new StubViewer(BinaryViewerKind.FileCard, 50, canHandle: true);
				StubViewer high = new StubViewer(BinaryViewerKind.FileCard, 500, canHandle: true);
				BinaryViewerRegistry.Register(low);
				BinaryViewerRegistry.Register(high);
				IBinaryViewer viewer = BinaryViewerRegistry.Resolve(new BinaryViewerRequest("a.bin", StreamOf(0x01)));
				Assert.Same(high, viewer);
			}
			finally
			{
				BinaryViewerRegistry.ResetToDefaults();
			}
		}

		[Fact]
		public void Register_CanHandleFalse_SkipsToNextCandidate()
		{
			try
			{
				StubViewer picky = new StubViewer(BinaryViewerKind.StaticImage, 900, canHandle: false);
				BinaryViewerRegistry.Register(picky);
				IBinaryViewer viewer = BinaryViewerRegistry.Resolve(new BinaryViewerRequest("a.txt", StreamOf(0x01)));
				Assert.NotSame(picky, viewer);
			}
			finally
			{
				BinaryViewerRegistry.ResetToDefaults();
			}
		}

		private sealed class StubViewer : IBinaryViewer
		{
			private readonly bool _canHandle;

			public BinaryViewerKind Kind { get; }

			public int Priority { get; }

			public StubViewer(BinaryViewerKind kind, int priority, bool canHandle = true)
			{
				Kind = kind;
				Priority = priority;
				_canHandle = canHandle;
			}

			public bool CanHandle(BinaryViewerRequest request)
			{
				return _canHandle;
			}
		}
	}
}
