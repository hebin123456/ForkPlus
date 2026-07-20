using System.IO;

namespace ForkPlus.Git
{
	/// <summary>
	/// v3.1.0：携带原始字节的二进制内容，供 Hex Viewer 渲染。
	/// 派生自 BinaryContent 以便现有 BinaryContent 分发逻辑能通过 is 判断走到 Hex 分支。
	/// 保留 Size 字段（基类），新增 Data 携带原始字节（仿 ImageContent）。
	/// </summary>
	public class HexContent : BinaryContent
	{
		/// <summary>原始字节流。调用方负责在控件卸载时 Dispose。</summary>
		public MemoryStream Data { get; private set; }

		public HexContent(string path, bool isTracked, MemoryStream data)
			: base(path, isTracked, data?.Length)
		{
			Data = data;
		}

		/// <summary>释放内部 MemoryStream（控件卸载时调用，避免大文件驻留内存）。</summary>
		public void DisposeData()
		{
			if (Data != null)
			{
				try { Data.Dispose(); } catch { }
				Data = null;
			}
		}
	}
}
