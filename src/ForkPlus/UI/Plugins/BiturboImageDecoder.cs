using System.IO;
using ForkPlus.Biturbo;
using ForkPlus.Git;
using ForkPlus.Git.Commands;

namespace ForkPlus.UI.Plugins
{
	/// <summary>
	/// v5.0.0：.tga 图片的原生解码入口（自主工程 BinaryDiffUserControl.DecodeImageData 迁移，插件化）。
	/// 宿主（DiffViewHostAdapter / BinaryFileContentControl）与插件经
	/// <see cref="DiffViewHostAdapter.PrepareImageStream"/> 共用；biturbo 为宿主侧原生依赖，插件不直接引用。
	/// </summary>
	public static class BiturboImageDecoder
	{
		public static GitCommandResult<MemoryStream> DecodeImageData(byte[] data)
		{
			return BtRequest.Run(() => default(BtDecodeImageResult), delegate (ref BtDecodeImageResult x)
			{
				return Bt.bt_decode_image(data, data.Length, ref x);
			}, delegate (ref BtDecodeImageResult x)
			{
				return GitCommandResult<MemoryStream>.Success(new MemoryStream(x.data.GetByteArray(x.data_len)));
			}, delegate (ref BtDecodeImageResult x)
			{
				Bt.bt_release_decode_image(ref x);
			});
		}
	}
}
