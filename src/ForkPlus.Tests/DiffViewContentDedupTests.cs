// 回归测试（v5.1.2，"窗口重新获得焦点触发对比视图无条件刷新，重内容文件反复重新渲染"）：
// Window_Activated → RepositoryUserControl 刷新 → 重拉变更列表后重新选中当前文件，
// 重新构造 DiffViewContext 并再次调用 PluginDiffViewControl.UpdateDiff。
// 修复前插件视图 SetContent 无条件执行 → Hex/图片/视频视图被全量重建（重新解码、
// 丢"加载更多"进度、视图模式被重置为并排）。修复分两层：
//   A) 宿主侧内容指纹去重：PluginDiffViewControl.UpdateDiff 在 SetContent 前算
//      DiffViewContentFingerprint，与上次相同则直接返回；
//   B) 插件侧幂等守卫：Hex/图片/视频视图 SetContent 开头算内容签名，相同则早退。
// 判定原则保守：只有当能证明两份内容对视图渲染等价时才跳过，否则照常重新下发。
// 本文件覆盖两层：指纹等价性单元（A 的核心语义）+ 生产路径集成（A+B 组合行为）。
using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Plugins.Abstractions;
using ForkPlus.Plugins.BuiltIn.HexDiff;
using ForkPlus.UI.Plugins;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.BinaryDiff;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>A 方案核心：内容指纹等价性语义（能证明等价才判相等）。</summary>
	public class DiffViewContentFingerprintTests
	{
		private static byte[] Bytes(int count, int seed)
		{
			byte[] data = new byte[count];
			for (int i = 0; i < count; i++)
			{
				data[i] = (byte)((i * seed + seed * 7 + 13) % 251);
			}
			return data;
		}

		/// <summary>构造上下文：每侧每轮都用全新的流/画刷实例，模拟刷新路径重新构造对象。</summary>
		private static DiffViewContext Context(string path, byte[] srcBytes, byte[] dstBytes,
			IBrush srcBrush = null, IBrush dstBrush = null)
		{
			DiffSideContent src = (srcBytes == null) ? null
				: new DiffSideContent(path, true, srcBytes.Length, () => new MemoryStream(srcBytes));
			DiffSideContent dst = (dstBytes == null) ? null
				: new DiffSideContent(path, true, dstBytes.Length, () => new MemoryStream(dstBytes));
			return new DiffViewContext(path, true, src, dst, DiffSideRole.Old, DiffSideRole.New,
				srcBrush ?? new SolidColorBrush(Colors.Red), dstBrush ?? new SolidColorBrush(Colors.Green));
		}

		[Fact]
		public void EquivalentContent_NewInstances_ProducesEqualFingerprints()
		{
			byte[] src = Bytes(4096, 3);
			byte[] dst = Bytes(4096, 11);
			DiffViewContentFingerprint first = DiffViewContentFingerprint.Create("forkplus.hex", Context("data.bin", src, dst));
			// 第二轮：全新流 / 全新同色画刷实例——内容等价，应判相等（否则刷新会误重建）
			DiffViewContentFingerprint second = DiffViewContentFingerprint.Create("forkplus.hex", Context("data.bin", src, dst));
			Assert.Equal(first, second);
		}

		[Fact]
		public void DifferentBytes_ProducesDifferentFingerprints()
		{
			byte[] src = Bytes(4096, 3);
			byte[] dstA = Bytes(4096, 11);
			byte[] dstB = Bytes(4096, 11);
			dstB[1234] ^= 0xFF; // 仅一个字节不同
			DiffViewContentFingerprint a = DiffViewContentFingerprint.Create("forkplus.hex", Context("data.bin", src, dstA));
			DiffViewContentFingerprint b = DiffViewContentFingerprint.Create("forkplus.hex", Context("data.bin", src, dstB));
			Assert.NotEqual(a, b);
		}

		[Fact]
		public void DifferentPath_ProducesDifferentFingerprints()
		{
			byte[] src = Bytes(1024, 5);
			DiffViewContentFingerprint a = DiffViewContentFingerprint.Create("forkplus.hex", Context("a.bin", src, null));
			DiffViewContentFingerprint b = DiffViewContentFingerprint.Create("forkplus.hex", Context("b.bin", src, null));
			Assert.NotEqual(a, b);
		}

		[Fact]
		public void DifferentPluginId_ProducesDifferentFingerprints()
		{
			byte[] src = Bytes(1024, 5);
			DiffViewContentFingerprint a = DiffViewContentFingerprint.Create("forkplus.hex", Context("a.bin", src, null));
			DiffViewContentFingerprint b = DiffViewContentFingerprint.Create("forkplus.image", Context("a.bin", src, null));
			Assert.NotEqual(a, b);
		}

		[Fact]
		public void DifferentBrushColor_ProducesDifferentFingerprints()
		{
			byte[] src = Bytes(1024, 5);
			DiffViewContentFingerprint a = DiffViewContentFingerprint.Create("forkplus.hex",
				Context("a.bin", src, null, new SolidColorBrush(Colors.Red)));
			DiffViewContentFingerprint b = DiffViewContentFingerprint.Create("forkplus.hex",
				Context("a.bin", src, null, new SolidColorBrush(Colors.Blue)));
			Assert.NotEqual(a, b);
		}

		[Fact]
		public void DifferentHexBytes_ProducesDifferentFingerprints()
		{
			// 仅 Hex 预载字节不同（两侧声明大小一致）：仍须判不等，避免漏刷新
			byte[] hexA = Bytes(2048, 7);
			byte[] hexB = Bytes(2048, 7);
			hexB[9] ^= 0x5A;
			DiffViewContext a = new DiffViewContext("data.bin", true, null, null,
				DiffSideRole.Old, DiffSideRole.New, null, null,
				new MemoryStream(hexA), null);
			DiffViewContext b = new DiffViewContext("data.bin", true, null, null,
				DiffSideRole.Old, DiffSideRole.New, null, null,
				new MemoryStream(hexB), null);
			Assert.NotEqual(
				DiffViewContentFingerprint.Create("forkplus.hex", a),
				DiffViewContentFingerprint.Create("forkplus.hex", b));
		}
	}

	/// <summary>A+B 组合行为：走生产路径 PluginDiffViewControl.UpdateDiff 验证去重与不误跳过。</summary>
	[Collection("HeadlessAvalonia")]
	public class DiffViewContentDedupTests
	{
		private static byte[] Bytes(int count, int seed)
		{
			byte[] data = new byte[count];
			new Random(seed).NextBytes(data);
			return data;
		}

		/// <summary>装配 PluginDiffViewControl（反射注入 GitModule，与既有两个 BinaryDiff 用例同款）。</summary>
		private static PluginDiffViewControl ShowDiffHost(out Window window)
		{
			var repoControl = new RepositoryUserControl();
			typeof(RepositoryUserControl).GetProperty("GitModule")!
				.SetValue(repoControl, new GitModule(Directory.GetCurrentDirectory(),
					Directory.GetCurrentDirectory(), null, null));
			var diffHost = new PluginDiffViewControl();
			window = new Window { Width = 900, Height = 500, Content = diffHost };
			window.Show();
			Dispatcher.UIThread.RunJobs();
			// 宿主刷新时复用同一 repoControl：把引用挂到 diffHost 供后续 UpdateDiff 传入。
			diffHost.Tag = repoControl;
			return diffHost;
		}

		private static ChangedFile BinaryChangedFile()
		{
			return new ChangedFile("data.bin", StatusType.Modified, StatusType.None,
				ChangeType.Modified, staged: true, isNew: false, tracked: true);
		}

		private static void Deliver(PluginDiffViewControl diffHost, ChangedFile changedFile, byte[] src, byte[] dst)
		{
			diffHost.UpdateDiff((RepositoryUserControl)diffHost.Tag,
				new UnknownBinaryDiffContent(changedFile, src.Length, dst.Length), true,
				new HexDiffContent(changedFile, new MemoryStream(src), new MemoryStream(dst)));
			Dispatcher.UIThread.RunJobs();
		}

		[Fact]
		public void EquivalentContent_Reissued_ReusesViewAndKeepsHexMode()
		{
			HeadlessAppBootstrap.EnsureStarted();
			bool[] holder = new bool[3];
			Window window = null;
			try
			{
				Dispatcher.UIThread.InvokeAsync(delegate
				{
					PluginDiffViewControl diffHost = ShowDiffHost(out window);
					ChangedFile changedFile = BinaryChangedFile();
					byte[] src = Bytes(32 * 1024, 1);
					byte[] dst = Bytes(32 * 1024, 2);

					Deliver(diffHost, changedFile, src, dst);
					HexDiffView hexView = diffHost.GetVisualDescendants().OfType<HexDiffView>().FirstOrDefault();
					Assert.NotNull(hexView);

					// 切到 Hex 模式：若发生重建，UpdateContent 会把模式重置回并排
					hexView.HexRadioButton.IsChecked = true;
					Dispatcher.UIThread.RunJobs();
					holder[0] = hexView.HexRadioButton.IsChecked.GetValueOrDefault();

					// 窗口重新激活路径：重拉变更列表 → 内容等价、但流为全新实例
					Deliver(diffHost, changedFile, src, dst);

					holder[1] = ReferenceEquals(hexView,
						diffHost.GetVisualDescendants().OfType<HexDiffView>().FirstOrDefault());
					holder[2] = hexView.HexRadioButton.IsChecked.GetValueOrDefault();
					return 0;
				}).GetAwaiter().GetResult();
			}
			finally
			{
				if (window != null)
				{
					Dispatcher.UIThread.InvokeAsync(window.Close).GetAwaiter().GetResult();
				}
			}

			Assert.True(holder[0], "首次下发后应能切到 Hex 模式");
			Assert.True(holder[1], "同内容重复下发应复用同一视图实例（未重建）");
			Assert.True(holder[2], "同内容重复下发后 Hex 模式选择应保留（未被 UpdateContent 重置为并排）");
		}

		[Fact]
		public void ChangedContent_Reissued_RebuildsView()
		{
			HeadlessAppBootstrap.EnsureStarted();
			bool rebuilt = false;
			Window window = null;
			try
			{
				Dispatcher.UIThread.InvokeAsync(delegate
				{
					PluginDiffViewControl diffHost = ShowDiffHost(out window);
					ChangedFile changedFile = BinaryChangedFile();
					byte[] src = Bytes(32 * 1024, 1);
					byte[] dst = Bytes(32 * 1024, 2);

					Deliver(diffHost, changedFile, src, dst);
					HexDiffView hexView = diffHost.GetVisualDescendants().OfType<HexDiffView>().FirstOrDefault();
					Assert.NotNull(hexView);
					hexView.HexRadioButton.IsChecked = true;
					Dispatcher.UIThread.RunJobs();
					Assert.True(hexView.HexRadioButton.IsChecked.GetValueOrDefault());

					// 内容确实变了：必须重新下发（UpdateContent 把模式重置回并排），不得被误判为"未变化"而跳过
					byte[] dstChanged = Bytes(32 * 1024, 99);
					Deliver(diffHost, changedFile, src, dstChanged);
					rebuilt = !hexView.HexRadioButton.IsChecked.GetValueOrDefault();
					return 0;
				}).GetAwaiter().GetResult();
			}
			finally
			{
				if (window != null)
				{
					Dispatcher.UIThread.InvokeAsync(window.Close).GetAwaiter().GetResult();
				}
			}

			Assert.True(rebuilt, "内容变化时不得误跳过：视图应重新下发（模式被 UpdateContent 重置）");
		}
	}
}