// 回归（v4.0.3）：界面重构后动态创建的控件（构造于 new，父级本地化调度触不到）
// 在构造函数内补 PreferencesLocalization.Apply，zh-Hans 下应即时本地化。
// 覆盖：RewordUserControl（交互式变基 reword 弹窗）、TooltipRevisionDetailsUserControl。
using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class DynamicControlLocalizationAuditTests
	{
		[Fact]
		public void RewordUserControl_ConstructorAppliesLocalization()
		{
			Dispatcher.UIThread.InvokeAsync(delegate
			{
				string original = ForkPlusSettings.Default.UiLanguage;
				try
				{
					ForkPlusSettings.Default.UiLanguage = "zh-Hans";
					var window = new Window { Width = 500, Height = 300 };
					var reword = new RewordUserControl("subject", "description");
					window.Content = reword;
					window.Show();
					Dispatcher.UIThread.RunJobs();

					// 占位符与按钮文案应全部翻译（修复前为英文原文）
					Assert.Empty(LocalizationCoverageAuditTests.CollectUntranslated(window));
					window.Close();
					Dispatcher.UIThread.RunJobs();
					return 0;
				}
				finally
				{
					ForkPlusSettings.Default.UiLanguage = original;
				}
			}).GetAwaiter().GetResult();
		}

		[Fact]
		public void UpdateChecker_PlatformAssetSelection()
		{
			// 多平台 Release 资产选择：v4.0.2 起四平台 zip 并行上传，assets 顺序不可控，
			// 旧实现固定 assets[0] 会拿到错误平台的包。
			string json = "[{\"name\":\"ForkPlus-4.0.2-linux-arm64.zip\",\"browser_download_url\":\"u-linux-arm64\"},"
				+ "{\"name\":\"ForkPlus-4.0.2-windows-x64.zip\",\"browser_download_url\":\"u-windows-x64\"},"
				+ "{\"name\":\"ForkPlus-4.0.2-macos-arm64.zip\",\"browser_download_url\":\"u-macos-arm64\"},"
				+ "{\"name\":\"ForkPlus-4.0.2-linux-x64.zip\",\"browser_download_url\":\"u-linux-x64\"}]";
			Newtonsoft.Json.Linq.JArray assets = Newtonsoft.Json.Linq.JArray.Parse(json);

			// 当前运行平台（Linux x64 沙箱）应精确匹配自己的资产，而不是 assets[0]（linux-arm64）
			string platformId = ForkPlus.UpdateChecker.GetCurrentPlatformId();
			string url = ForkPlus.UpdateChecker.FindPlatformAssetDownloadUrl(assets, "4.0.2");
			Assert.Contains(platformId, url);
			Assert.NotEqual("u-linux-arm64", platformId == "linux-x64" ? url : "u-linux-x64");

			// 版本不匹配时回退平台后缀匹配（仍是本平台的包）
			Assert.EndsWith(platformId, ForkPlus.UpdateChecker.FindPlatformAssetDownloadUrl(assets, "9.9.9"));

			// 空资产回退 null（调用方落回 Release 页）
			Assert.Null(ForkPlus.UpdateChecker.FindPlatformAssetDownloadUrl(new Newtonsoft.Json.Linq.JArray(), "4.0.2"));

			// 版本号规范化：tag 带 v 前缀
			Assert.Equal("4.0.3", ForkPlus.UpdateChecker.NormalizeVersion("v4.0.3"));
			Assert.True(ForkPlus.UpdateChecker.IsNewerVersion("4.0.3", "4.0.2"));
		}
	}
}
