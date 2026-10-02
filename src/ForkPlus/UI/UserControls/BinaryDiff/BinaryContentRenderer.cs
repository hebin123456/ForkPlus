using System;
using System.Collections.Generic;
using System.IO;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using ForkPlus.Plugins;

namespace ForkPlus.UI.UserControls.BinaryDiff
{
	/// <summary>
	/// v4.5.0：把"一侧字节"解码成可显示位图。内置格式走既有解码（<see cref="AnimatedImage"/> /
	/// <see cref="BinaryDiffUserControl.CreateBitmapSource"/>）；被插件认领的格式走
	/// <see cref="PluginManager"/> → 插件进程，返回的 PNG 帧再解码为 Avalonia 位图。
	///
	/// 抽出这个公共入口是为了让"并排显示"（BinaryContentUserControl.RefreshImage）与
	/// "滑动/洋葱皮/像素高亮"（ImageData.Create）两条路径对插件格式都能拿到位图——
	/// 否则插件只在并排视图里可见，Swipe/Onion 会因为没有 ImageData 而消失。
	/// </summary>
	internal static class BinaryContentRenderer
	{
		/// <summary>只要一张静态位图（首帧）。插件失败返回 null，由调用方按降级处理。</summary>
		[Null]
		public static Bitmap CreateStatic([Null] string path, [Null] MemoryStream data, out string statusLabel)
		{
			statusLabel = null;
			if (data == null)
			{
				return null;
			}
			if (IsPluginPath(path, data))
			{
				return RenderPlugin(path, data, onlyFirstFrame: true, out _, out statusLabel);
			}
			return BinaryDiffUserControl.CreateBitmapSource(data);
		}

		/// <summary>
		/// 解码一侧内容用于并排显示：返回首帧位图，动图另给出 <paramref name="animated"/>（调用方
		/// 据此启用播放控制条）。插件多帧内容同样走 <see cref="AnimatedImage"/> 复用现成播放机制。
		/// </summary>
		[Null]
		public static Bitmap CreateAnimatedOrStatic([Null] string path, [Null] MemoryStream data, out AnimatedImage animated, out string statusLabel)
		{
			animated = null;
			statusLabel = null;
			if (data == null)
			{
				return null;
			}
			IBinaryViewer viewer = BinaryViewerRegistry.Resolve(new BinaryViewerRequest(path, data));
			if (viewer.Kind == BinaryViewerKind.Plugin)
			{
				return RenderPlugin(path, data, onlyFirstFrame: false, out animated, out statusLabel);
			}
			if (viewer.Kind == BinaryViewerKind.AnimatedImage)
			{
				AnimatedImage decoded = AnimatedImage.TryDecode(data);
				if (decoded != null)
				{
					animated = decoded;
					return decoded.Frames[0];
				}
			}
			return BinaryDiffUserControl.CreateBitmapSource(data);
		}

		private static bool IsPluginPath([Null] string path, MemoryStream data)
		{
			return BinaryViewerRegistry.Resolve(new BinaryViewerRequest(path, data)).Kind == BinaryViewerKind.Plugin;
		}

		[Null]
		private static Bitmap RenderPlugin([Null] string path, MemoryStream data, bool onlyFirstFrame, out AnimatedImage animated, out string statusLabel)
		{
			animated = null;
			statusLabel = null;
			PluginViewerHandle handle = PluginManager.Instance.FindViewer(path);
			if (handle == null)
			{
				return null;
			}
			data.Position = 0L;
			RenderParams parameters = new RenderParams
			{
				ViewerId = handle.Viewer.Id,
				Path = path,
				IsLfs = false,
				Data = data.ToArray(),
				Width = 0,
				Height = 0,
				Theme = CurrentTheme()
			};
			RenderResult result = PluginManager.Instance.Render(handle, parameters, out string error);
			if (result == null)
			{
				Log.Warn("Plugin '" + handle.QualifiedId + "' failed to render '" + path + "': " + error);
				return null;
			}
			statusLabel = result.StatusLabel;
			List<Bitmap> frames = DecodeFrames(result.Frames);
			if (frames.Count == 0)
			{
				return null;
			}
			if (onlyFirstFrame || frames.Count == 1)
			{
				Bitmap first = frames[0];
				for (int i = 1; i < frames.Count; i++)
				{
					frames[i].Dispose();
				}
				return first;
			}
			int delay = ((result.FrameDelayMs > 0) ? result.FrameDelayMs : 100);
			int[] delays = new int[frames.Count];
			for (int j = 0; j < delays.Length; j++)
			{
				delays[j] = delay;
			}
			animated = new AnimatedImage(frames.ToArray(), delays, 0);
			return animated.Frames[0];
		}

		private static List<Bitmap> DecodeFrames([Null] List<byte[]> frames)
		{
			List<Bitmap> decoded = new List<Bitmap>();
			if (frames == null)
			{
				return decoded;
			}
			foreach (byte[] png in frames)
			{
				if (png == null || png.Length == 0)
				{
					continue;
				}
				try
				{
					decoded.Add(new Bitmap(new MemoryStream(png)));
				}
				catch (Exception ex)
				{
					Log.Warn("Plugin returned a frame that is not a decodable image", ex);
				}
			}
			return decoded;
		}

		/// <summary>插件据此刻画明暗配色；headless/无应用上下文时按 light 处理。</summary>
		private static string CurrentTheme()
		{
			try
			{
				return ((Application.Current?.ActualThemeVariant == ThemeVariant.Dark) ? "dark" : "light");
			}
			catch (Exception)
			{
				return "light";
			}
		}
	}
}