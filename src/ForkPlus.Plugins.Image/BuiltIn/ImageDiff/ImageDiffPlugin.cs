using System.Collections.Generic;
using ForkPlus.Plugins.Abstractions;

namespace ForkPlus.Plugins.BuiltIn.ImageDiff
{
	/// <summary>
	/// v5.0.0：内置图片对比插件。声明图片扩展名（与宿主 PathHelper.IsImagePath 同表），
	/// 命中后由 <see cref="BinaryDiffView"/> 渲染（并排/Swipe/洋葱皮/Hex，含 GIF/动态 WebP/APNG
	/// 播放与 LFS smudge）。第三方可注册更高优先级的同名扩展名插件覆盖之。
	/// v5.0.0：二进制通配兜底职责移交 Hex 插件（forkplus.hex，见 ForkPlus.Plugins.Hex）——
	/// 本插件只认图片扩展名，不再兜底非图片二进制。
	/// v5.0.1：实现 <see cref="IPluginMetadata"/>，向偏好设置 → 插件页提供名称/版本/描述。
	/// v5.0.3：元数据多语言——英文为缺省原文，按宿主下发语言返回对应译文
	///（<see cref="PluginEnvironment.CurrentLanguage"/>）。
	/// </summary>
	public sealed class ImageDiffPlugin : IDiffViewPlugin, IPluginMetadata
	{
		/// <summary>与宿主 PathHelper.IsImagePath 一致的扩展名表（小写含点）。</summary>
		public static readonly string[] ImageExtensions = new string[8]
		{
			".bmp", ".gif", ".png", ".jpg", ".jpeg", ".ico", ".tga", ".webp"
		};

		private static readonly Dictionary<string, string> DisplayNames = new Dictionary<string, string>
		{
			{ "en", "Image Diff" },
			{ "zh-Hans", "图片对比" },
			{ "zh-Hant", "圖片對比" },
			{ "ja-JP", "画像比較" },
			{ "ko-KR", "이미지 비교" },
			{ "fr-FR", "Comparaison d'images" },
			{ "de-DE", "Bildvergleich" },
			{ "es-ES", "Comparación de imágenes" }
		};

		private static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>
		{
			{ "en", "Side-by-side, swipe and onion-skin image comparison with GIF, animated WebP and APNG playback plus LFS content resolution." },
			{ "zh-Hans", "并排 / 滑动 / 洋葱皮三种图片对比视图，支持 GIF、动态 WebP、APNG 播放与 LFS 内容解析。" },
			{ "zh-Hant", "並排 / 滑動 / 洋蔥皮三種圖片對比視圖，支援 GIF、動態 WebP、APNG 播放與 LFS 內容解析。" },
			{ "ja-JP", "並べて表示 / スワイプ / オニオンスキンの 3 種類の画像比較ビュー。GIF・アニメーション WebP・APNG の再生と LFS コンテンツ解決に対応。" },
			{ "ko-KR", "나란히 보기 / 스와이프 / 어니언 스킨 세 가지 이미지 비교 뷰를 제공하며 GIF, 애니메이션 WebP, APNG 재생과 LFS 콘텐츠 해석을 지원합니다." },
			{ "fr-FR", "Comparaison d'images côte à côte, par balayage et en pelure d'oignon, avec lecture GIF, WebP animé et APNG et résolution du contenu LFS." },
			{ "de-DE", "Bildvergleich als Nebeneinander-, Wisch- und Zwiebelhaut-Ansicht, mit GIF-, animiertes-WebP- und APNG-Wiedergabe sowie LFS-Auflösung." },
			{ "es-ES", "Comparación de imágenes en paralelo, deslizamiento y piel de cebolla, con reproducción de GIF, WebP animado y APNG y resolución de contenido LFS." }
		};

		public string Id => "forkplus.image";

		public string DisplayNameKey => "Image";

		/// <summary>v5.0.3：插件元数据——英文为缺省原文，多语言由 GetDisplayName/GetDescription 提供。</summary>
		public string Version => "1.0.0";

		public string DisplayName => "Image Diff";

		public string Description => "Side-by-side, swipe and onion-skin image comparison with GIF, animated WebP and APNG playback plus LFS content resolution.";

		public string GetDisplayName(string language)
		{
			return PluginLocalization.Resolve(language, DisplayNames, DisplayName);
		}

		public string GetDescription(string language)
		{
			return PluginLocalization.Resolve(language, Descriptions, Description);
		}

		/// <summary>高于通配兜底（forkplus.hex，0），低于用户自定义绑定。</summary>
		public int Priority => 100;

		public IReadOnlyList<string> FileExtensions => ImageExtensions;

		/// <summary>扩展名已由宿主路由命中；图片视图对不可解码内容自带文件卡片兜底。</summary>
		public bool CanHandle(DiffViewRequest request)
		{
			return true;
		}

		public IDiffView CreateView()
		{
			return new BinaryDiffView();
		}
	}
}
