// v4.0.5（2026-09-09）："ARM 版本没有持久化配置文件"修复的回归测试。
// 根因：RepositoryManager.Save() 的 21 处调用点全部裸调 bt_save_repository_manager，
// ARM 老系统（glibc < libbiturbo.so 所需版本）上 NativeLibrary.Load 抛 DllNotFoundException
// 直接炸 UI 且零字节落盘。修复 = Save() try-catch + SaveManagedFallback() 托管兜底
// （镜像进 settings.json 的 RepositoryManager 节，与 Load() 既有回退导入路径闭环）。
//
// 两条用例：
//   1) SaveManagedFallback_WritesSettingsJson：写侧兜底——构造已知状态，落盘后直接
//      从 settings.json 反序列化校验契约（Path/Name/Color/时间戳精确往返）。
//   2) Load_ImportsFromSettingsWhenNativeReadFails：读侧回退——repositories.toml 写入
//      垃圾内容强制 bt_get 解析失败，Load() 应从 settings.json 的 RepositoryManager 节导入。
//
// 全局状态自恢复：结束还原 settings.json 与 repositories.toml 原内容（同进程 E2E 用例
// 依赖真实数据目录，见 SettingsPersistenceRoundTripTests 的同类处理）。
using System;
using System.IO;
using ForkPlus.Settings;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class RepositoryManagerPersistenceFallbackTests
	{
		private readonly ITestOutputHelper _output;

		public RepositoryManagerPersistenceFallbackTests(ITestOutputHelper output)
		{
			_output = output;
		}

		[Fact]
		public void SaveManagedFallback_WritesSettingsJson()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				string settingsPath = Path.Combine(App.ForkDirectoryPath, "settings.json");
				string? originalSettings = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
				try
				{
					// 已知状态：1 个仓库（带别名/颜色/打开时间戳）+ 源目录 + 扫描深度
					uint opened = (uint)(DateTime.UtcNow - DateTimeExtensions.UnixStartTime).TotalSeconds;
					RepositoryManager.Repository repo = new RepositoryManager.Repository(
						"/tmp/fpe2e-fallback/repo.git", "my-alias", (int)opened, RepositoryColor.Blue);
					RepositoryManager manager = new RepositoryManager(
						new string[1] { "/tmp/fpe2e-fallback" }, (byte)7, new string[0],
						new RepositoryManager.Repository[1] { repo });

					manager.SaveManagedFallback();

					Assert.True(File.Exists(settingsPath), "兜底应写入 settings.json");
					JObject json = JObject.Parse(File.ReadAllText(settingsPath));
					JToken? rmToken = json["RepositoryManager"];
					Assert.NotNull(rmToken);
					ForkPlusSettings.RepositoryManagerSettings? decoded =
						ForkPlusSettings.RepositoryManagerSettings.Coder.Decode(rmToken);
					Assert.NotNull(decoded);

					_output.WriteLine($"SourceDirectories: {string.Join(",", decoded.SourceDirectories)}, ScanDepth: {decoded.ScanDepth}");
					Assert.Equal(7, decoded.ScanDepth);
					// v4.0.5 源目录规范化：平台分隔符结尾（Unix "/"，Windows "\"）
					string expectedSourceDir = OperatingSystem.IsWindows() ? "/tmp/fpe2e-fallback\\" : "/tmp/fpe2e-fallback/";
					Assert.Contains(expectedSourceDir, decoded.SourceDirectories);
					Assert.Single(decoded.Repositories);
					Assert.Equal("/tmp/fpe2e-fallback/repo.git", decoded.Repositories[0].Path);
					Assert.Equal("my-alias", decoded.Repositories[0].Name);
					Assert.Equal(RepositoryColor.Blue, decoded.Repositories[0].Color);
					// 时间戳按 UnixStartTime 精确往返（Import 侧 TimeIntervalSince1970 应还原同值）
					Assert.Equal((int)opened, decoded.Repositories[0].LastAccessTime.TimeIntervalSince1970());
				}
				finally
				{
					RestoreFile(settingsPath, originalSettings);
				}
			});
		}

		[Fact]
		public void Load_ImportsFromSettingsWhenNativeReadFails()
		{
			HeadlessAppBootstrap.Run(delegate
			{
				string settingsPath = Path.Combine(App.ForkDirectoryPath, "settings.json");
				string tomlPath = App.RepositoriesFilePath;
				string? originalSettings = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
				string? originalToml = File.Exists(tomlPath) ? File.ReadAllText(tomlPath) : null;
				try
				{
					// 1) settings.json 写入已知仓库（走生产编码路径）
					uint opened = (uint)(DateTime.UtcNow - DateTimeExtensions.UnixStartTime).TotalSeconds;
					ForkPlusSettings.RepositoryManagerSettings.Repository settingsRepo =
						new ForkPlusSettings.RepositoryManagerSettings.Repository(
							1, "fallback-repo", "/tmp/fpe2e-fallback/imported.git", null,
							DateTimeExtensions.UnixStartTime.AddSeconds((long)opened), 0, RepositoryColor.Green);
					ForkPlusSettings.Default.RepositoryManager = new ForkPlusSettings.RepositoryManagerSettings(
						new string[1] { "/tmp/fpe2e-fallback" },
						new ForkPlusSettings.RepositoryManagerSettings.Category[0],
						new ForkPlusSettings.RepositoryManagerSettings.Repository[1] { settingsRepo },
						5);
					ForkPlusSettings.Default.Save();

					// 2) repositories.toml 写垃圾内容强制原生解析失败（模拟 ARM 上原生库不可用的效果：
					//    BtRequest.Run 捕获异常 → GitCommandResult.Failure → 走 settings 导入回退）
					Directory.CreateDirectory(App.ForkDataDirectoryPath);
					File.WriteAllText(tomlPath, "}}} not a valid toml [[[ ???");

					// 3) Load() 应从 settings.json 导入
					RepositoryManager loaded = RepositoryManager.Load();
					RepositoryManager.Repository? imported = Array.Find(loaded.Repositories,
						(RepositoryManager.Repository r) => r.Path == "/tmp/fpe2e-fallback/imported.git");
					Assert.NotNull(imported);
					_output.WriteLine($"Imported: {imported.Value.Path} alias={imported.Value.Alias} color={imported.Value.Color}");
					Assert.Equal("fallback-repo", imported.Value.Alias);
					Assert.Equal(RepositoryColor.Green, imported.Value.Color);
					Assert.Equal((int)opened, imported.Value.Opened ?? 0);
					Assert.Equal(5, loaded.ScanDepth);
				}
				finally
				{
					RestoreFile(settingsPath, originalSettings);
					RestoreFile(tomlPath, originalToml);
				}
			});
		}

		private static void RestoreFile(string path, string? originalContent)
		{
			try
			{
				if (originalContent != null)
				{
					File.WriteAllText(path, originalContent);
				}
				else if (File.Exists(path))
				{
					File.Delete(path);
				}
			}
			catch (Exception ex)
			{
				// 恢复失败不应吞掉断言结果，仅记录（同进程后续用例风险自担）
				System.Console.Error.WriteLine($"Failed to restore {path}: {ex.Message}");
			}
		}
	}
}
