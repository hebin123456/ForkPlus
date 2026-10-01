namespace ForkPlus.UI.UserControls.BinaryDiff
{
	/// <summary>
	/// v4.4.0：动图查看器——只读容器头判断是否多帧（GIF / 动态 WebP / APNG）。
	/// 优先级必须高于 <see cref="StaticImageViewer"/>：动图同样是可解码的位图，
	/// 先给静态图判定就会把动图当首帧静态图处理。
	/// </summary>
	public class AnimatedImageViewer : IBinaryViewer
	{
		public BinaryViewerKind Kind => BinaryViewerKind.AnimatedImage;

		public int Priority => 200;

		public bool CanHandle([Null] BinaryViewerRequest request)
		{
			return request?.Data != null && AnimatedImage.IsAnimatedStream(request.Data);
		}
	}

	/// <summary>
	/// v4.4.0：静态图片查看器——有字节即可解码。上游（BinaryDiffContent / ImageContent）已按
	/// 扩展名与内容把文件分类为图片，此处不再重复判扩展名，与改造前
	/// “ImageContent 一律走图片渲染路径”的行为保持一致。
	/// </summary>
	public class StaticImageViewer : IBinaryViewer
	{
		public BinaryViewerKind Kind => BinaryViewerKind.StaticImage;

		public int Priority => 100;

		public bool CanHandle([Null] BinaryViewerRequest request)
		{
			return request?.Data != null;
		}
	}

	/// <summary>
	/// v4.4.0：文件卡片兜底查看器——无字节（LFS 指针未 smudge / 大文件未预载）或格式无人认领时使用。
	/// 优先级取最小值且恒返回 true，保证注册表链总有结果。
	/// </summary>
	public class FileCardViewer : IBinaryViewer
	{
		public BinaryViewerKind Kind => BinaryViewerKind.FileCard;

		public int Priority => int.MinValue;

		public bool CanHandle([Null] BinaryViewerRequest request)
		{
			return true;
		}
	}
}