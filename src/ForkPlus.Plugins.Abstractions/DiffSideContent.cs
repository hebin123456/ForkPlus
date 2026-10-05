using System;
using System.IO;

namespace ForkPlus.Plugins.Abstractions
{
	/// <summary>
	/// 对比视图一侧的内容模型（v5.0.0 插件化架构）。
	/// 宿主把主工程的 ImageContent/LfsContent/BinaryContent 等领域模型压平成该投影：
	/// 插件不感知 Git 领域类型，只认「路径 + 字节（懒加载）+ LFS 引用」。
	/// </summary>
	public sealed class DiffSideContent
	{
		/// <summary>文件路径（用于扩展名/图标/保存对话框建议名）。</summary>
		public string Path { get; }

		public bool IsTracked { get; }

		/// <summary>声明大小（字节）。未知为 null。</summary>
		public long? Size { get; }

		/// <summary>
		/// 字节懒加载器：返回该侧的原始字节；不可用（未加载/无缓存/超阈值）时为 null。
		/// 首次访问后经 <see cref="Data"/> 缓存，重复取用不再触发加载。
		/// </summary>
		public Func<MemoryStream> LoadData { get; }

		/// <summary>LFS 引用；非 LFS 侧为 null。LFS 侧字节未 smudge 时 LoadData 可为 null。</summary>
		public LfsRef Lfs { get; }

		/// <summary>该 LFS 指针指向的是否为图片（决定 LFS 徽章 + 「显示 LFS 图片」按钮）。</summary>
		public bool IsLfsImage { get; }

		private MemoryStream _loadedData;

		private bool _dataLoaded;

		/// <summary>已加载的字节（懒加载 + 缓存；不可用时为 null）。</summary>
		public MemoryStream Data
		{
			get
			{
				if (!_dataLoaded)
				{
					_dataLoaded = true;
					_loadedData = LoadData?.Invoke();
				}
				return _loadedData;
			}
		}

		public DiffSideContent(string path, bool isTracked, long? size, Func<MemoryStream> loadData, LfsRef lfs = null, bool isLfsImage = false)
		{
			Path = path;
			IsTracked = isTracked;
			Size = size;
			LoadData = loadData;
			Lfs = lfs;
			IsLfsImage = isLfsImage;
		}
	}
}
