using System;
using ForkPlus.Plugins;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Plugins
{
	/// <summary>
	/// v5.0.0：宿主 → 插件的环境接线（启动期一次）。
	/// 把主工程的本地化、设置、剪贴板、键盘、图标等能力以委托形式注入
	/// <see cref="global::ForkPlus.Plugins.PluginEnvironment"/>，插件工程因此不引用主工程类型。
	/// </summary>
	public static class PluginEnvironmentBridge
	{
		private static bool _initialized;

		public static void Initialize()
		{
			if (_initialized)
			{
				return;
			}
			_initialized = true;
			// ---- 本地化 ----
			PluginEnvironment.TranslateHandler = delegate (string key)
			{
				return PreferencesLocalization.Current(key);
			};
			PluginEnvironment.FormatHandler = delegate (string key, object[] args)
			{
				return PreferencesLocalization.FormatCurrent(key, args);
			};
			PluginEnvironment.ApplyLocalizationHandler = delegate (object root)
			{
				if (root is global::Avalonia.AvaloniaObject avaloniaObject)
				{
					PreferencesLocalization.ApplyCurrent(avaloniaObject);
				}
			};
			// v5.0.3：向插件下发当前界面语言 code（缺省英文）——插件据此选取自带的多语言资源；
			// 语言热切换由 MainWindow.ApplyLocalizationCore 触发 PluginEnvironment.RaiseLanguageChanged。
			PluginEnvironment.CurrentLanguageHandler = () => ForkPlusSettings.Default.UiLanguage;
			// ---- 图片差异高亮开关（偏好 + 实时变更通知） ----
			PluginEnvironment.HighlightImageDiffHandler = () => ForkPlusSettings.Default.ImageDiffHighlightPixels;
			global::ForkPlus.NotificationCenter.Current.ImageDiffHighlightPixelsChanged += delegate (object sender, global::ForkPlus.UI.EventArgs<bool> e)
			{
				PluginEnvironment.RaiseImageDiffHighlightPixelsChanged(e.Value);
			};
			// ---- 扩展名图标 ----
			PluginEnvironment.GetFileIconHandler = delegate (string extension)
			{
				return UserControls.IconTools.GetImageSourceForExtension(extension, UserControls.ShellIconSize.LargeIcon);
			};
			// ---- 键盘修饰键 / 剪贴板（WpfCompat 全局跟踪） ----
			PluginEnvironment.KeyboardModifiersHandler = () => (global::Avalonia.Input.KeyModifiers)(int)WpfCompat.Keyboard.Modifiers;
			PluginEnvironment.SetClipboardTextHandler = delegate (string text)
			{
				WpfCompat.Clipboard.SetText(text);
			};
			PluginEnvironment.SetClipboardDataHandler = delegate (string format, object value)
			{
				WpfCompat.Clipboard.SetData(format, value);
			};
			// ---- Hex 视图设置 ----
			PluginEnvironment.GetHexBytesPerRowHandler = () => ForkPlusSettings.Default.HexViewBytesPerRow;
			PluginEnvironment.SetHexBytesPerRowHandler = delegate (int value)
			{
				ForkPlusSettings.Default.HexViewBytesPerRow = value;
			};
			PluginEnvironment.GetHexShowAsciiHandler = () => ForkPlusSettings.Default.HexViewShowAscii;
			PluginEnvironment.SetHexShowAsciiHandler = delegate (bool value)
			{
				ForkPlusSettings.Default.HexViewShowAscii = value;
			};
			PluginEnvironment.GetHexShowOffsetHandler = () => ForkPlusSettings.Default.HexViewShowOffset;
			PluginEnvironment.SetHexShowOffsetHandler = delegate (bool value)
			{
				ForkPlusSettings.Default.HexViewShowOffset = value;
			};
			PluginEnvironment.SaveHexSettingsHandler = delegate
			{
				ForkPlusSettings.Default.Save();
			};
		}
	}
}
