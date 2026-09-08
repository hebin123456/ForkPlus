// 探针测试（提交详情对齐问题，2026-09-04）：
// 用户报告"提交 tab 页里 SHA/父提交/Commit 标题和内容应与作者信息对齐，但某些提交节点的内容有缩进"。
// 本探针直接实例化 RevisionSummaryUserControl，模拟各种提交形态（committer 可见/隐藏、
// refs 有/无、描述有/无），实测作者信息与 SHA/PARENTS/Commit 内容的 X 坐标是否一致。
using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class RevisionSummaryAlignmentProbeTests
	{
		private sealed class ProbeResult
		{
			public double AuthorNameX;
			public double AuthorDateX;
			public double ShaX;
			public double ParentsX;
			public double SubjectX;
			public double DescriptionX;
			public double RefsX;

			public override string ToString()
			{
				return $"AuthorName={AuthorNameX} AuthorDate={AuthorDateX} Sha={ShaX} Parents={ParentsX} Subject={SubjectX} Description={DescriptionX} Refs={RefsX}";
			}
		}

		private static ProbeResult Measure(RevisionSummaryUserControl control, Window window)
		{
			Dispatcher.UIThread.RunJobs();
			Avalonia.Headless.HeadlessWindowExtensions.CaptureRenderedFrame(window);
			Dispatcher.UIThread.RunJobs();

			// 相对控件的 X 坐标（Translate 换算到同一坐标系）
			double RelX(Avalonia.Visual visual, Avalonia.Visual ancestor)
			{
				var p1 = visual.TranslatePoint(new Point(0, 0), ancestor);
				return p1?.X ?? -1;
			}

			var root = (Avalonia.Visual)control;
			var r = new ProbeResult();
			r.AuthorNameX = RelX(control.AuthorTextBlock, root);
			r.AuthorDateX = RelX(control.AuthorDateTextBlock, root);
			r.ShaX = RelX(control.ShaTextBlock, root);
			r.ParentsX = RelX(control.ParentsContainer, root);
			r.SubjectX = RelX(control.SubjectTextBlock, root);
			r.DescriptionX = RelX(control.DescriptionTextBlock, root);
			r.RefsX = RelX(control.ReferencePanel, root);
			return r;
		}

		private static RevisionSummaryUserControl Setup(out Window window)
		{
			var control = new RevisionSummaryUserControl();
			control.AuthorTextBlock.Text = "Author Name";
			control.AuthorEmailTextBlock.Text = "author@example.com";
			control.AuthorDateTextBlock.Text = "2026-09-04 12:00:00 +0800";
			control.ShaTextBlock.Text = "0123456789abcdef0123456789abcdef01234567";
			control.SubjectTextBlock.Text = "Commit subject line";
			control.DescriptionTextBlock.Text = "Commit description body";
			control.ReferencesTextBlock.IsVisible = false;
			control.ReferencePanel.IsVisible = false;
			control.CommitterDetailsContainer.IsVisible = false;
			Avalonia.Controls.Grid.SetColumnSpan(control.AuthorDetailsContainer, 4);
			window = new Window { Width = 900, Height = 700, Content = control };
			window.Show();
			return control;
		}

		[Fact]
		public void Probe_Alignment_CommitterHidden()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				var control = Setup(out var window);
				var r = Measure(control, window);
				window.Close();

				// 输出探针数据，便于诊断
				Console.WriteLine("[Probe_CommitterHidden] " + r);

				// 断言：SHA/Parents/Subject/Description 与作者姓名对齐
				Assert.Equal(r.AuthorNameX, r.ShaX, 1);
				Assert.Equal(r.AuthorNameX, r.ParentsX, 1);
				Assert.Equal(r.AuthorNameX, r.SubjectX, 1);
				Assert.Equal(r.AuthorNameX, r.DescriptionX, 1);
				Assert.Equal(r.AuthorNameX, r.AuthorDateX, 1);
			});
		}

		[Fact]
		public void Probe_Alignment_CommitterVisible()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				var control = Setup(out var window);
				// 模拟 committer 可见：作者区只占前 2 列
				Avalonia.Controls.Grid.SetColumnSpan(control.AuthorDetailsContainer, 2);
				control.CommitterDetailsContainer.IsVisible = true;
				control.CommitterTextBlock.Text = "Committer Name";
				var r = Measure(control, window);
				window.Close();

				Console.WriteLine("[Probe_CommitterVisible] " + r);

				Assert.Equal(r.AuthorNameX, r.ShaX, 1);
				Assert.Equal(r.AuthorNameX, r.ParentsX, 1);
				Assert.Equal(r.AuthorNameX, r.SubjectX, 1);
				Assert.Equal(r.AuthorNameX, r.DescriptionX, 1);
			});
		}

		[Fact]
		public void Probe_Alignment_RefsVisible()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				var control = Setup(out var window);
				// 模拟带 refs 的提交（tag/branch）
				control.ReferencesTextBlock.IsVisible = true;
				control.ReferencePanel.IsVisible = true;
				var r = Measure(control, window);
				window.Close();

				Console.WriteLine("[Probe_RefsVisible] " + r);

				Assert.Equal(r.AuthorNameX, r.ShaX, 1);
				Assert.Equal(r.AuthorNameX, r.SubjectX, 1);
				Assert.Equal(r.AuthorNameX, r.RefsX, 1);
			});
		}

		[Fact]
		public void Probe_Alignment_WithParents()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				var control = Setup(out var window);
				// 模拟 merge 提交：2 个父提交按钮（用普通 Button 代替 AdvancedTooltipButton，
				// 它需要 RepositoryUserControl，此处只测容器布局位置）
				var btn1 = new global::Avalonia.Controls.Button { Content = "abc1234", FontSize = 12, Padding = new Thickness(0), Margin = new Thickness(0, 0, 3, 0) };
				var btn2 = new global::Avalonia.Controls.Button { Content = "def5678", FontSize = 12, Padding = new Thickness(0), Margin = new Thickness(0, 0, 3, 0) };
				control.ParentsContainer.Children.Add(btn1);
				control.ParentsContainer.Children.Add(btn2);
				var r = Measure(control, window);
				double btnX = -1;
				if (btn1.TranslatePoint(new Point(0, 0), (Avalonia.Visual)control) is { } bp)
				{
					btnX = bp.X;
				}
				window.Close();

				Console.WriteLine("[Probe_WithParents] " + r + " FirstParentButton=" + btnX);

				Assert.Equal(r.AuthorNameX, r.ShaX, 1);
				Assert.Equal(r.AuthorNameX, r.ParentsX, 1);
				// 父提交按钮文本相对按钮有内边距（模板），只验证按钮容器与作者对齐
			});
		}

		[Fact]
		public void Probe_Alignment_ToggleCommitterBackAndForth()
		{
			HeadlessAppBootstrap.EnsureStarted();
			HeadlessAppBootstrap.Run(delegate
			{
				var control = Setup(out var window);
				// 模拟连续切换不同提交：先 committer 可见，再隐藏
				Avalonia.Controls.Grid.SetColumnSpan(control.AuthorDetailsContainer, 2);
				control.CommitterDetailsContainer.IsVisible = true;
				Dispatcher.UIThread.RunJobs();

				Avalonia.Controls.Grid.SetColumnSpan(control.AuthorDetailsContainer, 4);
				control.CommitterDetailsContainer.IsVisible = false;
				var r = Measure(control, window);
				window.Close();

				Console.WriteLine("[Probe_ToggleBackAndForth] " + r);

				Assert.Equal(r.AuthorNameX, r.ShaX, 1);
				Assert.Equal(r.AuthorNameX, r.ParentsX, 1);
				Assert.Equal(r.AuthorNameX, r.SubjectX, 1);
				Assert.Equal(r.AuthorNameX, r.DescriptionX, 1);
			});
		}
	}
}
