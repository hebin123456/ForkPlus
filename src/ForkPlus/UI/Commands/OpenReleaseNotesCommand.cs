using System;
using Avalonia.Input;
using ForkPlus;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI.Commands
{
	/// <summary>
	/// 打开当前版本"更新内容"弹窗（2026-09-17 实装）：弹窗展示当前版本号 + 随包
	/// RELEASE_NOTE.md 里 "## v{版本}" 章节的 markdown 渲染内容。入口：
	/// 双击任意弹窗左上角的 Fork 图标（ForkPlusDialogWindow.AddForkPlusLogo 接线）。
	/// </summary>
	public class OpenReleaseNotesCommand : IUICommand, IForkPlusCommand
	{
		public string Title { get; } = "";


		public KeyGesture Shortcut => null;

		public KeyGesture SecondaryShortcut => null;

		/// <summary>测试注入：替代默认 ShowDialog（捕获弹窗实例断言内容），用例结束必须复位。</summary>
		internal static Action<ReleaseNotesWindow> ShowWindowForTests;

		public void Execute()
		{
			ReleaseNotesWindow window = CreateWindow();
			if (ShowWindowForTests != null)
			{
				ShowWindowForTests(window);
				return;
			}
			window.ShowDialog();
		}

		/// <summary>构建当前版本的"更新内容"弹窗：App.Version + 随包 RELEASE_NOTE.md 当前版本章节。</summary>
		internal static ReleaseNotesWindow CreateWindow()
		{
			string version = App.Version;
			return new ReleaseNotesWindow(version, ReleaseNotesProvider.GetBundledNotesForVersion(version) ?? "");
		}
	}
}
