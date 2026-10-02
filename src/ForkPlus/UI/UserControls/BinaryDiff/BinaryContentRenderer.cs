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
				Bitmap pluginBitmap = RenderPlugin(path, data, onlyFirstFrame: true, out _, out statusLabel);
				if (pluginBitmap != null)
				{
					return pluginBitmap;
				}
				// v4.5.0：插件认领了却在渲染阶段失败（解码不了 / 进程超时 / 返回坏帧）时，
				// 不能把这一侧留白——落回内置静态解码，宿主兜底才算真的兜住。
				// （插件对此格式本就更强时会返回内容，走不到这里；走得到说明内置也解不出，
				// 结果与修复前一致。）
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
				Bitmap pluginBitmap = RenderPlugin(path, data, onlyFirstFrame: false, out animated, out statusLabel);
				if (pluginBitmap != null)
				{
					return pluginBitmap;
				}
				// v4.5.0：插件认领了但渲染失败（解不出 / 超时 / 坏帧）——落回内置解码兜底，
				// 而不是把这一侧留白。内置动图优先于静态图，故这里也先试动图。
				AnimatedImage pluginFallback = AnimatedImage.TryDecode(data);
				if (pluginFallback != null)
				{
					animated = pluginFallback;
					return pluginFallback.Frames[0];
				}
				return BinaryDiffUserControl.CreateBitmapSource(data);
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
			// 渲染用解析（显式扩展名优先，再退通配兜底）——与 PluginBinaryViewer 的判定同源，
			// 否则会出现"注册表判给了插件，这里却找不到视图"的空渲染。
			PluginViewerHandle handle = PluginManager.Instance.FindRenderViewer(path);
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