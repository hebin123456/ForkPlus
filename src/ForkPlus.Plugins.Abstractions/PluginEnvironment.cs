using System;
using Avalonia.Media;

namespace ForkPlus.Plugins
{
	/// <summary>
	/// 插件侧的宿主环境桥（v5.0.0 插件化架构）。
	///
	/// 插件工程不得引用主工程类型，但视图运行期需要主工程的一组服务：
	/// 本地化翻译、树级语言应用、图片差异高亮开关（及实时变更通知）、扩展名图标。
	/// 这些能力全部以委托/回调形式注入本静态类，由宿主的 PluginEnvironmentBridge 在启动期接线。
	/// 未初始化时全部走安全回退（原文返回 / no-op / null 图标），保证插件在独立测试环境可运行。
	/// </summary>
	public static class PluginEnvironment
	{
		// ---- 本地化 ----

		/// <summary>按 key 取当前语言译文（宿主注入；未注入时原样返回 key，即英文原文）。</summary>
		public static Func<string, string> TranslateHandler { get; set; }

		/// <summary>按 key + args 取当前语言格式化译文（宿主注入；未注入时 string.Format 兜底）。</summary>
		public static Func<string, object[], string> FormatHandler { get; set; }

		/// <summary>对控件子树应用当前语言（宿主注入，内部走 PreferencesLocalization.ApplyCurrent；未注入时 no-op）。</summary>
		public static Action<object> ApplyLocalizationHandler { get; set; }

		public static string Translate(string key)
		{
			return TranslateHandler != null ? TranslateHandler(key) : key;
		}

		public static string Format(string key, params object[] args)
		{
			return FormatHandler != null ? FormatHandler(key, args) : string.Format(key, args);
		}

		public static void ApplyLocalization(object root)
		{
			if (root != null)
			{
				ApplyLocalizationHandler?.Invoke(root);
			}
		}

		// ---- 界面语言（v5.0.3：宿主下发当前语言 code，缺省英文） ----

		/// <summary>v5.0.3：缺省界面语言 code（宿主未注入语言时使用）。</summary>
		public const string DefaultLanguage = "en";

		/// <summary>
		/// v5.0.3：当前界面语言 code（宿主注入，取宿主 UiLanguage；未注入/空值回退
		/// <see cref="DefaultLanguage"/> 即英文）。插件据此选取自带的本地化资源——
		/// 例如 <c>IPluginMetadata.GetDisplayName(language)</c> 的多语言名称/描述。
		/// </summary>
		public static Func<string> CurrentLanguageHandler { get; set; }

		/// <summary>v5.0.3：当前界面语言 code，缺省为英文（"en"）。</summary>
		public static string CurrentLanguage
		{
			get
			{
				string language = CurrentLanguageHandler?.Invoke();
				return string.IsNullOrWhiteSpace(language) ? DefaultLanguage : language;
			}
		}

		/// <summary>
		/// v5.0.3：界面语言热切换通知（宿主在语言切换时触发，参数为新语言 code）。
		/// 插件订阅后重刷自带的本地化资源；宿主每次切换必然触发，
		/// 未挂载的插件视图也不会漏掉语言变更。
		/// </summary>
		public static event Action<string> LanguageChanged;

		/// <summary>v5.0.3：由宿主触发语言热切换通知（空值按缺省英文处理）。</summary>
		public static void RaiseLanguageChanged(string language)
		{
			LanguageChanged?.Invoke(string.IsNullOrWhiteSpace(language) ? DefaultLanguage : language);
		}

		// ---- 图片差异高亮开关（偏好设置 → 实时生效） ----

		/// <summary>当前「高亮差异像素」偏好值（宿主注入；未注入时默认 false）。</summary>
		public static Func<bool> HighlightImageDiffHandler { get; set; }

		public static bool HighlightImageDiff => HighlightImageDiffHandler != null && HighlightImageDiffHandler();

		/// <summary>偏好变更通知（宿主接线 NotificationCenter.ImageDiffHighlightPixelsChanged）。</summary>
		public static event Action<bool> ImageDiffHighlightPixelsChanged;

		public static void RaiseImageDiffHighlightPixelsChanged(bool newValue)
		{
			ImageDiffHighlightPixelsChanged?.Invoke(newValue);
		}

		// ---- 扩展名图标 ----

		/// <summary>按扩展名取大号文件图标（宿主注入 IconTools；未注入时返回 null，卡片不显示图标）。</summary>
		public static Func<string, IImage> GetFileIconHandler { get; set; }

		public static IImage GetFileIcon(string extension)
		{
			return GetFileIconHandler?.Invoke(extension);
		}

		// ---- 键盘修饰键（宿主 WpfCompat.Keyboard 的全局按键跟踪；未注入时视为无修饰键） ----

		/// <summary>当前修饰键状态（宿主注入；未注入时 None）。</summary>
		public static Func<global::Avalonia.Input.KeyModifiers> KeyboardModifiersHandler { get; set; }

		public static global::Avalonia.Input.KeyModifiers KeyboardModifiers => KeyboardModifiersHandler?.Invoke() ?? global::Avalonia.Input.KeyModifiers.None;

		// ---- 剪贴板（宿主 WpfCompat.Clipboard；未注入时 no-op） ----

		public static Action<string> SetClipboardTextHandler { get; set; }

		/// <summary>剪贴板数据格式常量（与主工程 WpfDataFormats 同值）。</summary>
		public static class ClipboardFormats
		{
			public const string Text = "Text";
			public const string Serializable = "Serializable";
		}

		public static void SetClipboardText(string text)
		{
			SetClipboardTextHandler?.Invoke(text);
		}

		/// <summary>
		/// WPF Clipboard.SetData(format, value) 等价：文本直写剪贴板，
		/// 二进制（如「复制为原始字节」）经宿主进程内直通表 + 文本降级。
		/// </summary>
		public static Action<string, object> SetClipboardDataHandler { get; set; }

		public static void SetClipboardData(string format, object value)
		{
			if (value is string text)
			{
				SetClipboardText(text);
				return;
			}
			SetClipboardDataHandler?.Invoke(format, value);
		}

		// ---- Hex 视图设置（宿主 ForkPlusSettings；未注入时用同款默认值） ----

		public static Func<int> GetHexBytesPerRowHandler { get; set; }

		public static Action<int> SetHexBytesPerRowHandler { get; set; }

		public static Func<bool> GetHexShowAsciiHandler { get; set; }

		public static Action<bool> SetHexShowAsciiHandler { get; set; }

		public static Func<bool> GetHexShowOffsetHandler { get; set; }

		public static Action<bool> SetHexShowOffsetHandler { get; set; }

		/// <summary>设置写入后持久化（宿主 ForkPlusSettings.Save；未注入时 no-op）。</summary>
		public static Action SaveHexSettingsHandler { get; set; }

		public static int HexBytesPerRow
		{
			get
			{
				return GetHexBytesPerRowHandler?.Invoke() ?? 16;
			}
			set
			{
				SetHexBytesPerRowHandler?.Invoke(value);
			}
		}

		public static bool HexShowAscii
		{
			get
			{
				return GetHexShowAsciiHandler?.Invoke() ?? true;
			}
			set
			{
				SetHexShowAsciiHandler?.Invoke(value);
			}
		}

		public static bool HexShowOffset
		{
			get
			{
				return GetHexShowOffsetHandler?.Invoke() ?? true;
			}
			set
			{
				SetHexShowOffsetHandler?.Invoke(value);
			}
		}

		public static void SaveHexSettings()
		{
			SaveHexSettingsHandler?.Invoke();
		}
	}
}
