using System;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI
{
	/// <summary>
	/// 首次启动新版本时的"更新内容"弹窗管理器（v4.1.0 起）：当前版本与设置里
	/// 记录的 LastShownReleaseNotesVersion 不一致（即首次启动该版本）时，读取
	/// 随包分发的 Docs/RELEASE_NOTE.md 当前版本章节并弹窗展示；关闭后记录版本，
	/// 同版本后续启动不再弹。章节缺失/文件缺失同样记录版本——不弹窗也不每次
	/// 启动重试（记录失败重试只会反复 IO，无用户价值）。
	/// 展示时机：主窗口 Loaded 后延迟一帧（等首个渲染完成），模态弹窗不阻塞
	/// 启动链路（CLI 命令等在启动帧内继续）。
	/// </summary>
	internal class ReleaseNotesManager
	{
		/// <summary>
		/// 测试注入：替代默认的版本章节提取（默认读安装目录 Docs/RELEASE_NOTE.md）。
		/// 参数为规范化版本号，返回 null 视为"无内容不弹窗"。
		/// </summary>
		internal static Func<string, string> NotesProviderForTests;

		/// <summary>测试注入：捕获弹窗行为（默认真正弹 ReleaseNotesWindow）。</summary>
		internal static Action<string, string> ShowWindowForTests;

		/// <summary>
		/// 首次启动新版本时弹出该版本更新内容。幂等：同版本第二次调用直接返回。
		/// 任何异常吞掉只记日志——启动路径上的装饰性功能不允许影响主流程。
		/// </summary>
		public static void ShowIfFirstLaunchOfNewVersion()
		{
			try
			{
				string version = UpdateChecker.NormalizeVersion(App.Version);
				if (string.IsNullOrEmpty(version))
				{
					return;
				}
				string lastShown = ForkPlusSettings.Default.LastShownReleaseNotesVersion ?? "";
				if (string.Equals(lastShown, version, StringComparison.OrdinalIgnoreCase))
				{
					return; // 该版本已展示过（或已标记无需展示）
				}
				string notes = NotesProviderForTests != null
					? NotesProviderForTests(version)
					: ReleaseNotesProvider.GetBundledNotesForVersion(version);
				// 无论有无内容都记录版本：缺失不弹、后续启动也不再重试
				ForkPlusSettings.Default.LastShownReleaseNotesVersion = version;
				ForkPlusSettings.Default.Save();
				if (!string.IsNullOrEmpty(notes))
				{
					if (ShowWindowForTests != null)
					{
						ShowWindowForTests(version, notes);
					}
					else
					{
						new ReleaseNotesWindow(version, notes).ShowDialog();
					}
				}
			}
			catch (Exception ex)
			{
				Log.Warn("Show release notes failed: " + ex.Message);
			}
		}
	}
}
