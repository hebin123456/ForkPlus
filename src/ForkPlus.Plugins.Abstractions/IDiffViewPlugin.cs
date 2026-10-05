namespace ForkPlus.Plugins.Abstractions
{
	/// <summary>
	/// 路由请求（v5.0.0 插件化架构）：宿主在解析对比视图时下发给候选插件，
	/// 供插件在扩展名之外做二次判定（如看魔数）。此阶段字节通常尚未加载。
	/// </summary>
	public sealed class DiffViewRequest
	{
		public string Path { get; }

		/// <summary>左侧声明大小；未知为 null。</summary>
		public long? SrcSize { get; }

		/// <summary>右侧声明大小；未知为 null。</summary>
		public long? DstSize { get; }

		public DiffViewRequest(string path, long? srcSize, long? dstSize)
		{
			Path = path;
			SrcSize = srcSize;
			DstSize = dstSize;
		}
	}

	/// <summary>
	/// 对比视图插件契约：一类文件的完整对比能力（可含多个视图模式）。
	/// 第三方实现本接口 + 声明扩展名即可接管对应文件的对比视图；
	/// 逻辑自包含于插件内，宿主只负责路由与能力供给（见 <see cref="IDiffViewHost"/>）。
	/// </summary>
	public interface IDiffViewPlugin
	{
		/// <summary>插件唯一 Id（如 "forkplus.image"、"forkplus.hex"）。</summary>
		string Id { get; }

		/// <summary>显示名的翻译 key（用户绑定 UI 使用）。</summary>
		string DisplayNameKey { get; }

		/// <summary>同轮裁决优先级，大者优先。</summary>
		int Priority { get; }

		/// <summary>
		/// 声明支持的扩展名（小写含点，如 ".png"）；"*" 表示通配兜底。
		/// 路由规则：用户绑定 &gt; 精确扩展名（按优先级）&gt; 通配（按优先级）。
		/// </summary>
		System.Collections.Generic.IReadOnlyList<string> FileExtensions { get; }

		/// <summary>扩展名命中后的二次判定（不满足则继续问下一个候选）。</summary>
		bool CanHandle(DiffViewRequest request);

		/// <summary>每次对比创建独立视图实例。</summary>
		IDiffView CreateView();
	}
}
