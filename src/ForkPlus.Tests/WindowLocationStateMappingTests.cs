using System.Windows;
using ForkPlus.UI.Helpers;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// Win32 ShowCmd 与 WPF WindowState 是两套枚举值，不能直接强转：
	///   SW_NORMAL=1, SW_SHOWMINIMIZED=2, SW_SHOWMAXIMIZED=3
	///   WindowState.Normal=0, Minimized=1, Maximized=2
	/// 历史上 <see cref="WindowLocationStateExtensions"/> 读取窗口状态时直接
	/// (WindowState)placement.ShowCmd，导致最大化窗口（ShowCmd=3）被存成无效值，
	/// 恢复时退化为 Normal，窗口最大化状态丢失。这些用例锁住映射函数。
	/// </summary>
	public class WindowLocationStateMappingTests
	{
		[Theory]
		[InlineData(WindowState.Normal, 1)]     // SW_NORMAL
		[InlineData(WindowState.Minimized, 2)]  // SW_SHOWMINIMIZED
		[InlineData(WindowState.Maximized, 3)]  // SW_SHOWMAXIMIZED
		public void ToShowCmd_MapsToWin32ShowCmdValues(WindowState state, int expectedShowCmd)
		{
			Assert.Equal(expectedShowCmd, WindowLocationStateExtensions.ToShowCmd(state));
		}

		[Theory]
		[InlineData(1, WindowState.Normal)]     // SW_NORMAL
		[InlineData(2, WindowState.Minimized)]  // SW_SHOWMINIMIZED
		[InlineData(3, WindowState.Maximized)]  // SW_SHOWMAXIMIZED
		public void FromShowCmd_MapsToCorrectWpfWindowState(int showCmd, WindowState expectedState)
		{
			Assert.Equal(expectedState, WindowLocationStateExtensions.FromShowCmd(showCmd));
		}

		[Theory]
		[InlineData(WindowState.Normal)]
		[InlineData(WindowState.Minimized)]
		[InlineData(WindowState.Maximized)]
		public void ShowCmdRoundTrip_PreservesWindowState(WindowState original)
		{
			// 保存路径：WindowState → ToShowCmd → 存入 Win32 placement.ShowCmd
			// 读取路径：placement.ShowCmd → FromShowCmd → WindowState
			// 这正是 GetWindowLocationState/SetWindowLocationState 的往返。
			int showCmd = WindowLocationStateExtensions.ToShowCmd(original);
			WindowState restored = WindowLocationStateExtensions.FromShowCmd(showCmd);

			Assert.Equal(original, restored);
		}

		[Fact]
		public void FromShowCmd_UnknownValue_DefaultsToNormal()
		{
			// 历史 bug：旧代码 (WindowState)3 产生无效枚举值 3。
			// 新 FromShowCmd 对未知值（含 0、4、99 等）一律回退 Normal，绝不产生非法枚举值。
			Assert.Equal(WindowState.Normal, WindowLocationStateExtensions.FromShowCmd(0));
			Assert.Equal(WindowState.Normal, WindowLocationStateExtensions.FromShowCmd(4));
			Assert.Equal(WindowState.Normal, WindowLocationStateExtensions.FromShowCmd(99));
		}

		[Fact]
		public void FromShowCmd_DoesNotProduceInvalidEnumValue()
		{
			// 守卫：历史上 (WindowState)3 是未定义枚举值，导致保存/恢复逻辑全错。
			// 对所有合法 ShowCmd 输入，FromShowCmd 必须只返回定义过的 WindowState。
			int[] validShowCmds = { 1, 2, 3 };
			foreach (int showCmd in validShowCmds)
			{
				WindowState state = WindowLocationStateExtensions.FromShowCmd(showCmd);
				Assert.True(System.Enum.IsDefined(typeof(WindowState), state),
					"FromShowCmd(" + showCmd + ") 产生了未定义的 WindowState 值 " + (int)state);
			}
		}

		[Fact]
		public void DirectCastShowCmdToWindowState_WouldBeWrong()
		{
			// 文档化为何不能直接强转：记录旧 bug 的具体表现。
			// SW_SHOWMAXIMIZED(3) 直接强转 (WindowState)3 既不是 Normal(0)、Minimized(1)、Maximized(2)，
			// 是未定义值。这条断言永远成立，作为"为什么需要 FromShowCmd"的活文档。
			int swShowMaximized = 3;

			Assert.False(System.Enum.IsDefined(typeof(WindowState), (WindowState)swShowMaximized));
		}
	}
}
