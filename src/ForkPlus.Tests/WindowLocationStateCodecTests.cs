using System.Windows;
using ForkPlus.Settings;
using ForkPlus.UI;
using Newtonsoft.Json.Linq;
using Xunit;

// Phase 0.4：CustomDecoders 已迁入 Core，使用 ForkPlus.UI.WindowState（Core 跨平台枚举）。
// 本测试类不继承自 Window，using 别名可正常工作（不会被实例成员查找覆盖）。
using WindowState = ForkPlus.UI.WindowState;

namespace ForkPlus.Tests
{
	/// <summary>
	/// 窗口位置/状态持久化走 JSON 序列化（CustomDecoders.Encode/DecodeWindowLocationState）。
	/// 历史上窗口最大化状态丢失的根因不在序列化层（序列化存的是 WPF WindowState 枚举值 0/1/2），
	/// 而在读取 Win32 ShowCmd 时的映射（见 <see cref="WindowLocationStateMappingTests"/>）。
	/// 这里锁住序列化往返本身，确保三个状态都能正确存取。
	/// </summary>
	public class WindowLocationStateCodecTests
	{
		[Theory]
		[InlineData(100.0, 200.0, 1000.0, 600.0, WindowState.Normal)]
		[InlineData(50.0, 75.0, 1920.0, 1080.0, WindowState.Maximized)]
		[InlineData(0.0, 0.0, 800.0, 600.0, WindowState.Minimized)]
		public void EncodeDecode_RoundTrip_PreservesAllFields(double left, double top, double width, double height, WindowState state)
		{
			var original = new WindowLocationState(left, top, width, height, state);

			JObject json = CustomDecoders.Encode(original);
			WindowLocationState restored = CustomDecoders.DecodeWindowLocationState(json);

			Assert.NotNull(restored);
			Assert.Equal(left, restored.Left);
			Assert.Equal(top, restored.Top);
			Assert.Equal(width, restored.Width);
			Assert.Equal(height, restored.Height);
			Assert.Equal(state, restored.WindowState);
		}

		[Fact]
		public void Decode_NullJson_ReturnsNull()
		{
			Assert.Null(CustomDecoders.DecodeWindowLocationState(null));
		}

		[Fact]
		public void Decode_MalformedJson_ReturnsNull()
		{
			// 缺少 WindowState 字段，Value&lt;int&gt;() 抛异常，应被 catch 并返回 null
			var malformed = new JObject
			{
				["Left"] = new JValue(10.0),
				["Top"] = new JValue(20.0),
				["Width"] = new JValue(300.0),
				["Height"] = new JValue(200.0)
			};

			Assert.Null(CustomDecoders.DecodeWindowLocationState(malformed));
		}

		[Fact]
		public void Encode_StoresWpfWindowStateEnumValue()
		{
			// 守卫：序列化必须存 WPF WindowState 的枚举值（0/1/2），而不是 Win32 ShowCmd（1/2/3）。
			// 最大化对应 WindowState.Maximized=2。若误存 ShowCmd=3，反序列化会得到未定义枚举值。
			var maximized = new WindowLocationState(0, 0, 100, 100, WindowState.Maximized);
			JObject json = CustomDecoders.Encode(maximized);

			Assert.Equal(2, json["WindowState"].Value<int>());
		}

		[Fact]
		public void EncodeDecode_MaximizedStateRoundTrips()
		{
			// 直接针对"窗口最大化记不住"的 bug：最大化状态经存取后必须仍是最大化。
			var maximized = new WindowLocationState(10, 20, 1000, 700, WindowState.Maximized);

			JObject json = CustomDecoders.Encode(maximized);
			WindowLocationState restored = CustomDecoders.DecodeWindowLocationState(json);

			Assert.Equal(WindowState.Maximized, restored.WindowState);
		}
	}
}
