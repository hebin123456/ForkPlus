using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// <see cref="CustomColorsDialog"/> 的 ViewModel（阶段 3 抽取）。
	/// 承接原 View 中的纯逻辑：颜色 key 白名单、LoadItems 数据构造、TranslateColorKey、
	/// HSV/RGB 纯数学转换、导出 JSON 构造、导入校验流程。
	/// 不依赖任何 WPF 类型（无 <c>System.Windows.*</c> using）。
	/// </summary>
	/// <remarks>
	/// 拆分原则——VM 承载纯逻辑/纯数据，View 保留 MessageBox 弹框与 UI 事件处理。
	/// 12 处 MessageBox 中的导入/导出校验逻辑迁入 VM，VM 返回结果对象（含成功/失败 +
	/// 已本地化消息 + 错误列表），View 据此弹 MessageBox。
	/// <para>
	/// 设计为 <c>static</c> 类：被抽取的逻辑均无状态。<c>_items</c>/<c>_workingCopy</c> 等运行态
	/// 与 <see cref="CustomColorItem"/>（含 <c>SolidColorBrush</c> 等 WPF 类型）留 View，VM 不持有。
	/// </para>
	/// <para>
	/// 新验证的模式点（其它 Dialog VM 未涉及）：
	/// <list type="bullet">
	/// <item>导入校验多分支结果对象（<see cref="ImportValidationResult"/> + <see cref="ImportStatus"/>），
	/// View 单点消费结果弹 MessageBox，避免 9 处 MessageBox 散落 VM。</item>
	/// <item><see cref="CustomColorItem"/> 含 WPF 类型（<c>PreviewBrush</c>），故不提升为 VM 顶级类，
	/// VM 仅返回纯数据 <see cref="ColorItemData"/>，View 据此构造 <see cref="CustomColorItem"/>。</item>
	/// <item><see cref="IsValidHexColor"/> 原依赖 <c>ColorConverter.ConvertFromString</c>（WPF），
	/// 这里纯正则实现替代，仅接受 #RRGGBB / #AARRGGBB / RRGGBB / AARRGGBB（即原方法文档声明的格式）。</item>
	/// <item><see cref="HsvToRgb"/> 返回 <c>(byte, byte, byte)</c> 而非 <c>Color</c>，
	/// View 薄包装构造 <c>System.Windows.Media.Color</c>。</item>
	/// </list>
	/// </para>
	/// </remarks>
	internal static class CustomColorsDialogViewModel
	{
		/// <summary>JSON 配置文件的 schema 标识。导入时校验，未来 schema 升级时向后兼容判断用。</summary>
		public const string CustomColorsSchema = "ForkPlus.CustomColors/v1";

		/// <summary>可自定义的颜色 key 列表（Colors.*.xaml 中的 Color resource key）。
		/// 只暴露核心颜色，不暴露全部 260+ key。</summary>
		public static readonly string[] EditableColorKeys = new string[]
		{
			"BackgroundColor",
			"SecondaryBackgroundColor",
			"PanelBackgroundColor",
			"BorderColor",
			"TileBorderColor",
			"LabelColor",
			"ForegroundColor",
			"SecondaryLabelColor",
			"AccentColor",
			"AccentSecondaryColor",
			"ReferenceColor",
			"IconColor",
			"Diff.AddedColor",
			"Diff.RemovedColor",
			"Diff.AddColor",
			"Diff.RemoveColor",
			"Diff.ExactAddColor",
			"Diff.ExactRemoveColor",
			"LineNumber.ForegroundColor",
			"LineNumber.SeparatorColor",
			"ChunkSelection.BorderColor",
			"ChunkSelection.BackgroundColor",
			"Syntax.CommentColor",
			"Syntax.StringColor",
			"Syntax.KeywordColor",
			"Syntax.NumberColor",
			"CodeEditor.BackgroundColor",
			"CodeEditor.ForegroundColor",
			"Window.BackgroundColor",
			"Window.TitleBar.BackgroundColor",
		};

		/// <summary>白名单哈希集合，导入校验时 O(1) 查询。</summary>
		private static readonly HashSet<string> _editableColorKeySet = new HashSet<string>(EditableColorKeys);

		/// <summary>导入校验单项错误最多展示条数（与原 View 一致）。</summary>
		private const int _maxImportErrorsShown = 10;

		/// <summary>hex 颜色合法格式：可选 # 前缀 + 6 或 8 位十六进制（不区分大小写）。</summary>
		private static readonly Regex _hexColorRegex = new Regex(@"^#?([0-9A-Fa-f]{6}|[0-9A-Fa-f]{8})$", RegexOptions.Compiled);

		/// <summary>颜色 key → 国际化显示名。用 "Color." + key 作为 i18n key，
		/// 找不到翻译时返回 key 原文。</summary>
		public static string TranslateColorKey(string key, string lang)
		{
			string i18nKey = "Color." + key;
			string translated = PreferencesLocalization.Translate(i18nKey, lang);
			// Translate 找不到时返回原文（即 "Color." + key），此时 fallback 到 key
			if (translated != null && translated == i18nKey)
				return key;
			return translated ?? key;
		}

		/// <summary>构造颜色项数据列表 + 工作副本字典。
		/// 已自定义项（settings.json 中存在）显示自定义值并写入 workingCopy；
		/// 未自定义项取预设原色（通过 presetHexLookup 回调，View 提供以隔离 Application.Resources 的 WPF 依赖）。
		/// 不做 UI 绑定（ColorListControl.ItemsSource 由 View 设置）。</summary>
		/// <param name="presetHexLookup">预设原色查找：View 提供，从 Application.Resources 取 Color key 的 hex。</param>
		/// <returns>items: 颜色项纯数据列表；workingCopy: 已自定义项的 key→hex 字典（落盘副本）。</returns>
		public static (List<ColorItemData> items, Dictionary<string, string> workingCopy) LoadItems(
			Func<string, string> presetHexLookup)
		{
			Dictionary<string, string> saved = ForkPlusSettings.Default.CustomColors;
			string lang = ForkPlusSettings.Default.UiLanguage;
			string resetLabel = PreferencesLocalization.Translate("Reset", lang);

			var workingCopy = new Dictionary<string, string>();
			var items = new List<ColorItemData>();
			foreach (string key in EditableColorKeys)
			{
				string hex;
				bool isCustomized;
				if (saved != null && saved.TryGetValue(key, out string savedHex) && !string.IsNullOrEmpty(savedHex))
				{
					hex = savedHex;
					isCustomized = true;
					workingCopy[key] = hex;
				}
				else
				{
					hex = presetHexLookup(key);
					isCustomized = false;
				}
				items.Add(new ColorItemData(key, TranslateColorKey(key, lang), hex, isCustomized, resetLabel));
			}
			return (items, workingCopy);
		}

		/// <summary>RGB → HSV。纯数学，无 WPF 依赖。</summary>
		public static void RgbToHsv(byte r, byte g, byte b, out double h, out double s, out double v)
		{
			double rd = r / 255.0, gd = g / 255.0, bd = b / 255.0;
			double max = Math.Max(rd, Math.Max(gd, bd));
			double min = Math.Min(rd, Math.Min(gd, bd));
			double delta = max - min;
			v = max;
			s = max == 0 ? 0 : delta / max;
			if (delta == 0)
				h = 0;
			else if (max == rd)
				h = 60 * (((gd - bd) / delta) % 6);
			else if (max == gd)
				h = 60 * (((bd - rd) / delta) + 2);
			else
				h = 60 * (((rd - gd) / delta) + 4);
			if (h < 0) h += 360;
		}

		/// <summary>HSV → RGB。纯数学，无 WPF 依赖。View 据此薄包装构造 System.Windows.Media.Color。</summary>
		public static void HsvToRgb(double h, double s, double v, out byte r, out byte g, out byte b)
		{
			double c = v * s;
			double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
			double m = v - c;
			double rd, gd, bd;
			if (h < 60) { rd = c; gd = x; bd = 0; }
			else if (h < 120) { rd = x; gd = c; bd = 0; }
			else if (h < 180) { rd = 0; gd = c; bd = x; }
			else if (h < 240) { rd = 0; gd = x; bd = c; }
			else if (h < 300) { rd = x; gd = 0; bd = c; }
			else { rd = c; gd = 0; bd = x; }
			r = (byte)Math.Round((rd + m) * 255);
			g = (byte)Math.Round((gd + m) * 255);
			b = (byte)Math.Round((bd + m) * 255);
		}

		/// <summary>校验 hex 颜色字符串是否合法。接受 #RRGGBB / #AARRGGBB / RRGGBB / AARRGGBB（不区分大小写）。
		/// 纯正则实现，替代原 View 中依赖 ColorConverter.ConvertFromString（WPF）的版本。</summary>
		public static bool IsValidHexColor(string hex)
		{
			if (string.IsNullOrWhiteSpace(hex)) return false;
			return _hexColorRegex.IsMatch(hex.Trim());
		}

		/// <summary>构建导出 JSON 字符串。仅含 workingCopy 中实际自定义项。
		/// 格式：{ schema, theme, exportedAt, customColors: {key:hex,...} }。
		/// themeName: 导出时的主题名（仅参考，导入时不强制匹配）。文件写入由 View 负责。</summary>
		public static string BuildExportJson(Dictionary<string, string> workingCopy, string themeName)
		{
			Dictionary<string, string> exportColors = new Dictionary<string, string>(workingCopy);
			JObject root = new JObject
			{
				["schema"] = CustomColorsSchema,
				["theme"] = themeName,
				["exportedAt"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
				["customColors"] = JObject.FromObject(exportColors),
			};
			return root.ToString(Formatting.Indented);
		}

		/// <summary>校验导入 JSON 内容。View 据返回结果弹 MessageBox。
		/// 校验项：1) 合法 JSON 且 root 为对象 2) schema 字段（若有）匹配 3) customColors 字段存在且为对象
		/// 4) 每个 key 在白名单内 5) 每个 value 为合法 hex。
		/// 任一校验失败时返回 <see cref="ImportValidationResult.Message"/>（已本地化），View 直接用于 MessageBox。</summary>
		/// <param name="jsonContent">JSON 文本（View 已读取文件内容）。</param>
		/// <param name="lang">UI 语言代码，用于本地化消息。</param>
		/// <returns>校验结果：成功时 <see cref="ImportValidationResult.Imported"/> 填充待合并字典；失败时 Message 已含完整错误描述。</returns>
		public static ImportValidationResult ValidateImport(string jsonContent, string lang)
		{
			// 校验 1: 必须是合法 JSON + root 为对象
			JObject root;
			try
			{
				JToken parsed = JToken.Parse(jsonContent);
				if (parsed.Type != JTokenType.Object)
				{
					return new ImportValidationResult(ImportStatus.InvalidJsonSyntax,
						PreferencesLocalization.Translate("Invalid format: JSON root must be an object.", lang),
						null);
				}
				root = (JObject)parsed;
			}
			catch (JsonReaderException ex)
			{
				return new ImportValidationResult(ImportStatus.InvalidJsonSyntax,
					PreferencesLocalization.Translate("Invalid JSON: ", lang) + ex.Message,
					null);
			}

			// 校验 2: schema 字段如果存在，必须匹配
			JToken schemaToken = root["schema"];
			if (schemaToken != null)
			{
				if (schemaToken.Type != JTokenType.String || (string)schemaToken != CustomColorsSchema)
				{
					return new ImportValidationResult(ImportStatus.UnsupportedSchema,
						string.Format(PreferencesLocalization.Translate("Unsupported schema. Expected '{0}'.", lang), CustomColorsSchema),
						null);
				}
			}

			// 校验 3: customColors 字段必须存在且是对象
			JToken colorsToken = root["customColors"];
			if (colorsToken == null)
			{
				return new ImportValidationResult(ImportStatus.MissingCustomColorsField,
					PreferencesLocalization.Translate("Invalid format: missing 'customColors' field.", lang),
					null);
			}
			if (colorsToken.Type != JTokenType.Object)
			{
				return new ImportValidationResult(ImportStatus.InvalidCustomColorsType,
					PreferencesLocalization.Translate("Invalid format: 'customColors' must be an object.", lang),
					null);
			}

			// 校验 4 & 5: 每个 key 在白名单内 + 每个 value 是合法 hex
			Dictionary<string, string> imported = new Dictionary<string, string>();
			int errorCount = 0;
			StringBuilder errorBuf = new StringBuilder();
			JObject colorsObj = (JObject)colorsToken;
			foreach (KeyValuePair<string, JToken> kv in colorsObj)
			{
				string key = kv.Key;
				JToken valToken = kv.Value;
				// value 必须是字符串
				if (valToken.Type != JTokenType.String)
				{
					errorCount++;
					if (errorCount <= _maxImportErrorsShown)
						errorBuf.AppendLine(string.Format(PreferencesLocalization.Translate("  - '{0}': value must be a string", lang), key));
					continue;
				}
				string hex = (string)valToken;
				// key 白名单
				if (!_editableColorKeySet.Contains(key))
				{
					errorCount++;
					if (errorCount <= _maxImportErrorsShown)
						errorBuf.AppendLine(string.Format(PreferencesLocalization.Translate("  - '{0}': unknown color key", lang), key));
					continue;
				}
				// value 合法 hex
				if (!IsValidHexColor(hex))
				{
					errorCount++;
					if (errorCount <= _maxImportErrorsShown)
						errorBuf.AppendLine(string.Format(PreferencesLocalization.Translate("  - '{0}': invalid hex color '{1}'", lang), key, hex));
					continue;
				}
				// 规范化：统一加 # 前缀
				if (!hex.StartsWith("#")) hex = "#" + hex;
				imported[key] = hex;
			}

			if (errorCount > 0)
			{
				string summary;
				if (errorCount > _maxImportErrorsShown)
					summary = string.Format(PreferencesLocalization.Translate("Import aborted: {0} errors found (showing first {1}):\n", lang),
						errorCount, _maxImportErrorsShown);
				else
					summary = string.Format(PreferencesLocalization.Translate("Import aborted: {0} errors found:\n", lang), errorCount);
				return new ImportValidationResult(ImportStatus.EntryErrors,
					summary + errorBuf.ToString(),
					null);
			}

			if (imported.Count == 0)
			{
				return new ImportValidationResult(ImportStatus.NoValidEntries,
					PreferencesLocalization.Translate("No valid color entries found in file.", lang),
					null);
			}

			// 校验通过
			return new ImportValidationResult(ImportStatus.Success,
				string.Format(PreferencesLocalization.Translate("Imported {0} colors successfully.", lang), imported.Count),
				imported);
		}
	}

	/// <summary>颜色项的纯数据（无 WPF 类型）。View 据此构造 <see cref="CustomColorItem"/>。
	/// 因 <see cref="CustomColorItem"/> 含 <c>Brush PreviewBrush</c> 等 WPF 类型，
	/// 故 <see cref="CustomColorItem"/> 留 View，VM 仅产出本纯数据结构。</summary>
	internal sealed class ColorItemData
	{
		public string Key { get; }
		public string DisplayName { get; }
		public string HexValue { get; }
		public bool IsCustomized { get; }
		public string ResetLabel { get; }

		public ColorItemData(string key, string displayName, string hexValue, bool isCustomized, string resetLabel)
		{
			Key = key;
			DisplayName = displayName;
			HexValue = hexValue;
			IsCustomized = isCustomized;
			ResetLabel = resetLabel;
		}
	}

	/// <summary>导入校验状态码。View 据此选择 MessageBox 图标（Error/Warning/Information）。</summary>
	internal enum ImportStatus
	{
		/// <summary>校验通过，<see cref="ImportValidationResult.Imported"/> 已填充待合并字典。</summary>
		Success,
		/// <summary>校验 1 失败：非合法 JSON 或 root 非对象。</summary>
		InvalidJsonSyntax,
		/// <summary>校验 2 失败：schema 字段不匹配。</summary>
		UnsupportedSchema,
		/// <summary>校验 3a 失败：缺 customColors 字段。</summary>
		MissingCustomColorsField,
		/// <summary>校验 3b 失败：customColors 非对象。</summary>
		InvalidCustomColorsType,
		/// <summary>校验 4 & 5 失败：key 不在白名单或 value 非 hex（汇总）。</summary>
		EntryErrors,
		/// <summary>校验通过但无有效项（customColors 为空对象）。</summary>
		NoValidEntries,
	}

	/// <summary>导入校验结果。<see cref="Message"/> 已本地化，View 直接用于 MessageBox 内容。
	/// <see cref="Imported"/> 仅在 <see cref="IsSuccess"/> 时填充（待覆盖式合并到 _workingCopy）。</summary>
	internal sealed class ImportValidationResult
	{
		public ImportStatus Status { get; }
		public bool IsSuccess => Status == ImportStatus.Success;
		public string Message { get; }
		public Dictionary<string, string> Imported { get; }

		public ImportValidationResult(ImportStatus status, string message, Dictionary<string, string> imported)
		{
			Status = status;
			Message = message;
			Imported = imported ?? new Dictionary<string, string>();
		}
	}
}
