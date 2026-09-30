using System;
using ForkPlus.Settings;
using ForkPlus.UI.Dialogs;

namespace ForkPlus.UI
{
	/// <summary>
	/// 新手引导管理器：首次启动（OnboardingCompleted=false，看过或跳过均置位）时
	/// 自动弹 OnboardingTourWindow 向导；关闭后置位并保存设置，后续启动不再自动弹。
	/// 与 ReleaseNotesManager 同款"先置位再弹窗"策略——置位放在 ShowDialog 之前，
	/// 即使弹窗期间崩溃/断电也不会陷入每次启动重弹。帮助菜单"Getting Started"
	/// 的手动入口不走本管理器（直接 ShowOnboardingTourCommand 弹窗，不改标志）。
	/// 展示时机：主窗口 Loaded 后延迟一帧（与 ReleaseNotesManager 同一启动钩子，
	/// 模态弹窗挂起在独立 dispatcher 帧，不阻塞启动链路）。
	/// </summary>
	internal class OnboardingManager
	{
		/// <summary>测试注入：捕获弹窗行为（默认真正弹 OnboardingTourWindow）。</summary>
		internal static Action ShowWindowForTests;

		/// <summary>
		/// 首次启动未完成引导时弹新手引导向导。幂等：已完成时直接返回。
		/// 任何异常吞掉只记日志——启动路径上的装饰性功能不允许影响主流程。
		/// </summary>
		public static void ShowIfFirstLaunch()
		{
			try
			{
				if (ForkPlusSettings.Default.OnboardingCompleted)
				{
					return;
				}
				// 跳过与完整走完均算完成：置位在弹窗前，失败/中断不会每次启动重弹
				ForkPlusSettings.Default.OnboardingCompleted = true;
				ForkPlusSettings.Default.Save();
				if (ShowWindowForTests != null)
				{
					ShowWindowForTests();
				}
				else
				{
					new OnboardingTourWindow().ShowDialog();
				}
			}
			catch (Exception ex)
			{
				Log.Warn("Show onboarding tour failed: " + ex.Message);
			}
		}
	}
}
