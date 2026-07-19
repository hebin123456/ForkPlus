using System.IO;

namespace ForkPlus.Git
{
	/// <summary>
	/// v3.1.0：携带两侧原始字节的二进制 diff 内容，供 HexDiffUserControl 渲染。
	/// 当 UnknownBinaryDiffContent 的 src/dst 大小均不超过 MaxHexDiffSize 时升级为 HexDiffContent，
	/// 加载两侧 blob 字节用于 side-by-side hex 比较。
	/// 任意一侧 MemoryStream 由调用方在控件卸载时通过 DisposeData 释放。
	/// </summary>
	public class HexDiffContent : DiffContent
	{
		/// <summary>源侧字节（删除侧）。可能为 null（如纯新增文件）。</summary>
		public MemoryStream SrcData { get; private set; }

		/// <summary>目标侧字节（新增侧）。可能为 null（如纯删除文件）。</summary>
		public MemoryStream DstData { get; private set; }

		public long? SrcSize => SrcData?.Length;
		public long? DstSize => DstData?.Length;

		public HexDiffContent(ChangedFile changedFile, MemoryStream srcData, MemoryStream dstData)
			: base(changedFile)
		{
			SrcData = srcData;
			DstData = dstData;
		}

		/// <summary>释放两侧 MemoryStream（控件卸载时调用）。</summary>
		public void DisposeData()
		{
			if (SrcData != null) { try { SrcData.Dispose(); } catch { } SrcData = null; }
			if (DstData != null) { try { DstData.Dispose(); } catch { } DstData = null; }
		}
	}
}
