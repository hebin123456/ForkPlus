using System.Collections.Generic;
using System.IO;

namespace ForkPlus.Plugins.BuiltIn.ImageDiff
{
	/// <summary>
	/// v5.0.0：二进制对比内容的展示方式。由 <see cref="BinaryViewerRegistry"/> 按文件判定，
	/// BinaryContentPanel 据此选择渲染路径。新增文件类型时优先复用已有 Kind
	/// （如字体 / SVG 可先并入 StaticImage）；确实需要全新渲染方式时才扩枚举 + 注册一处分支。
	/// </summary>
	public enum BinaryViewerKind
	{
		/// <summary>通用文件卡片（图标 + 扩展名 + 大小）；无字节或无人认领时的兜底。</summary>
		FileCard,

		/// <summary>静态图片：缩放/平移，可选像素差异高亮。</summary>
		StaticImage,

		/// <summary>动图（GIF / 动态 WebP / APNG）：逐帧播放 + 播放控制条。</summary>
		AnimatedImage
	}

	/// <summary>v5.0.0：一次查看器判定的输入。</summary>
	public sealed class BinaryViewerRequest
	{
		/// <summary>仓库内路径（供按扩展名判定的查看器使用）；LFS 场景可能为 null。</summary>
		[Null]
		public string Path { get; }

		/// <summary>原始字节；LFS 指针尚未 smudge、或大文件未预载时为 null。</summary>
		[Null]
		public MemoryStream Data { get; }

		public bool IsLfs { get; }

		public long FileSize { get; }

		public BinaryViewerRequest([Null] string path, [Null] MemoryStream data, bool isLfs, long fileSize)
		{
			Path = path;
			Data = data;
			IsLfs = isLfs;
			FileSize = fileSize;
		}

		/// <summary>只关心路径 + 字节的调用方用（大小取字节长度）。</summary>
		public BinaryViewerRequest([Null] string path, [Null] MemoryStream data)
			: this(path, data, isLfs: false, data?.Length ?? 0L)
		{
		}
	}

	/// <summary>
	/// v5.0.0：可插拔的二进制内容查看器。新增格式（字体 / SVG / 压缩包 / 可执行文件…）时
	/// 实现本接口并 <see cref="BinaryViewerRegistry.Register"/> 即可，无需改插件视图的类型判断分支。
	/// </summary>
	public interface IBinaryViewer
	{
		BinaryViewerKind Kind { get; }

		/// <summary>判定优先级：数值越大越先被询问；兜底查看器用最小值。</summary>
		int Priority { get; }

		bool CanHandle([Null] BinaryViewerRequest request);
	}

	/// <summary>
	/// v5.0.0：查看器注册表——按优先级从高到低挑选第一个能处理该文件的查看器。
	/// 内置三个（动图 / 静态图 / 文件卡片兜底），动图必须先于静态图判定（GIF 本身也是可解码位图）。
	/// </summary>
	public static class BinaryViewerRegistry
	{
		private static readonly object SyncRoot = new object();

		private static readonly List<IBinaryViewer> Registry = new List<IBinaryViewer>();

		static BinaryViewerRegistry()
		{
			ResetToDefaults();
		}

		/// <summary>注册查看器，并按优先级降序保持顺序（同级则后注册的排在后面）。</summary>
		public static void Register(IBinaryViewer viewer)
		{
			lock (SyncRoot)
			{
				// 同一 Kind 只保留一个：重复注册即替换（便于覆盖内置实现）。
				Registry.RemoveAll((IBinaryViewer existing) => existing.Kind == viewer.Kind);
				int index = 0;
				while (index < Registry.Count && Registry[index].Priority >= viewer.Priority)
				{
					index++;
				}
				Registry.Insert(index, viewer);
			}
		}

		/// <summary>清空并恢复内置查看器。</summary>
		public static void ResetToDefaults()
		{
			lock (SyncRoot)
			{
				Registry.Clear();
				Register(new AnimatedImageViewer());
				Register(new StaticImageViewer());
				Register(new FileCardViewer());
			}
		}

		/// <summary>挑出处理该文件的查看器。内置兜底恒可用，故不会返回 null。</summary>
		public static IBinaryViewer Resolve([Null] BinaryViewerRequest request)
		{
			IBinaryViewer[] snapshot;
			lock (SyncRoot)
			{
				snapshot = Registry.ToArray();
			}
			foreach (IBinaryViewer viewer in snapshot)
			{
				if (viewer.CanHandle(request))
				{
					return viewer;
				}
			}
			// 兜底 FileCardViewer 的 CanHandle 恒为 true，正常到不了这里；仅为满足返回契约。
			return new FileCardViewer();
		}
	}
}
