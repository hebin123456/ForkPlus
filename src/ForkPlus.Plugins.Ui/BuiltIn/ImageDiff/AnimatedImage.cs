using System;
using System.IO;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace ForkPlus.Plugins.BuiltIn.ImageDiff
{
	/// <summary>
	/// v4.3.2：动图（GIF / 动态 WebP / APNG）解码结果——逐帧位图 + 每帧时长 + 循环次数。
	/// 用 SkiaSharp 的 <c>SKCodec</c> 统一解码三种容器（Avalonia 自身的 Bitmap 只解首帧）。
	/// 帧按顺序解码进同一块像素缓冲（GIF 增量帧依赖前一帧），再各自转成 Avalonia Bitmap。
	/// 帧数 / 总像素超阈值时整体放弃（返回 null），由调用方回退为静态首帧显示，避免内存爆掉。
	/// </summary>
	public class AnimatedImage : IDisposable
	{
		/// <summary>帧数上限：超过则不做动图播放，退回静态首帧。</summary>
		public const int MaxFrameCount = 120;

		/// <summary>总像素上限（宽 × 高 × 帧数）：超过则退回静态首帧，兼顾内存。</summary>
		public const long MaxTotalPixels = 16000000L;

		private const int MinFrameDelayMs = 20;

		private const int DefaultFrameDelayMs = 100;

		public Bitmap[] Frames { get; }

		/// <summary>每帧显示时长（毫秒，已做下限归一）。</summary>
		public int[] FrameDelays { get; }

		/// <summary>循环次数：0 表示无限循环（与 SKCodec.RepetitionCount 语义一致）。</summary>
		public int LoopCount { get; }

		public int FrameCount => Frames.Length;

		public bool IsAnimated => Frames.Length > 1;

		public AnimatedImage(Bitmap[] frames, int[] delays, int loopCount)
		{
			if (frames == null)
			{
				throw new ArgumentNullException("frames");
			}
			if (frames.Length == 0)
			{
				throw new ArgumentException("Animated image requires at least one frame.", "frames");
			}
			Frames = frames;
			FrameDelays = NormalizeDelays(delays, frames.Length);
			LoopCount = Math.Max(0, loopCount);
		}

		/// <summary>廉价判断流是否为真正的动图（只读容器头，不解整帧）。</summary>
		public static bool IsAnimatedStream(MemoryStream stream)
		{
			if (stream == null || stream.Length == 0L)
			{
				return false;
			}
			try
			{
				stream.Position = 0L;
				// SKCodec.Create(Stream) 内部用默认「拥有并释放包装流」的 SKManagedStream，
				// 探测结束会把调用方的 MemoryStream 一起关掉（后续读取 Length 抛
				// ObjectDisposedException）。显式传 disposeManagedStream:false，只包不接管。
				using SKManagedStream managedStream = new SKManagedStream(stream, disposeManagedStream: false);
				using SKCodec codec = SKCodec.Create(managedStream);
				return codec != null && codec.FrameCount > 1;
			}
			catch (Exception ex)
			{
				PluginLog.Warn("Failed to probe animated image", ex);
				return false;
			}
		}

		/// <summary>解码动图。非动图、解码失败或超过阈值时返回 null（调用方按静态图处理）。</summary>
		[Null]
		public static AnimatedImage TryDecode(MemoryStream stream)
		{
			if (stream == null || stream.Length == 0L)
			{
				return null;
			}
			try
			{
				stream.Position = 0L;
				// 同 IsAnimatedStream：不把调用方的流交给 SKManagedStream 释放。
				using SKManagedStream managedStream = new SKManagedStream(stream, disposeManagedStream: false);
				using SKCodec codec = SKCodec.Create(managedStream);
				if (codec == null)
				{
					return null;
				}
				int frameCount = codec.FrameCount;
				if (frameCount <= 1 || frameCount > MaxFrameCount)
				{
					return null;
				}
				int width = codec.Info.Width;
				int height = codec.Info.Height;
				if (width <= 0 || height <= 0)
				{
					return null;
				}
				if ((long)width * (long)height * (long)frameCount > MaxTotalPixels)
				{
					return null;
				}
				SKImageInfo info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
				using SKBitmap canvas = new SKBitmap(info);
				SKCodecFrameInfo[] frameInfos = codec.FrameInfo;
				Bitmap[] frames = new Bitmap[frameCount];
				int[] delays = new int[frameCount];
				for (int i = 0; i < frameCount; i++)
				{
					// GIF 增量帧需要前一帧内容做底：按顺序解码进同一缓冲，priorFrame 传上一帧序号。
					int priorFrame = ((i == 0) ? (-1) : (i - 1));
					SKCodecResult result = codec.GetPixels(info, canvas.GetPixels(), new SKCodecOptions(i, priorFrame));
					if (result != SKCodecResult.Success && result != SKCodecResult.IncompleteInput)
					{
						DisposeFrames(frames);
						return null;
					}
					frames[i] = ToAvaloniaBitmap(canvas);
					delays[i] = ResolveDelay(frameInfos, i);
				}
				int loopCount = codec.RepetitionCount;
				if (loopCount < 0)
				{
					loopCount = 0;
				}
				return new AnimatedImage(frames, delays, loopCount);
			}
			catch (Exception ex)
			{
				PluginLog.Error("Failed to decode animated image", ex);
				return null;
			}
		}

		public void Dispose()
		{
			DisposeFrames(Frames);
		}

		private static int[] NormalizeDelays([Null] int[] delays, int frameCount)
		{
			int[] result = new int[frameCount];
			for (int i = 0; i < frameCount; i++)
			{
				int value = ((delays != null && i < delays.Length) ? delays[i] : 0);
				result[i] = ((value < MinFrameDelayMs) ? DefaultFrameDelayMs : value);
			}
			return result;
		}

		private static int ResolveDelay([Null] SKCodecFrameInfo[] frameInfos, int index)
		{
			if (frameInfos == null || index < 0 || index >= frameInfos.Length)
			{
				return DefaultFrameDelayMs;
			}
			int duration = frameInfos[index].Duration;
			return ((duration < MinFrameDelayMs) ? DefaultFrameDelayMs : duration);
		}

		private static Bitmap ToAvaloniaBitmap(SKBitmap bitmap)
		{
			using SKImage image = SKImage.FromBitmap(bitmap);
			using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
			using MemoryStream stream = new MemoryStream(data.ToArray());
			return new Bitmap(stream);
		}

		private static void DisposeFrames([Null] Bitmap[] frames)
		{
			if (frames == null)
			{
				return;
			}
			foreach (Bitmap frame in frames)
			{
				frame?.Dispose();
			}
		}
	}
}