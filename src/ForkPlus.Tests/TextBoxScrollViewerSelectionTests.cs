// 回归测试（2026-09-10，v4.0.6，"重命名贮藏等弹窗长文本鼠标拖不中全部文字"）：
// 根因（三层闭环）：
//   1) WPF 原版 8 个 TextBox 系模板的内容宿主都是 ScrollViewer PART_ContentHost
//      （HSBV/VSBV=Hidden：可滚动但不显示滚动条）；
//   2) 迁移时被统一换成裸 TextPresenter 直接放进 Border/Grid——presenter 被裁剪为
//      可视宽度，而 Avalonia TextBox.OnPointerMoved 把拖选坐标钳制到
//      presenter.Bounds（= 可视宽度）→ 可视区外的文字永远选不中；
//   3) TextPresenter 移动光标时调 BringIntoView(caretBounds)（冒泡
//      RequestBringIntoView），没有 ScrollViewer 承接 → 光标越界也不自动滚动。
// 修复：全部模板补回 ScrollViewer（Avalonia 部件契约名 PART_ScrollViewer，
// TextBox.OnApplyTemplate 按此名 Find；对齐官方 Fluent 主题）。
// 本测试守卫三层：1) 模板契约（8 变体都有 PART_ScrollViewer 承载 presenter）；
// 2) 几何根因（presenter 在 ScrollViewer 内以内容全宽测量，而非可视宽度）；
// 3) 行为端到端（指针按下后拖出框右缘 → 全部选中 + ScrollViewer 自动滚动）。
using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.UI.Controls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class TextBoxScrollViewerSelectionTests
	{
		// ============ 1) 模板契约：8 个 TextBox 系变体 ============

		[Fact]
		public void TextBoxFamily_TemplatesHostPresenterInsideScrollViewer()
		{
			HeadlessAppBootstrap.EnsureStarted();
			string[] failures = HeadlessAppBootstrap.Run(delegate
			{
				var found = new System.Collections.Generic.List<string>();

				void Check(TextBox tb, string name)
				{
					var window = new Window { Width = 360, Height = 100, Content = tb };
					window.Show();
					Dispatcher.UIThread.RunJobs();
					try
					{
						TextPresenter presenter = tb.GetVisualDescendants()
							.OfType<TextPresenter>().FirstOrDefault((TextPresenter p) => p.Name == "PART_TextPresenter");
						ScrollViewer scroller = tb.GetVisualDescendants()
							.OfType<ScrollViewer>().FirstOrDefault((ScrollViewer s) => s.Name == "PART_ScrollViewer");
						if (presenter == null)
						{
							found.Add(name + ": 缺 PART_TextPresenter");
							return;
						}
						if (scroller == null)
						{
							// WPF 原版 PART_ContentHost（ScrollViewer）迁移丢失即此形态
							found.Add(name + ": 缺 PART_ScrollViewer（WPF 原版内容宿主迁移回归）");
							return;
						}
						if (!presenter.GetVisualAncestors().OfType<ScrollViewer>().Contains(scroller))
						{
							found.Add(name + ": PART_TextPresenter 不在 PART_ScrollViewer 内（拖选坐标钳制回可视宽度）");
							return;
						}
						// 仓库级 ScrollViewer 主题默认 Background=ScrollViewerBackground（不透明主题色），
						// 模板必须显式 Transparent，否则盖掉 TextBox 自身背景
						if (scroller.Background is ISolidColorBrush bg && bg.Color.A != 0)
						{
							found.Add(name + ": PART_ScrollViewer 背景不透明（" + bg.Color + "），会盖掉文本框底色");
						}
					}
					finally
					{
						window.Close();
					}
				}

				Check(new TextBox { Text = "t" }, "TextBox");
				Check(new PlaceholderTextBox { Text = "t", Placeholder = "ph" }, "PlaceholderTextBox（重命名贮藏消息框同款）");
				Check(new AutoCompleteTextBox { Text = "t" }, "AutoCompleteTextBox");
				Check(new CommitDescriptionTextBox { Text = "t" }, "CommitDescriptionTextBox（多行提交描述）");
				Check(new FilterTextBox { Text = "t" }, "FilterTextBox");

				var commitSubject = new PlaceholderTextBox { Text = "t" };
				commitSubject.Theme = (ControlTheme)Avalonia.Application.Current.FindResource("CommitPlaceholderTextBox");
				Check(commitSubject, "CommitPlaceholderTextBox 主题（提交主题行/改写提交）");

				var searchPanel = new PlaceholderTextBox { Text = "t" };
				searchPanel.Theme = (ControlTheme)Avalonia.Application.Current.FindResource("SearchPanelPlaceholderTextBox");
				Check(searchPanel, "SearchPanelPlaceholderTextBox 主题（修订/统计搜索）");

				var comboInner = new TextBox { Text = "t" };
				comboInner.Theme = (ControlTheme)Avalonia.Application.Current.FindResource("ComboBoxEditableTextBox");
				Check(comboInner, "ComboBoxEditableTextBox 主题（编辑型下拉内嵌框）");

				return found.ToArray();
			});
			Assert.True(failures.Length == 0,
				"TextBox 系模板 PART_ScrollViewer 契约回归：" + Environment.NewLine + string.Join(Environment.NewLine, failures));
		}

		// ============ 2) 几何根因 + 3) 行为端到端 ============

		[Fact]
		public void LongText_DragBeyondRightEdge_SelectsAll()
		{
			HeadlessAppBootstrap.EnsureStarted();
			(bool geometryOk, bool captured, bool selectionOk, bool scrolled, string diag) = HeadlessAppBootstrap.Run(delegate
			{
				// 120 个宽字符（~800px）远超 200px 框宽——重命名贮藏长消息的形态
				string longText = new string('A', 120) + "-tail";
				var tb = new PlaceholderTextBox { Text = longText, Placeholder = "ph", Width = 200, Height = 30 };
				var window = new Window { Width = 400, Height = 120, Content = tb };
				window.Show();
				Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

				TextPresenter presenter = tb.GetVisualDescendants()
					.OfType<TextPresenter>().First((TextPresenter p) => p.Name == "PART_TextPresenter");
				ScrollViewer scroller = tb.GetVisualDescendants()
					.OfType<ScrollViewer>().First((ScrollViewer s) => s.Name == "PART_ScrollViewer");

				// 几何根因守卫：presenter 必须以内容全宽测量（修复前≈可视宽度 200）
				bool geometry = presenter.Bounds.Width > tb.Bounds.Width + 50;

				// 行为守卫：真实事件管线（TextBox.OnPointerPressed 捕获 presenter →
				// OnPointerMoved 钳制到 presenter.Bounds 扩选）。
				// 按下点取 presenter 内最左（文字起点），拖动点取 presenter 最右之外
				//（框外——指针已被 presenter 捕获，坐标仍可路由换算；由
				// OnPointerMoved 钳制到 presenter.Bounds-1 → 光标落文本末尾）
				var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, true);
				Point pressInWindow = presenter.TranslatePoint(new Point(2, 5), window) ?? new Point(10, 10);
				Point endInWindow = presenter.TranslatePoint(new Point(presenter.Bounds.Width + 50, 5), window) ?? new Point(300, 10);

				tb.RaiseEvent(new PointerPressedEventArgs(tb, pointer, window, pressInWindow,
					(ulong)Environment.TickCount64,
					new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed),
					KeyModifiers.None));
				Dispatcher.UIThread.RunJobs();
				bool isCaptured = ReferenceEquals(pointer.Captured, presenter);

				tb.RaiseEvent(new PointerEventArgs(InputElement.PointerMovedEvent, tb, pointer, window, endInWindow,
					(ulong)Environment.TickCount64,
					new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.Other),
					KeyModifiers.None));
				Dispatcher.UIThread.RunJobs();

				// 修复前：拖选坐标被钳制到 presenter.Bounds（=可视宽度 200px），
				// SelectionEnd 卡在最后一个"可见"字符（~28），永远到不了文本末尾
				bool selection = tb.SelectionStart == 0 && tb.SelectionEnd == longText.Length;
				bool hasScrolled = UiClick.WaitFor(delegate { return scroller.Offset.X > 0; }, 2000);
				string diagLocal = "presenterW=" + presenter.Bounds.Width.ToString("F0")
					+ " tbW=" + tb.Bounds.Width.ToString("F0")
					+ " sel=[" + tb.SelectionStart + "," + tb.SelectionEnd + ") len=" + longText.Length
					+ " scrollX=" + scroller.Offset.X.ToString("F1");
				window.Close();
				return (geometry, isCaptured, selection, hasScrolled, diagLocal);
			});

			Assert.True(geometryOk, "presenter 应以内容全宽测量（ScrollViewer 内按内容宽度排布），实测未超过可视宽度——模板回归：" + diag);
			Assert.True(captured, "按下后指针应被 TextPresenter 捕获（拖选管线前提）：" + diag);
			Assert.True(selectionOk,
				"拖出框右缘应选中全部文字（修复前钳制在可视宽度、只能选到最后一个可见字符）：" + diag);
			Assert.True(scrolled, "选区扩到框外后 ScrollViewer 应自动滚动（TextPresenter.BringIntoView 接线）：" + diag);
		}
	}
}
