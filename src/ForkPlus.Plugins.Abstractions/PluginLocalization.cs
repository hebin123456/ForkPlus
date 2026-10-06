using System.Collections.Generic;

namespace ForkPlus.Plugins.Abstractions
{
	/// <summary>
	/// v5.0.3：插件侧的多语言查表助手。插件把自带译文按「语言 code → 文案」组织成字典，
	/// 交给本类按宿主下发的当前语言取译文；查不到时逐级回退，最终回退英文原文。
	///
	/// 回退顺序：当前语言 → 语言主标签（如 zh-Hans-CN → zh-Hans）→ 英文（en）→ 传入的英文原文。
	/// 语言 code 与宿主界面语言一致（en / zh-Hans / zh-Hant / ja-JP / ko-KR / fr-FR / de-DE / es-ES）。
	/// </summary>
	public static class PluginLocalization
	{
		/// <summary>
		/// 取当前语言译文。translations 为空或未命中时回退 <paramref name="englishText"/>。
		/// </summary>
		public static string Resolve(string language, IReadOnlyDictionary<string, string> translations, string englishText)
		{
			if (translations != null && translations.Count > 0)
			{
				string key = string.IsNullOrWhiteSpace(language) ? global::ForkPlus.Plugins.PluginEnvironment.DefaultLanguage : language;
				if (TryGet(translations, key, out string value))
				{
					return value;
				}
				int separator = key.LastIndexOf('-');
				if (separator > 0 && TryGet(translations, key.Substring(0, separator), out value))
				{
					return value;
				}
				if (TryGet(translations, global::ForkPlus.Plugins.PluginEnvironment.DefaultLanguage, out value))
				{
					return value;
				}
			}
			return englishText;
		}

		private static bool TryGet(IReadOnlyDictionary<string, string> translations, string key, out string value)
		{
			if (translations.TryGetValue(key, out value) && !string.IsNullOrEmpty(value))
			{
				return true;
			}
			value = null;
			return false;
		}
	}
}
