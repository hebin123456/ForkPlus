// 诊断复现（2026-09-07，"Linux 持久化失效（每次启动弹引导）"）：
// 核心链路 round-trip：设置 Guid（欢迎窗口完成时的生产调用序）→ Save()（AtomicWrite）→
// 文件真实落盘（~/.local/share/ForkPlus/settings.json，App.ForkDirectoryPath）→ Load() 回读。
// 已排除项：Encode/Decode 键对齐（Guid 两侧都有）；Avalonia 12.1.1 Design.IsDesignMode 仅
// Previewer 注入（internal setter，正常运行恒 false）；MigrateLegacyAppData 只拷贝不删除；
// WriteFile 已跨平台（Unix 走 File.Replace/File.Move）。
// 测试自恢复：结束还原 settings.json 原内容与 Default.Guid 原值，避免污染同进程后续测试。
using System;
using System.IO;
using ForkPlus.Settings;
using Xunit;
using Xunit.Abstractions;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class SettingsPersistenceRoundTripTests
	{
		private readonly ITestOutputHelper _output;

		public SettingsPersistenceRoundTripTests(ITestOutputHelper output)
		{
			_output = output;
		}

		[Fact]
		public void SettingsSave_WritesFileAndLoadRoundTripsGuid()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				string path = Path.Combine(App.ForkDirectoryPath, "settings.json");
				string? originalContent = File.Exists(path) ? File.ReadAllText(path) : null;
				string? originalGuid = ForkPlusSettings.Default.Guid;
				try
				{
					// 生产调用序（WelcomeWindow.OnSubmit 完成）：赋 Guid + Save
					string marker = "e2e-persist-roundtrip-" + Guid.NewGuid().ToString("N").Substring(0, 8);
					ForkPlusSettings.Default.Guid = marker;
					ForkPlusSettings.Default.Save();

					Assert.True(File.Exists(path), "settings.json 应落盘（Linux 写路径验证）");
					string content = File.ReadAllText(path);
					Assert.Contains(marker, content);
					_output.WriteLine($"saved-to={path}, len={content.Length}, marker-ok");

					// 回读（下次启动的 Load 路径）
					ForkPlusSettings reloaded = ForkPlusSettings.Load();
					Assert.Equal(marker, reloaded.Guid);
					_output.WriteLine($"reload-guid={reloaded.Guid}");
				}
				finally
				{
					// 还原：文件内容 + 单例状态（进程内共享，避免污染同进程其他测试）
					if (originalContent != null)
					{
						File.WriteAllText(path, originalContent);
					}
					else if (File.Exists(path))
					{
						File.Delete(path);
					}
					ForkPlusSettings.Default.Guid = originalGuid;
				}
			});
		}
	}
}
