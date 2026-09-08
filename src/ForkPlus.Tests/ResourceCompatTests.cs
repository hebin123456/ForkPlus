// 本轮25 回归：ResourceCompat.TryFindResource / SetResourceReference 修复"弹窗残缺"。
// 根因（headless 探针实测 + Avalonia 12.1.1 源码核实）：原 shim 走 IResourceHost.TryGetResource
// 实例方法——该方法只查元素自身 Resources/Styles、不沿逻辑树上溯（StyledElement.cs:
// return (_resources?.TryGetResource(...)) || (_styles?.TryGetResource(...))），App 级资源
// （App.axaml 合并字典里的 BackgroundBrush/BorderBrush/各 Icon）永远解析不到——即使元素
// 已挂在显示中的窗口里（探针实测 attachedShim=False）。
// 修复：改走 ResourceNodeExtensions.TryFindResource 链式扩展（沿 StylingParent：
// 元素 → 逻辑树祖先 → TopLevel → GlobalStyles/Application → 主题资源），
// SetResourceReference 另订阅 AttachedToLogicalTree/ResourcesChanged 做"先建后挂树"补解析
// 与主题切换重解析（同 XAML DynamicResource 的事件源）。
// 注：探针资源在各测试内注册（而非 App 初始化时）——多个测试类共享 HeadlessAppBootstrap
// 启动的同一 headless Application（真实 App，ModuleInitializer 即启动），App 初始化期
// 注册无处下手；探针 key 与真实 App 资源 key 无冲突，每次进 Run 幂等重注册。
using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using ForkPlus.UI.WpfCompat;
using Xunit;

namespace ForkPlus.Tests
{
	// 同一 Collection：与其余 headless 测试类串行（共享 HeadlessAppBootstrap 启动的
	// 真实 App；探针资源仍各测试内注册，保持测试间独立性）。
	[Collection("HeadlessAvalonia")]
	public class ResourceCompatTests
	{
		private static void EnsureProbeResources()
		{
			var res = Application.Current.Resources;
			res["CompatProbeBrush"] = new SolidColorBrush(Colors.Pink);
			res["CompatProbeBrush2"] = new SolidColorBrush(Colors.Lime);
		}

		private static T Run<T>(Func<T> func)
		{
			// 复用共享 bootstrap 的 Run（UI 线程 + 排空 job），叠加本类的探针资源注册。
			return HeadlessAppBootstrap.Run(delegate
			{
				EnsureProbeResources();
				T result = func();
				Dispatcher.UIThread.RunJobs();
				return result;
			});
		}

		[Fact]
		public void AttachedElement_TryFindResource_ResolvesAppLevelResource()
		{
			// 修复验证：挂在已显示窗口里的元素能沿链解析 App 级资源。
			//（修复前：实例 TryGetResource 只查自身 → null；探针实测 attachedShim=False，
			//  即 git mm 弹窗"残缺"（无背景无边框）的根因。）
			bool found = Run(delegate
			{
				var window = new Window { Width = 400, Height = 300 };
				var border = new Border();
				window.Content = border;
				window.Show();
				window.UpdateLayout();
				bool ok = border.TryFindResource("CompatProbeBrush") != null;
				window.Close();
				return ok;
			});
			Assert.True(found);
		}

		[Fact]
		public void DetachedElement_TryFindResource_DoesNotResolveAppLevelResource()
		{
			// 孤立（未挂树）元素链上溯在自身处截止 → App 级资源解析不到（null）。
			// 证明"残缺"与挂树状态相关：代码构建内容必须挂树后资源才可达。
			bool resolved = Run(delegate
			{
				var border = new Border();
				return border.TryFindResource("CompatProbeBrush") != null;
			});
			Assert.False(resolved);
		}

		[Fact]
		public void DetachedElement_SetResourceReference_ResolvesAfterAttach()
		{
			// 生产"弹窗残缺"场景回归：代码构建的内容先 SetResourceReference（此刻未挂树，
			// 解析不到 → 属性回落默认值），随后挂进已显示窗口 → AttachedToLogicalTree
			// 触发补解析 → 拿到 App 级资源。
			//（修复前一次性赋值拿不到值且永不再试 → 无背景无边框"残缺"。）
			bool resolved = Run(delegate
			{
				var window = new Window { Width = 400, Height = 300 };
				var panel = new Panel();
				window.Content = panel;
				window.Show();
				window.UpdateLayout();

				var border = new Border(); // 孤立元素
				border.SetResourceReference(Border.BackgroundProperty, "CompatProbeBrush");
				bool beforeAttach = border.Background != null; // false：链断，回落默认

				panel.Children.Add(border); // 挂树 → AttachedToLogicalTree → 补解析
				window.UpdateLayout();

				bool afterAttach = border.Background is SolidColorBrush b && b.Color == Colors.Pink;
				window.Close();
				return !beforeAttach && afterAttach;
			});
			Assert.True(resolved);
		}

		[Fact]
		public void AttachedElement_SetResourceReference_ResolvesImmediately()
		{
			// 已挂树元素调用即解析（保留原一次性赋值语义）。
			bool resolved = Run(delegate
			{
				var window = new Window { Width = 400, Height = 300 };
				var panel = new Panel();
				var border = new Border();
				panel.Children.Add(border);
				window.Content = panel;
				window.Show();
				window.UpdateLayout();

				border.SetResourceReference(Border.BackgroundProperty, "CompatProbeBrush");
				bool ok = border.Background is SolidColorBrush b && b.Color == Colors.Pink;
				window.Close();
				return ok;
			});
			Assert.True(resolved);
		}

		[Fact]
		public void SetResourceReference_MissingKey_ResetsToDefault()
		{
			// 未找到资源 → UnsetValue → 属性回落默认（对齐 XAML DynamicResource 语义；
			// 用"先设值再指向缺失 key"验证回落）。
			bool reset = Run(delegate
			{
				var window = new Window { Width = 400, Height = 300 };
				var border = new Border { Background = Brushes.Red };
				window.Content = border;
				window.Show();
				window.UpdateLayout();

				border.SetResourceReference(Border.BackgroundProperty, "NoSuchResourceKey");
				bool ok = border.Background == null;
				window.Close();
				return ok;
			});
			Assert.True(reset);
		}

		[Fact]
		public void SetResourceReference_SamePropertyLastKeyWins()
		{
			// 同一属性多次调用（StageFileUserControl 切换 StageAll/UnstageAll 图标场景）：
			// 按订阅顺序推送，后调用的 key 最终生效。
			bool lastWins = Run(delegate
			{
				var window = new Window { Width = 400, Height = 300 };
				var border = new Border();
				window.Content = border;
				window.Show();
				window.UpdateLayout();

				border.SetResourceReference(Border.BackgroundProperty, "CompatProbeBrush");
				border.SetResourceReference(Border.BackgroundProperty, "CompatProbeBrush2");

				bool ok = border.Background is SolidColorBrush b && b.Color == Colors.Lime;
				window.Close();
				return ok;
			});
			Assert.True(lastWins);
		}
	}
}
