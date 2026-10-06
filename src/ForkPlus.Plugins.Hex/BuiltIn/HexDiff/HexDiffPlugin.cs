using System.Collections.Generic;
using ForkPlus.Plugins.Abstractions;

namespace ForkPlus.Plugins.BuiltIn.HexDiff
{
	/// <summary>
	/// v5.0.0：内置 Hex 对比插件（独立 DLL：ForkPlus.Plugins.Hex.dll，经 plugins/ 动态加载）。
	/// v5.0.0 起兼二进制文件通配兜底（原 forkplus.binary 职责并入，与 4.3.2「一个图片对比器 +
	/// 一个十六进制对比器」的心智模型一致）：任何未被专属插件认领的文件都落这里——默认
	/// side-by-side 文件卡片（图标 + 扩展名 + 大小 + LFS 徽章），宿主预载字节（≤50MB）时
	/// 可切 Hex 对比。图片扩展名仍由图片插件（forkplus.image，100）优先认领；第三方插件
	/// 以更高优先级或用户绑定（DiffViewPluginRegistry.BindExtension）覆盖本兜底。
	/// 渲染内核与图片插件的「Hex」模式页共用同一 ForkPlus.Plugins.Ui 组件
	///（HexDiffUserControl / BinaryContentPanel）。
	/// v5.0.1：实现 <see cref="IPluginMetadata"/>，向偏好设置 → 插件页提供名称/版本/描述。
	/// v5.0.3：元数据多语言——英文为缺省原文，按宿主下发语言返回对应译文
	///（<see cref="PluginEnvironment.CurrentLanguage"/>）。
	/// </summary>
	public sealed class HexDiffPlugin : IDiffViewPlugin, IPluginMetadata
	{
		private static readonly Dictionary<string, string> DisplayNames = new Dictionary<string, string>
		{
			{ "en", "Hex Diff" },
			{ "zh-Hans", "十六进制对比" },
			{ "zh-Hant", "十六進位對比" },
			{ "ja-JP", "16 進数比較" },
			{ "ko-KR", "16진수 비교" },
			{ "fr-FR", "Comparaison hexadécimale" },
			{ "de-DE", "Hexadezimalvergleich" },
			{ "es-ES", "Comparación hexadecimal" }
		};

		private static readonly Dictionary<string, string> Descriptions = new Dictionary<string, string>
		{
			{ "en", "Byte-by-byte hexadecimal comparison; as the wildcard fallback it shows a file card for any file type not claimed by another plugin." },
			{ "zh-Hans", "逐字节十六进制对比；作为通配兜底，未被其他插件认领的文件类型都显示文件卡片。" },
			{ "zh-Hant", "逐位元組十六進位對比；作為萬用字元兜底，未被其他外掛認領的檔案類型都顯示檔案卡片。" },
			{ "ja-JP", "バイト単位の 16 進数比較。ワイルドカードのフォールバックとして、他のプラグインが処理しないファイル種別にはファイルカードを表示します。" },
			{ "ko-KR", "바이트 단위 16진수 비교이며, 와일드카드 폴백으로 다른 플러그인이 처리하지 않는 파일 형식에는 파일 카드를 표시합니다." },
			{ "fr-FR", "Comparaison hexadécimale octet par octet ; en tant que solution de repli générique, affiche une fiche de fichier pour tout type non pris en charge par un autre plugin." },
			{ "de-DE", "Byteweiser Hexadezimalvergleich; als Wildcard-Fallback zeigt er eine Dateikarte für jeden Dateityp, der von keinem anderen Plugin übernommen wird." },
			{ "es-ES", "Comparación hexadecimal byte a byte; como respaldo comodín, muestra una tarjeta de archivo para cualquier tipo no reclamado por otro plugin." }
		};

		public string Id => "forkplus.hex";

		public string DisplayNameKey => "Hex";

		/// <summary>v5.0.3：插件元数据——英文为缺省原文，多语言由 GetDisplayName/GetDescription 提供。</summary>
		public string Version => "1.0.0";

		public string DisplayName => "Hex Diff";

		public string Description => "Byte-by-byte hexadecimal comparison; as the wildcard fallback it shows a file card for any file type not claimed by another plugin.";

		public string GetDisplayName(string language)
		{
			return PluginLocalization.Resolve(language, DisplayNames, DisplayName);
		}

		public string GetDescription(string language)
		{
			return PluginLocalization.Resolve(language, Descriptions, Description);
		}

		/// <summary>通配兜底：取最低优先级，任何精确扩展名插件（如图片）都先于它。</summary>
		public int Priority => 0;

		public IReadOnlyList<string> FileExtensions => new string[1] { "*" };

		/// <summary>扩展名已由路由/绑定命中；字节未预载（超大文件）时视图只显示文件卡片。</summary>
		public bool CanHandle(DiffViewRequest request)
		{
			return true;
		}

		public IDiffView CreateView()
		{
			return new HexDiffView();
		}
	}
}
