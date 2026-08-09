using System;

namespace ForkPlus
{
	/// <summary>
	/// v3.9.1：仓库项目类型。用于"Open in"菜单智能推荐 JetBrains IDE。
	/// 一个仓库可同时属于多种类型（如同时是 Node + 有 .iml），用 Flags 标记。
	/// </summary>
	[Flags]
	public enum ProjectType
	{
		Unknown = 0,
		Node = 1,
		Maven = 2,
		Gradle = 4,
		Android = 8,
		Python = 16,
		Go = 32,
		DotNet = 64,
		Php = 128
	}
}
