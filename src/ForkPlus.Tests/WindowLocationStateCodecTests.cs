using Avalonia;
using ForkPlus.Settings;
using ForkPlus.UI;
using Newtonsoft.Json.Linq;
using Xunit;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

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
		[InlineData(100.0, 200.0, 1000.0, 600.0, global::Avalonia.Controls.WindowState.Normal)]
		[InlineData(50.0, 75.0, 1920.0, 1080.0, global::Avalonia.Controls.WindowState.Maximized)]
		[InlineData(0.0, 0.0, 800.0, 600.0, global::Avalonia.Controls.WindowState.Minimized)]
		public void EncodeDecode_RoundTrip_PreservesAllFields(double left, double top, double width, double height, global::Avalonia.Controls.WindowState state)
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
			var maximized = new WindowLocationState(0, 0, 100, 100, global::Avalonia.Controls.WindowState.Maximized);
			JObject json = CustomDecoders.Encode(maximized);

			Assert.Equal(2, json["WindowState"].Value<int>());
		}

	[Fact]
	public void EncodeDecode_MaximizedStateRoundTrips()
	{
		// 直接针对"窗口最大化记不住"的 bug：最大化状态经存取后必须仍是最大化。
		var maximized = new WindowLocationState(10, 20, 1000, 700, global::Avalonia.Controls.WindowState.Maximized);

		JObject json = CustomDecoders.Encode(maximized);
		WindowLocationState restored = CustomDecoders.DecodeWindowLocationState(json);

		Assert.Equal(global::Avalonia.Controls.WindowState.Maximized, restored.WindowState);
	}

	// ===== 修复（2026-09-17，"每次启动主窗口缩成极小窗，settings.json Width/Height=0.0"）=====
	// 根因：关闭链路上 Window_Closing 二次触发（窗口句柄已销毁）→ GetWindowPlacement
	// 静默失败 → 全零 WindowLocationState 落盘。Decode 端拒绝退化几何，让已污染的
	// settings.json 在下次启动时回退默认尺寸（1000×600），形成自愈闭环。

	[Theory]
	[InlineData(0.0, 0.0, 0.0, 0.0)]          // 用户实例：全零
	[InlineData(0.0, 0.0, 0.0, 600.0)]        // 宽为 0
	[InlineData(0.0, 0.0, 1000.0, 0.0)]       // 高为 0
	[InlineData(100.0, 50.0, -5.0, 600.0)]    // 负宽
	[InlineData(100.0, 50.0, 1000.0, -1.0)]   // 负高
	public void Decode_DegenerateSize_ReturnsNull_SoDefaultSizeIsUsed(double left, double top, double width, double height)
	{
		var poisoned = new JObject
		{
			["Left"] = new JValue(left),
			["Top"] = new JValue(top),
			["Width"] = new JValue(width),
			["Height"] = new JValue(height),
			["WindowState"] = new JValue(0)
		};

		// null → ForkPlusSettings.Decode 回退 new WindowLocationState(100,100,1000,600,...)
		Assert.Null(CustomDecoders.DecodeWindowLocationState(poisoned));
	}

	[Fact]
	public void Decode_NanSize_ReturnsNull()
	{
		var poisoned = new JObject
		{
			["Left"] = new JValue(0.0),
			["Top"] = new JValue(0.0),
			["Width"] = new JValue(double.NaN),
			["Height"] = new JValue(double.NaN),
			["WindowState"] = new JValue(0)
		};

		Assert.Null(CustomDecoders.DecodeWindowLocationState(poisoned));
	}

	[Fact]
	public void Decode_LeftTopZeroWithValidSize_StillDecodes()
	{
		// 守卫：Left/Top=(0,0) 合法（窗口贴屏幕左上角），不得误杀——
		// 既有用例（最小化窗口 Left/Top=0）依赖该语义。
		var json = new JObject
		{
			["Left"] = new JValue(0.0),
			["Top"] = new JValue(0.0),
			["Width"] = new JValue(800.0),
			["Height"] = new JValue(600.0),
			["WindowState"] = new JValue((int)global::Avalonia.Controls.WindowState.Minimized)
		};

		WindowLocationState restored = CustomDecoders.DecodeWindowLocationState(json);

		Assert.NotNull(restored);
		Assert.Equal(0.0, restored.Left);
		Assert.Equal(0.0, restored.Top);
		Assert.Equal(800.0, restored.Width);
		Assert.Equal(600.0, restored.Height);
		Assert.Equal(global::Avalonia.Controls.WindowState.Minimized, restored.WindowState);
	}
}
}
