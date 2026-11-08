using System;
using System.IO;
using System.Linq;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Diff;
using ForkPlus.Git.Diff.Presentation;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.Controls.Editor.Diff.LineBlame;
using ForkPlus.UI.UserControls;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class LineBlamePopupTests
	{
		[Fact]
		public void Popup_RendersUncommittedCommittedAndHistoryStates()
		{
			Exception crash = HeadlessAppBootstrap.Run(delegate
			{
				try
				{
					LineBlamePopupUserControl popup = new LineBlamePopupUserControl();
					popup.ShowLoading();
					Assert.True(popup.BusyPanel.IsVisible);
					Assert.False(popup.ContentPanel.IsVisible);

					popup.ShowData(new LineBlamePopupData
					{
						Primary = new LineBlameLineItem
						{
							IsUncommitted = true,
							UncommittedKind = LineBlameUncommittedKind.Untracked,
							Line = 1,
							Path = "new.txt"
						},
						FilePath = "new.txt"
					});
					Assert.True(popup.UncommittedPanel.IsVisible);
					Assert.False(popup.PrimaryPanel.IsVisible);
					Assert.False(popup.HistoryBorder.IsVisible);
					Assert.False(string.IsNullOrEmpty(popup.UncommittedTitleTextBlock.Text));

					Revision revision = new Revision(ParseSha("cccccccccccccccccccccccccccccccccccccccc"),
						new RevisionHeader(new UserIdentity("Test", "test@example.com"), DateTime.Now.AddDays(-1), "subject line", hasBody: false));
					popup.ShowData(new LineBlamePopupData
					{
						Primary = new LineBlameLineItem
						{
							Revision = revision,
							Line = 12,
							Path = "a.txt",
							RevisionSpec = revision.Sha.ToString()
						},
						HasHistory = true,
						HistoryPath = "a.txt",
						HistoryLine = 12,
						HistoryStartSha = revision.Sha,
						FilePath = "a.txt"
					});
					Assert.True(popup.PrimaryPanel.IsVisible);
					Assert.True(popup.HistoryBorder.IsVisible);
					Assert.False(popup.OtherBorder.IsVisible);
					Assert.Equal("Test", popup.PrimaryAuthorTextBlock.Text);
					TextBlock shaText = popup.PrimaryShaButton.Content as TextBlock;
					Assert.NotNull(shaText);
					Assert.Equal(revision.Sha.ToAbbreviatedString(), shaText.Text);
					Assert.NotNull(shaText.TextDecorations);
					Assert.Equal("subject line", popup.PrimarySubjectTextBlock.Text);

					popup.ShowHistory(new RevisionWithFiles[0]);
					Assert.True(popup.HistoryEmptyTextBlock.IsVisible);

					// 历史行内容是多列 Grid：必须由 ContentPresenter 渲染，而不是被
					// TextButtonStyle 字符串化成 "Avalonia.Controls.Grid"。
					Revision historyRevision = new Revision(ParseSha("dddddddddddddddddddddddddddddddddddddddd"),
						new RevisionHeader(new UserIdentity("History", "history@example.com"), DateTime.Now.AddDays(-2), "history subject", hasBody: false));
					popup.ShowHistory(new[] { new RevisionWithFiles(historyRevision, new ChangedFile[0]) });
					Assert.False(popup.HistoryEmptyTextBlock.IsVisible);
					Assert.True(popup.ViewFullHistoryButton.IsVisible);
					Assert.Equal(1, popup.HistoryItemsPanel.Children.Count);
					Button row = popup.HistoryItemsPanel.Children[0] as Button;
					Assert.NotNull(row);
					Grid rowGrid = row.Content as Grid;
					Assert.NotNull(rowGrid);
					Assert.Equal(3, rowGrid.Children.Count);
					return (Exception)null;
				}
				catch (Exception ex)
				{
					return ex;
				}
			});
			Assert.True(crash == null, "行号 blame 浮窗渲染异常：\n" + crash);
		}

		[Fact]
		public void Margin_HitTest_MapsSplitSidesToOldAndNewLineNumbers()
		{
			Exception crash = HeadlessAppBootstrap.Run(delegate
			{
				try
				{
					Diff diff = new Diff("a.txt", "a.txt", null, null, "111", "222",
						new[] { "context\n", "deleted\n", "added\n", "context2\n" },
						new[]
						{
							new Chunk(1, 3, 1, 3, null, new[]
							{
								new SubChunk(new Range(0, 1), new Range(1, 2), new Range(2, 3), new Range(3, 4), NoNewLineAtEndOfFile.None)
							})
						},
						null, Diff.FileType.Text, false);
					DiffCodeEditor editor = new DiffCodeEditor(DiffViewMode.Split);
					editor.VisualPatch = VisualPatch.CreateVisualPatch(diff, entireFile: false, DiffLocation.Unstaged);
					Window window = new Window { Width = 700, Height = 240, Content = editor };
					window.Show();
					Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

					DiffLineNumberMargin margin = editor.TextArea.LeftMargins.OfType<DiffLineNumberMargin>().First();
					var textView = editor.TextArea.TextView;
					// VisualLines[0] = "@@" 头；[1] 上下文；[2] 删除行（From=2）；[3] 新增行（To=2）
					Assert.Equal(6, textView.VisualLines.Count);
					double deletedY = textView.VisualLines[2].VisualTop - textView.ScrollOffset.Y + 1.0;
					LineBlameHitInfo deletedHit = margin.HitTestLineBlame(new Point(margin.Bounds.Width / 4.0, deletedY));
					Assert.NotNull(deletedHit);
					Assert.True(deletedHit.OldSide);
					Assert.Equal(2, deletedHit.OldLine);
					Assert.Equal(0, deletedHit.NewLine);

					double addedY = textView.VisualLines[3].VisualTop - textView.ScrollOffset.Y + 1.0;
					LineBlameHitInfo addedHit = margin.HitTestLineBlame(new Point(margin.Bounds.Width / 4.0, addedY));
					Assert.NotNull(addedHit);
					Assert.False(addedHit.OldSide, "新增行在旧侧没有行号，应回退到新侧");
					Assert.Equal(2, addedHit.NewLine);
					Assert.Equal(0, addedHit.OldLine);
					window.Close();
					return (Exception)null;
				}
				catch (Exception ex)
				{
					return ex;
				}
			});
			Assert.True(crash == null, "行号 margin 命中测试异常：\n" + crash);
		}

		[Fact]
		public void Hover_LineNumberMargin_OpensPopup_ForUncommittedLine()
		{
			string repo = TestRepoFactory.CreateWorkingDir();
			try
			{
				GitModule module = new GitModule(repo, Path.Combine(repo, ".git"), null, null);
				GitCommandResult<ChangedFilesCollection> status =
					new GetChangedFilesGitCommand().Execute(module);
				Assert.True(status.Succeeded, status.Error?.ToString());
				ChangedFile file = status.Result.ChangedFiles.First((ChangedFile f) => f.Path == "a.txt" && !f.Staged);
				GitCommandResult<DiffContent> diff = new GetWorkingDirectoryFileChangesGitCommand().Execute(
					module, file, null, 3, 4, false, false, false, resolvedConflict: false);
				Assert.True(diff.Succeeded, diff.Error?.ToString());

				Exception crash = HeadlessAppBootstrap.Run(delegate
				{
					try
					{
						RepositoryUserControl repoControl = new RepositoryUserControl();
						typeof(RepositoryUserControl).GetProperty("GitModule")
							.SetValue(repoControl, module);
						FileDiffControl control = new FileDiffControl
						{
							RepositoryUserControl = repoControl,
							Target = FileDiffControlTarget.Commit
						};
						global::ForkPlus.UI.CustomWindow window = new global::ForkPlus.UI.CustomWindow { Width = 1000, Height = 600, Content = control };
						window.Show();
						Dispatcher.UIThread.RunJobs();
						control.Content = diff;

						DiffCodeEditor editor = null;
						for (int i = 0; i < 40 && editor == null; i++)
						{
							Thread.Sleep(50);
							Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
							editor = window.GetVisualDescendants().OfType<DiffCodeEditor>().FirstOrDefault();
						}
						Assert.NotNull(editor);
						DiffLineNumberMargin margin = editor.TextArea.LeftMargins.OfType<DiffLineNumberMargin>().FirstOrDefault();
						Assert.NotNull(margin);

						// 工作区 a.txt 在原 3 行后追加了 2 行——按 VisualPatch 的 Added 行定位文档行。
						VisualLine addedVisualLine = editor.VisualPatch.VisualDiff.VisualChunks
							.SelectMany((VisualChunk chunk) => chunk.VisualLines)
							.FirstOrDefault((VisualLine line) => line.Type == LineType.Added);
						Assert.NotNull(addedVisualLine);
						int addedDocLine = addedVisualLine.LineNumber + 1;
						var textView = editor.TextArea.TextView;
						var visualLine = textView.GetVisualLine(addedDocLine)
							?? textView.VisualLines.FirstOrDefault(v => v.FirstDocumentLine.LineNumber == addedDocLine);
						Assert.NotNull(visualLine);
						Point pointInMargin = new Point(
							margin.Bounds.Width / 2.0,
							visualLine.VisualTop - textView.ScrollOffset.Y + Math.Min(4.0, visualLine.Height / 2.0));
						Point? translatedPoint = margin.TranslatePoint(pointInMargin, window);
						Assert.NotNull(translatedPoint);
						Point pointInWindow = translatedPoint.Value;
						HeadlessWindowExtensions.MouseMove(window, pointInWindow, global::Avalonia.Input.RawInputModifiers.None);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						Assert.True(margin.IsPointerOver, "鼠标未落在行号 margin 上：" + pointInWindow + " marginBounds=" + margin.Bounds);
						Assert.True(margin.HitTestLineBlame(pointInMargin) != null, "行号命中测试返回 null：" + pointInMargin);
						object controller = typeof(DiffLineNumberMargin)
							.GetField("_lineBlameHoverController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
							.GetValue(margin);
						Assert.NotNull(controller);
						DispatcherTimer hoverTimer = (DispatcherTimer)controller.GetType()
							.GetField("_hoverTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
							.GetValue(controller);
						Assert.True(hoverTimer.IsEnabled, "悬停后 400ms 定时器应启动");
						// headless 下 DispatcherTimer 不随 RunJobs 推进（无真实消息循环），直接触发
						// tick 验证“定时器到点 → 打开浮窗 → blame 装配”的完整链路。
						System.Reflection.MethodInfo tick = controller.GetType().GetMethod(
							"HoverTimer_Tick",
							System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
						tick.Invoke(controller, new object[] { hoverTimer, EventArgs.Empty });
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

						var popup = (global::Avalonia.Controls.Primitives.Popup)controller.GetType()
							.GetField("_popup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
							.GetValue(controller);
						Assert.NotNull(popup);
						Assert.True(popup.IsOpen, "悬停行号 500ms 后浮窗应已打开");
						LineBlamePopupUserControl content = (LineBlamePopupUserControl)controller.GetType()
							.GetField("_content", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
							.GetValue(controller);
						Assert.NotNull(content);
						// 模拟鼠标离开行号区：不能立即启动关闭（否则用户来不及把鼠标移到浮窗上）。
						System.Reflection.MethodInfo onPointerExited = controller.GetType().GetMethod(
							"OnPointerExited", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
						onPointerExited.Invoke(controller, null);
						DispatcherTimer closeTimer = (DispatcherTimer)controller.GetType()
							.GetField("_closeTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
							.GetValue(controller);
						Assert.False(closeTimer.IsEnabled, "离开行号区不应立即启动关闭定时器");
						Thread.Sleep(600);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						Assert.True(popup.IsOpen, "鼠标离开行号区后浮窗不应立即消失");

						for (int i = 0; i < 20 && content.BusyPanel.IsVisible; i++)
						{
							Thread.Sleep(100);
							Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						}
						Assert.False(content.BusyPanel.IsVisible, "blame 查询应在超时前完成");
						Assert.True(content.UncommittedPanel.IsVisible, "工作区新增行应显示未提交占位卡片");
						Assert.False(content.PrimaryPanel.IsVisible);
						popup.IsOpen = false;
						window.Close();
						return (Exception)null;
					}
					catch (Exception ex)
					{
						return ex;
					}
				});
				Assert.True(crash == null, "行号悬停浮窗端到端异常：\n" + crash);
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		// 回归（2026-11-08，v5.4.3）：点“变更历史 ▸”后卡片从加载态长成历史列表，
		// 尺寸/位置变化会让指针暂时离开浮窗；旧逻辑里 Button 把 PointerPressed 标记
		// handled，内容级订阅收不到，_popupPinned 没置位，随后一次 PointerExited /
		// 行号掠过就关窗——用户看到的就是“面板刷新后鼠标一动就消失”。
		// 现在展开历史即钉住浮窗，且关闭定时器到点也会被钉住状态拦下。
		[Fact]
		public void Hover_ExpandHistory_PinsPopup_AndSurvivesLayoutRefresh()
		{
			string repo = TestRepoFactory.CreateWorkingDir();
			try
			{
				GitModule module = new GitModule(repo, Path.Combine(repo, ".git"), null, null);
				GitCommandResult<ChangedFilesCollection> status =
					new GetChangedFilesGitCommand().Execute(module);
				Assert.True(status.Succeeded, status.Error?.ToString());
				ChangedFile file = status.Result.ChangedFiles.First((ChangedFile f) => f.Path == "a.txt" && !f.Staged);
				GitCommandResult<DiffContent> diff = new GetWorkingDirectoryFileChangesGitCommand().Execute(
					module, file, null, 3, 4, false, false, false, resolvedConflict: false);
				Assert.True(diff.Succeeded, diff.Error?.ToString());

				Exception crash = HeadlessAppBootstrap.Run(delegate
				{
					try
					{
						RepositoryUserControl repoControl = new RepositoryUserControl();
						typeof(RepositoryUserControl).GetProperty("GitModule").SetValue(repoControl, module);
						FileDiffControl control = new FileDiffControl
						{
							RepositoryUserControl = repoControl,
							Target = FileDiffControlTarget.Commit
						};
						global::ForkPlus.UI.CustomWindow window = new global::ForkPlus.UI.CustomWindow { Width = 1000, Height = 600, Content = control };
						window.Show();
						Dispatcher.UIThread.RunJobs();
						control.Content = diff;

						DiffCodeEditor editor = null;
						for (int i = 0; i < 40 && editor == null; i++)
						{
							Thread.Sleep(50);
							Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
							editor = window.GetVisualDescendants().OfType<DiffCodeEditor>().FirstOrDefault();
						}
						Assert.NotNull(editor);
						DiffLineNumberMargin margin = editor.TextArea.LeftMargins.OfType<DiffLineNumberMargin>().FirstOrDefault();
						Assert.NotNull(margin);

						// 悬停一条上下文行（已提交）：卡片带“变更历史”。
						VisualLine contextLine = editor.VisualPatch.VisualDiff.VisualChunks
							.SelectMany((VisualChunk chunk) => chunk.VisualLines)
							.FirstOrDefault((VisualLine line) => line.Type == LineType.Context && line.LineNumber > 0);
						Assert.NotNull(contextLine);
						int docLine = contextLine.LineNumber + 1;
						var textView = editor.TextArea.TextView;
						var visualLine = textView.GetVisualLine(docLine)
							?? textView.VisualLines.FirstOrDefault(v => v.FirstDocumentLine.LineNumber == docLine);
						Assert.NotNull(visualLine);
						Point pointInMargin = new Point(
							margin.Bounds.Width / 2.0,
							visualLine.VisualTop - textView.ScrollOffset.Y + Math.Min(4.0, visualLine.Height / 2.0));
						Point? translatedPoint = margin.TranslatePoint(pointInMargin, window);
						Assert.NotNull(translatedPoint);
						HeadlessWindowExtensions.MouseMove(window, translatedPoint.Value, global::Avalonia.Input.RawInputModifiers.None);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

						object controller = typeof(DiffLineNumberMargin)
							.GetField("_lineBlameHoverController", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
							.GetValue(margin);
						Assert.NotNull(controller);
						DispatcherTimer hoverTimer = (DispatcherTimer)controller.GetType()
							.GetField("_hoverTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
							.GetValue(controller);
						System.Reflection.MethodInfo tick = controller.GetType().GetMethod(
							"HoverTimer_Tick",
							System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
						tick.Invoke(controller, new object[] { hoverTimer, EventArgs.Empty });
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);

						global::Avalonia.Controls.Primitives.Popup popup = (global::Avalonia.Controls.Primitives.Popup)controller.GetType()
							.GetField("_popup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
							.GetValue(controller);
						Assert.NotNull(popup);
						Assert.True(popup.IsOpen);
						LineBlamePopupUserControl content = (LineBlamePopupUserControl)controller.GetType()
							.GetField("_content", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
							.GetValue(controller);
						Assert.NotNull(content);
						for (int i = 0; i < 20 && content.BusyPanel.IsVisible; i++)
						{
							Thread.Sleep(100);
							Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						}
						Assert.False(content.BusyPanel.IsVisible, "blame 查询应在超时前完成");
						Assert.True(content.PrimaryPanel.IsVisible, "上下文行应显示已提交卡片");
						Assert.True(content.HistoryBorder.IsVisible, "已提交行应有历史入口");

						System.Reflection.FieldInfo pinnedField = controller.GetType()
							.GetField("_popupPinned", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
						Assert.False((bool)pinnedField.GetValue(controller), "刚打开的浮窗不应处于钉住状态");

						// 点击“变更历史 ▸”：Button 会把 PointerPressed 标记 handled，但
						// HistoryExpanded 是业务层事件，必须无条件钉住浮窗。
						content.HistoryToggleButton.RaiseEvent(new global::Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						Assert.True((bool)pinnedField.GetValue(controller), "展开历史后浮窗必须钉住");

						System.Reflection.MethodInfo closeTick = controller.GetType().GetMethod(
							"CloseTimer_Tick",
							System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
						closeTick.Invoke(controller, new object[] { null, EventArgs.Empty });
						Assert.True(popup.IsOpen, "钉住状态下关闭定时器不应关窗");

						// 模拟历史列表刷新（尺寸变化）后再触发一次关闭定时器。
						content.ShowHistory(new RevisionWithFiles[0]);
						Dispatcher.UIThread.RunJobs(DispatcherPriority.Background);
						closeTick.Invoke(controller, new object[] { null, EventArgs.Empty });
						Assert.True(popup.IsOpen, "历史列表刷新后浮窗不应消失");

						popup.IsOpen = false;
						window.Close();
						return (Exception)null;
					}
					catch (Exception ex)
					{
						return ex;
					}
				});
				Assert.True(crash == null, "展开历史后的浮窗稳定性异常：\n" + crash);
			}
			finally
			{
				TestRepoFactory.Cleanup(repo);
			}
		}

		private static Sha ParseSha(string value)
		{
			Assert.True(Sha.TryParse(value, out Sha sha));
			return sha;
		}
	}
}
