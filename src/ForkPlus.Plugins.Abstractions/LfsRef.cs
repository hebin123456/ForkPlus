namespace ForkPlus.Plugins.Abstractions
{
	/// <summary>
	/// LFS 指针的插件侧投影（v5.0.0 插件化架构）。
	/// 主工程的 LfsPointer（含解析逻辑）留在宿主；插件只见 SHA-256 与声明大小，
	/// smudge/缓存取回一律经 IDiffViewHost 完成。
	/// </summary>
	public sealed class LfsRef
	{
		public string Sha256 { get; }

		public long Size { get; }

		public LfsRef(string sha256, long size)
		{
			Sha256 = sha256;
			Size = size;
		}
	}
}
