using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Avalonia.Media;
using ForkPlus.Plugins.Abstractions;

namespace ForkPlus.UI.Plugins
{
	/// <summary>对比视图一侧的内容指纹（见 <see cref="DiffViewContentFingerprint"/>）。</summary>
	internal sealed record DiffSideFingerprint(
		string Path,
		bool IsTracked,
		long? Size,
		string LfsSha256,
		long? LfsSize,
		bool IsLfsImage,
		string DataHash);

	/// <summary>
	/// v5.1.2：一次对比内容的指纹，用于「同一插件 + 同一内容」重复下发时的去重。
	/// 典型场景：窗口重新激活触发 RepositoryUserControl 刷新——重拉变更列表后重新选中同一文件、
	/// 重新构造 <see cref="DiffViewContext"/>，此前会无条件调用 IDiffView.SetContent，
	/// 导致 Hex/图片/视频等重内容视图被全量重建（重新解码、丢失当前视图模式与「加载更多」进度）。
	///
	/// 判定原则保守：把视图可观察到的全部输入（路径 / 角色 / 声明大小 / LFS 指针 / 标题画刷颜色 /
	/// 字节内容）逐一比较，字节按 SHA-256 内容哈希——只有当能证明两份内容对视图渲染等价时才判相同；
	/// 任何一项不同（含无法哈希的不可回退流）都判不同，照常重新下发，避免漏刷新。
	/// </summary>
	internal sealed record DiffViewContentFingerprint(
		string PluginId,
		string FilePath,
		bool ShowTitle,
		DiffSideRole SrcRole,
		DiffSideRole DstRole,
		string SrcTitleBrushKey,
		string DstTitleBrushKey,
		DiffSideFingerprint Src,
		DiffSideFingerprint Dst,
		string HexSrcHash,
		string HexDstHash)
	{
		/// <summary>按插件 Id + 展开后的上下文构造指纹。</summary>
		internal static DiffViewContentFingerprint Create(string pluginId, DiffViewContext context)
		{
			// 同一份流可能同时挂在 side.Data 与 HexSrc/HexDst 上（BinaryDiffContent 路径），
			// 按引用去重，避免重复哈希。
			Dictionary<Stream, string> hashes = new Dictionary<Stream, string>(ReferenceEqualityComparer.Instance);
			return new DiffViewContentFingerprint(
				pluginId,
				context.FilePath,
				context.ShowTitle,
				context.SrcRole,
				context.DstRole,
				BrushKey(context.SrcTitleBrush),
				BrushKey(context.DstTitleBrush),
				CreateSide(context.Src, hashes),
				CreateSide(context.Dst, hashes),
				HashStream(context.HexSrc, hashes),
				HashStream(context.HexDst, hashes));
		}

		[Null]
		private static DiffSideFingerprint CreateSide([Null] DiffSideContent side, Dictionary<Stream, string> hashes)
		{
			if (side == null)
			{
				return null;
			}
			return new DiffSideFingerprint(
				side.Path,
				side.IsTracked,
				side.Size,
				side.Lfs?.Sha256,
				side.Lfs?.Size,
				side.IsLfsImage,
				HashStream(side.Data, hashes));
		}

		/// <summary>标题画刷标识：取颜色值（同色视为同渲染，不受画刷实例更换影响），无法取值时退回 ToString。</summary>
		[Null]
		private static string BrushKey([Null] IBrush brush)
		{
			if (brush == null)
			{
				return null;
			}
			return brush is ISolidColorBrush solid ? solid.Color.ToString() : brush.ToString();
		}

		[Null]
		private static string HashStream([Null] Stream stream, Dictionary<Stream, string> hashes)
		{
			if (stream == null)
			{
				return null;
			}
			if (hashes.TryGetValue(stream, out string cached))
			{
				return cached;
			}
			string hash;
			if (!stream.CanSeek)
			{
				// 不可回退的流无法在不破坏后续读取的前提下取哈希：用唯一值代表「无法证明相等」。
				hash = Guid.NewGuid().ToString("N");
			}
			else
			{
				long position = stream.Position;
				try
				{
					stream.Position = 0L;
					hash = Convert.ToHexString(SHA256.HashData(stream));
				}
				catch (Exception)
				{
					hash = Guid.NewGuid().ToString("N");
				}
				finally
				{
					stream.Position = position;
				}
			}
			hashes[stream] = hash;
			return hash;
		}
	}
}