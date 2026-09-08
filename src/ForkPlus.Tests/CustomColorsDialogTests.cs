// 回归测试（2026-09-03，"自定义颜色窗口不加载当前颜色，显示全是 #FFFFFF"修复产物）：
// 根因：WPF 的 Application.Current.Resources[key] 索引器会穿透 MergedDictionaries 找到
// 主题色（Generic.{Skin}.axaml 合并在 App.Resources.MergedDictionaries）；Avalonia 的索引器
// 只查顶层字典（headless 探针实测：Resources["BackgroundColor"] 返回 null，而合并字典里
// 有值），GetCurrentColorHex 全部走 fallback "#FFFFFF" → 30 个颜色项全白。InitializeSwatches
// 的 BorderBrush 取值同根因（null → 预设色板 30 个色块无描边）。
// 修复：改用 ResourceCompat.TryFindResource（底层 Resources.TryGetResource，逆序穿透合并
// 字典，与 WPF 索引器同语义——末尾 merge 的自定义颜色覆盖字典优先命中）。
// 本测试守卫：(1) 打开对话框加载的是主题当前色而非全白；(2) 预设色板取到 BorderBrush；
// (3) TryFindResource 的"末尾合并字典优先"语义（自定义颜色覆盖能被 GetCurrentColorHex 取到）。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class CustomColorsDialogTests
	{
		private static readonly Regex HexPattern = new Regex("^#[0-9A-F]{6}$", RegexOptions.Compiled);

		[Fact]
		public void DialogLoadsCurrentThemeColors_NotAllWhite()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				// LoadItems 只在 saved.CustomColors 无该 key 时走 GetCurrentColorHex；
				// 清空自定义色保证全部走资源查找路径（用户报告的场景）。
				Dictionary<string, string> originalColors = ForkPlusSettings.Default.CustomColors;
				bool originalUseCustom = ForkPlusSettings.Default.UseCustomColors;
				try
				{
					ForkPlusSettings.Default.CustomColors = new Dictionary<string, string>();
					ForkPlusSettings.Default.UseCustomColors = false;

					var dialog = new CustomColorsDialog();
					dialog.Show();
					Dispatcher.UIThread.RunJobs();

					ItemsControl colorList = dialog.GetVisualDescendants().OfType<ItemsControl>()
						.FirstOrDefault(c => c.Name == "ColorListControl");
					Assert.NotNull(colorList);
					var items = colorList.ItemsSource.OfType<CustomColorsDialog.CustomColorItem>().ToList();
					// 30 个可编辑颜色 key（_editableColorKeys）。
					Assert.Equal(30, items.Count);

					// 修复前：全部 "#FFFFFF"（索引器查不到合并字典 → fallback）。
					// headless App 合并的是 Generic.Light.axaml → Colors.Light.axaml 的预设原色。
					Assert.Equal("#007ACC", FindHex(items, "AccentColor"));
					Assert.Equal("#222222", FindHex(items, "ForegroundColor"));
					Assert.Equal("#C14047", FindHex(items, "Syntax.KeywordColor"));
					// 至少一项不是 #FFFFFF（直接反证用户报告的"全是 #FFFFFF"）。
					Assert.Contains(items, i => i.HexValue != "#FFFFFF");
					// 所有 hex 均为合法 #RRGGBB。
					Assert.All(items, i => Assert.Matches(HexPattern, i.HexValue));

					dialog.Close();
					Dispatcher.UIThread.RunJobs();
					return 0;
				}
				finally
				{
					ForkPlusSettings.Default.CustomColors = originalColors;
					ForkPlusSettings.Default.UseCustomColors = originalUseCustom;
				}
			}).GetAwaiter().GetResult();
		}

		[Fact]
		public void SwatchesGetBorderBrush_FromMergedDictionary()
		{
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				Dictionary<string, string> originalColors = ForkPlusSettings.Default.CustomColors;
				bool originalUseCustom = ForkPlusSettings.Default.UseCustomColors;
				try
				{
					ForkPlusSettings.Default.CustomColors = new Dictionary<string, string>();
					ForkPlusSettings.Default.UseCustomColors = false;

					var dialog = new CustomColorsDialog();
					dialog.Show();
					Dispatcher.UIThread.RunJobs();

					// 预设色板 30 个色块：修复前 BorderBrush 恒为 null（索引器查不到合并字典里
					// 的 BorderBrush），修复后取到主题画刷。SwatchPanel 在 ColorPickerPopup 内，
					// Popup 未打开不在视觉树——经 x:Name 字段反射取（ctor 里已填充 30 个色块）。
					var swatchPanelField = typeof(CustomColorsDialog).GetField("SwatchPanel",
						System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
					Assert.NotNull(swatchPanelField);
					var swatchPanel = (WrapPanel)swatchPanelField.GetValue(dialog);
					var swatches = swatchPanel.Children.OfType<Border>().ToList();
					Assert.Equal(30, swatches.Count);
					Assert.All(swatches, s => Assert.NotNull(s.BorderBrush));

					dialog.Close();
					Dispatcher.UIThread.RunJobs();
					return 0;
				}
				finally
				{
					ForkPlusSettings.Default.CustomColors = originalColors;
					ForkPlusSettings.Default.UseCustomColors = originalUseCustom;
				}
			}).GetAwaiter().GetResult();
		}

		[Fact]
		public void ResourceLookup_LastMergedDictionaryWins()
		{
			// 守卫 ResourceCompat.TryFindResource 的 WPF Resources[key] 等价语义：
			// 逆序穿透合并字典，末尾 merge 的字典优先命中——App.ApplyCustomColors 把自定义
			// 覆盖字典 merge 在末尾，靠这条语义 GetCurrentColorHex 才能取到"当前生效色"。
			HeadlessAppBootstrap.EnsureStarted();
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				var overrideDict = new ResourceDictionary
				{
					["AccentColor"] = Color.Parse("#123456"),
				};
				Application.Current.Resources.MergedDictionaries.Add(overrideDict);
				try
				{
					object overridden = ResourceCompat.TryFindResource(Application.Current, "AccentColor");
					Assert.True(overridden is Color c1 && c1 == Color.Parse("#123456"));
				}
				finally
				{
					Application.Current.Resources.MergedDictionaries.Remove(overrideDict);
				}

				// 移除覆盖后回落主题原色（headless 合并 Generic.Light → Colors.Light）。
				object theme = ResourceCompat.TryFindResource(Application.Current, "AccentColor");
				Assert.True(theme is Color c2 && c2 == Color.Parse("#007ACC"));
				return 0;
			}).GetAwaiter().GetResult();
		}

		private static string FindHex(List<CustomColorsDialog.CustomColorItem> items, string key)
		{
			return items.First(i => i.Key == key).HexValue;
		}
	}
}
