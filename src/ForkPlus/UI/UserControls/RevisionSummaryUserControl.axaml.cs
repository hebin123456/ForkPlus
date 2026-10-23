using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Markup;
using Avalonia.Media;
using ForkPlus.Accounts.AiServices;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Git.Interaction;
using ForkPlus.Jobs;
using ForkPlus.Utils.Http;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.CustomCommands;
using ForkPlus.UI.Dialogs;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls
{
	public partial class RevisionSummaryUserControl : UserControl
	{
		// Migration note：TextButtonStyle 资源是 ControlTheme；IStyle 由 StyleCompat.SetStyle 消费（挂 Theme）。
	private static global::Avalonia.Styling.IStyle ParentButtonStyle => Application.Current?.TryFindResource("TextButtonStyle") as global::Avalonia.Styling.IStyle;

		[Null]
		private RevisionSearchQuery _searchQuery;

		private BugtrackerLinkDefinition[] _bugtrackers;

		private UserColors _userColors;

		private Sha? _sha;

		public RevisionDetailsUserControl RevisionDetailsUserControl { get; set; }

		public RepositoryUserControl RepositoryUserControl => RevisionDetailsUserControl.RepositoryUserControl;

		public RevisionSummaryUserControl()
		{
			InitializeComponent();
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			DescriptionTextBlock.FontFamily = FontConstants.MonospaceFontFamily;
		}

		public void Refresh(Sha sha, BugtrackerLinkDefinition[] bugtrackers, UserColors userColors)
		{
			_sha = sha;
			_bugtrackers = bugtrackers;
			_userColors = userColors;
			// AI Explain 按钮：仅在 AI 配置完毕时显示（v3.9.0：统一用 AiActionButton.RefreshVisibility）
		AiExplainCommitButton.RefreshVisibility();
		global::Avalonia.Controls.ToolTip.SetTip(AiExplainCommitButton,Preferences.PreferencesLocalization.Translate("Use AI to explain this commit", ForkPlusSettings.Default.UiLanguage));
			FullRevisionDetails fullRevisionDetails = RevisionDetailsUserControl.FullRevisionDetails;
			RevisionDetails revisionDetails = fullRevisionDetails.RevisionDetails;
			AuthorAvatarImage.UserIdentity = revisionDetails.Author;
			AuthorTextBlock.Text = revisionDetails.Author.Name;
			AuthorEmailTextBlock.Text = revisionDetails.Author.Email;
			AuthorDateTextBlock.Text = revisionDetails.AuthorDate.ToString(Consts.FullDateTimeFormat);
			AuthorColorsToggleButton.Show();
			if (IsCommitterSectionVisible(revisionDetails))
			{
				Grid.SetColumnSpan(AuthorDetailsContainer, 2);
				CommitterDetailsContainer.Show();
				CommitterAvatarImage.UserIdentity = revisionDetails.Committer;
				CommitterTextBlock.Text = revisionDetails.Committer.Name;
				CommitterEmailTextBlock.Text = revisionDetails.Committer.Email;
				CommitterDateTextBlock.Text = revisionDetails.CommitterDate.ToString(Consts.FullDateTimeFormat);
				CommitterColorsToggleButton.Show();
			}
			else
			{
				Grid.SetColumnSpan(AuthorDetailsContainer, 4);
				CommitterDetailsContainer.Collapse();
			}
		UpdateReferences(revisionDetails.Sha);
		ShaTextBlock.Text = revisionDetails.Sha.ToString();
		UpdateSignatureBadge(revisionDetails);
			RevisionRemoteButtonsContainer.Children.Clear();
			RepositoryData repositoryData = RepositoryUserControl.RepositoryData;
			if (repositoryData != null && !revisionDetails.IsStash(repositoryData.Stashes))
			{
				Remote[] items = repositoryData.Remotes.Items;
				foreach (Remote remote in items)
				{
					string text = new RepositoryUrlBuilder(remote).CreateRevisionShaUrl(revisionDetails.Sha.ToString());
					if (text != null)
					{
						RevisionRemoteButtonsContainer.Children.Add(CreateRevisionRemoteButton(remote, sha, text));
					}
				}
			}
			ParentsContainer.Children.Clear();
			Sha[] parents = revisionDetails.Parents;
			foreach (Sha parent in parents)
			{
				ParentsContainer.Children.Add(CreateParentButton(RepositoryUserControl, parent, delegate
				{
					RepositoryUserControl.SelectRevision(parent);
				}));
			}
			revisionDetails.MessageParts(out var subject, out var description);
			SubjectTextBlock.Text = subject;
			DescriptionTextBlock.Text = description;
			DescriptionTextBlock.IsVisible = (string.IsNullOrEmpty(description) ? false : true);
			ApplySearch();
			int num = 100;
			RebuildDiffRows(fullRevisionDetails.ChangedFiles.Take(num));
			UpdateExpandAllButtonTitle();
			UpdateDiffListLimitText(fullRevisionDetails.ChangedFiles.Length, num);
			ReconcileColorsPopupWithRefreshedSummary();
		}

		// Refresh() 落地新详情后的弹窗对账：
		// (a) 仍开着的弹窗若来自上一行（邮箱已变），立即关闭——避免对旧邮箱选色；
		// (b) 延迟开窗（详情过期期间用户已点箭头）在数据就绪后自动补开。
		private void ReconcileColorsPopupWithRefreshedSummary()
		{
			ToggleButton openButton = _openColorsToggleButton;
			if (openButton != null && _openColorsPopupEmail != null)
			{
				string currentEmail = ((openButton == AuthorColorsToggleButton) ? AuthorEmailTextBlock.Text : CommitterEmailTextBlock.Text);
				if (_openColorsPopupEmail != currentEmail)
				{
					// IsOpen←IsChecked 绑定：置 false 即关窗，Closed 里完成清理
					openButton.IsChecked = false;
				}
			}
			if (_pendingColorsToggleButton != null && !IsColorsSummaryStale())
			{
				ToggleButton pendingButton = _pendingColorsToggleButton;
				if (pendingButton.IsChecked == true)
				{
					_pendingColorsToggleButton = null;
					OpenColorsPopup(pendingButton);
				}
			}
		}

		public void ApplyLocalization()
		{
			Preferences.PreferencesLocalization.ApplyCurrent(this);
			UpdateExpandAllButtonTitle();
			var fullRevisionDetails = RevisionDetailsUserControl?.FullRevisionDetails;
			if (fullRevisionDetails != null)
			{
				UpdateDiffListLimitText(fullRevisionDetails.ChangedFiles.Length, 100);
			}
		}

		private void UpdateDiffListLimitText(int changedFilesCount, int limit)
		{
			if (changedFilesCount >= limit)
			{
				DiffListLimitTextBlock.Text = Preferences.PreferencesLocalization.FormatCurrent("See full list of changed files ({0}) in the Changes tab", changedFilesCount);
				DiffListLimitTextBlock.Show();
			}
			else
			{
				DiffListLimitTextBlock.Collapse();
			}
		}

		private void RebuildDiffRows(IEnumerable<ChangedFile> changedFiles)
		{
			ClearDiffRows();
			DiffList.ItemsSource = changedFiles.Select((ChangedFile x) => new DiffEntry(RepositoryUserControl, x)).ToArray();
		}

		private void ClearDiffRows()
		{
			DiffList.ItemsSource = null;
		}

		public void HighlightSearchMatches([Null] RevisionSearchQuery searchQuery)
		{
			if (!RevisionSearchQuery.Equals(_searchQuery, searchQuery))
			{
				_searchQuery = searchQuery;
				ApplySearch();
			}
		}

		private void ExpandButton_Click(object sender, RoutedEventArgs e)
		{
			UpdateExpandAllButtonTitle(AllItemsExpanded());
			if (!(sender is Expander { DataContext: DiffEntry diffEntry } expander) || !expander.IsExpanded || diffEntry.Content != null)
			{
				return;
			}
			LoadDiffEntryContent(diffEntry);
		}

		private void ExpandAllButton_Click(object sender, RoutedEventArgs e)
		{
			bool expand = !AllItemsExpanded();
			foreach (DiffEntry diffEntry in DiffList.Items.OfType<DiffEntry>())
			{
				diffEntry.IsExpanded = expand;
				if (expand && diffEntry.Content == null)
				{
					LoadDiffEntryContent(diffEntry);
				}
			}
			UpdateExpandAllButtonTitle(expand);
		}

		private void LoadDiffEntryContent(DiffEntry diffEntry)
		{
			GitModule gitModule = RevisionDetailsUserControl.GitModule;
			var fullRevisionDetails = RevisionDetailsUserControl.FullRevisionDetails;
			if (gitModule == null || fullRevisionDetails == null || diffEntry == null || diffEntry.ChangedFile == null)
			{
				return;
			}
			Sha sha = fullRevisionDetails.RevisionDetails.Sha;
			diffEntry.Content = new GetRevisionFileChangesGitCommand().Execute(gitModule, new RevisionDiffTarget.Revision(sha), diffEntry.ChangedFile, ForkPlusSettings.Default.DiffContextSize, gitModule.Settings.TabWidth, ForkPlusSettings.Default.DiffIgnoreWhitespaces, showEntireFile: false);
		}

		private void AuthorColorsToggleButton_Changed(object sender, RoutedEventArgs args)
		{
			// Bug fix: IsCheckedChanged also fires when unchecked (after a light-dismiss close, IsChecked is manually reset to false),
			// only create the popup in the checked state, to avoid "no response on alternate clicks".
			if (sender == AuthorColorsToggleButton)
			{
				if (AuthorColorsToggleButton.IsChecked == true)
				{
					TryOpenColorsPopup(AuthorColorsToggleButton);
				}
				else if (_pendingColorsToggleButton == AuthorColorsToggleButton)
				{
					_pendingColorsToggleButton = null;
				}
			}
		}

		private void CommitterColorsToggleButton_Changed(object sender, RoutedEventArgs args)
		{
			if (sender == CommitterColorsToggleButton)
			{
				if (CommitterColorsToggleButton.IsChecked == true)
				{
					TryOpenColorsPopup(CommitterColorsToggleButton);
				}
				else if (_pendingColorsToggleButton == CommitterColorsToggleButton)
				{
					_pendingColorsToggleButton = null;
				}
			}
		}

		// Bug fix（选中 b 行却标记到 a 行）：切换选中行后，修订详情（含作者/提交者邮箱）由
		// 异步 job 加载（含提交签名验证，耗时可观）；job 完成前 summary 仍显示上一行的邮箱，
		// 此时打开颜色弹窗会捕获旧邮箱 → 选色标记到上一行。详情过期时延迟开窗
		//（按钮呈按下态作反馈），Refresh() 收到新数据后自动补开——弹窗永不捕获过期邮箱。
		private ToggleButton _pendingColorsToggleButton;

		private ToggleButton _openColorsToggleButton;

		private string _openColorsPopupEmail;

		private void TryOpenColorsPopup(ToggleButton parentButton)
		{
			if (IsColorsSummaryStale())
			{
				_pendingColorsToggleButton = parentButton;
				return;
			}
			OpenColorsPopup(parentButton);
		}

		private void OpenColorsPopup(ToggleButton parentButton)
		{
			string text = ((parentButton == AuthorColorsToggleButton) ? AuthorEmailTextBlock.Text : CommitterEmailTextBlock.Text);
			byte colorId = GetCurrentUserColorId(text);
			_openColorsToggleButton = parentButton;
			_openColorsPopupEmail = text;
			CreateColorsPopup(parentButton, text, colorId);
		}

		// 仅 MainWindow 模式存在驱动详情的"当前选中行"，可据此判断过期；
		// 其他模式（独立窗口 / AI 审查 / 交互式 rebase）显示固定修订，保持原行为。
		private bool IsColorsSummaryStale()
		{
			RevisionDetailsUserControl detailsControl = RevisionDetailsUserControl;
			if (detailsControl == null || detailsControl.Mode != RevisionDetailsUserControlMode.MainWindow)
			{
				return false;
			}
			DecoratedRevision selectedRevision = RepositoryUserControl?.Content?.RevisionListViewUserControl?.SelectedRevision;
			if (selectedRevision == null)
			{
				return false;
			}
			Sha? displayedSha = detailsControl.FullRevisionDetails?.RevisionDetails?.Sha;
			return displayedSha.HasValue && displayedSha.GetValueOrDefault() != selectedRevision.Sha;
		}

		// Bug 修复（取消颜色后弹窗仍显示旧选中态）：_userColors 只在 SummaryUserControl.Refresh
		// 时更新，而 UserColors-only 刷新不会重跑详情面板 → 取消/修改颜色后缓存过期，
		// 重开弹窗仍显示旧选中态。改为打开时从 RepositoryUserControl.RepositoryData 现取
		//（刷新完成后即为最新），_userColors 仅作 RepositoryData 缺失时的兜底。
		private byte GetCurrentUserColorId(string email)
		{
			UserColors userColors = RepositoryUserControl?.RepositoryData?.UserColors;
			if (userColors != null)
			{
				return userColors.GetColorId(email);
			}
			return _userColors.GetColorId(email);
		}

		private static Button CreateRevisionRemoteButton(Remote remote, Sha sha, string revisionShaUrl)
		{
			Image content = new Image
			{
				Height = 14.0,
				Width = 14.0,
				Source = remote.RemoteType.Icon()
			};
			Button button = new Button();
			button.Height = 16.0;
			button.Width = 16.0;
			button.Margin = new Thickness(4.0, 0.0, 0.0, 0.0);
			button.Padding = new Thickness(0.0);
			button.IsTabStop = false;
			button.BorderThickness = new Thickness(0.0);
{			global::ForkPlus.UI.WpfCompat.StyleCompat.SetStyle(button, global::ForkPlus.UI.Theme.TransparentButtonStyle);
}			global::Avalonia.Controls.ToolTip.SetTip(button,Preferences.PreferencesLocalization.FormatCurrent("Open '{0}' on {1} ({2})", sha.ToAbbreviatedString(), remote.Name, remote.RemoteType.FriendlyName()));
			button.Content = content;
			button.Click += delegate
			{
				new Uri(revisionShaUrl).OpenInBrowser();
			};
			return button;
		}

		private void CreateColorsPopup(ToggleButton parentButton, string email, byte userBrushIndex)
		{
			GitModule gitModule = RevisionDetailsUserControl.GitModule;
			if (gitModule != null)
			{
				Popup popup = new Popup();
				// Bug 修复（弹窗位置太偏左）：WPF 原版用 HorizontalOffset=-270 手工右对齐；
				// Avalonia 里默认定位+该偏移会把弹窗甩到按钮左侧 270px。改为 Bottom 定位、
				// 左缘对齐箭头按钮，弹窗出现在按钮正下方。
				popup.Placement = PlacementMode.Bottom;
				popup.HorizontalOffset = 0.0;
				popup.VerticalOffset = 2.0;
				popup.IsLightDismissEnabled= (!false);
				/* Migration note: AllowsTransparency 已删除 */;
				/* Migration note: PopupAnimation 已删除 */;
				popup.PlacementTarget = parentButton;
				popup.Opened += delegate
				{
					parentButton.Disable();
				};
			popup.Closed += delegate
			{
				global::ForkPlus.UI.WpfCompat.BindingCompat.ClearBinding(popup, Popup.IsOpenProperty);
				// After light-dismiss close, IsChecked is still true; reset to false; the Changed handler only handles the checked state, so it will not retrigger here.
				parentButton.IsChecked = false;
				parentButton.Enable();
				if (_openColorsToggleButton == parentButton)
				{
					_openColorsToggleButton = null;
					_openColorsPopupEmail = null;
				}
				DetachColorsPopup(popup);
			};
				UserColorsUserControl userColorsUserControl = new UserColorsUserControl(parentButton, email, userBrushIndex);
				userColorsUserControl.SelectedColorChanged += delegate(object s, (string, byte) colormap)
				{
					new UpdateUserColorGitCommand().Execute(gitModule, colormap.Item1, colormap.Item2);
					RepositoryUserControl.InvalidateAndRefresh(SubDomain.UserColors);
				};
				// Bug fix (clicking the email color dropdown arrow has no response):
				// 1) The original code first set the IsOpen←IsChecked binding (synchronously pushes IsChecked=true to immediately open the window), and only then set Child.
				//    In Avalonia, an already-opened Popup does not backfill the host's content when Child is subsequently assigned (unlike WPF),
				//    causing the popup to open empty → seemingly unresponsive on click. Must set Child first, then set the binding.
				// 2) An orphaned Popup (not in the logical tree) has its DynamicResource resolution chain truncated at itself (all themes resolve to null),
				//    same fix as GraphCellView: attach it to an ancestor Panel (occupying no layout space), and remove it on close to prevent leaks.
				VisualTreeAttachmentHelper.TrySetPopupChild(popup, userColorsUserControl, GetType().Name + ".Popup");
				AttachColorsPopup(popup);
				global::ForkPlus.UI.WpfCompat.BindingCompat.SetBinding(popup, Popup.IsOpenProperty, new Binding("IsChecked")
				{
					Source = parentButton
				});
			}
		}

		private global::Avalonia.Controls.Panel _colorsPopupHost;

		private void AttachColorsPopup(Popup popup)
		{
			if (_colorsPopupHost == null)
			{
				global::Avalonia.Visual ancestor = this;
				while ((ancestor = global::Avalonia.VisualTree.VisualExtensions.GetVisualParent(ancestor)) != null)
				{
					if (ancestor is global::Avalonia.Controls.Panel panel)
					{
						_colorsPopupHost = panel;
						break;
					}
				}
			}
			if (_colorsPopupHost != null && !_colorsPopupHost.Children.Contains(popup))
			{
				_colorsPopupHost.Children.Add(popup);
			}
		}

		private void DetachColorsPopup(Popup popup)
		{
			_colorsPopupHost?.Children.Remove(popup);
		}

		// Migration note：WPF UIElement 返回类型 → Avalonia Control（Children.Add 需要 Control）。
		private global::Avalonia.Controls.Control CreateParentButton(RepositoryUserControl repositoryUserControl, Sha parent, Action action)
		{
			return global::ForkPlus.UI.WpfCompat.StyleCompat.WithStyle(new AdvancedTooltipButton(repositoryUserControl, parent, action)
			{
				Content = parent.ToAbbreviatedString(),				Margin = new Thickness(0.0, 0.0, 3.0, 0.0),				Padding = new Thickness(0.0),				FontSize = 12.0			},ParentButtonStyle
);
		}

		private void ApplySearch()
		{
			RevisionSearchQuery searchQuery = _searchQuery;
			if (searchQuery != null && searchQuery.Type == RevisionSearchType.All)
			{
				ShaTextBlock.ApplySearchHighlighting(_searchQuery?.SearchString);
			}
			else
			{
				ShaTextBlock.ApplySearchHighlighting(null);
			}
			RevisionSearchQuery searchQuery2 = _searchQuery;
			if (searchQuery2 != null && searchQuery2.Type == RevisionSearchType.Message)
			{
				AuthorTextBlock.ApplySearchHighlighting(null);
				AuthorEmailTextBlock.ApplySearchHighlighting(null);
			}
			else
			{
				AuthorTextBlock.ApplySearchHighlighting(_searchQuery?.SearchString);
				AuthorEmailTextBlock.ApplySearchHighlighting(_searchQuery?.SearchString);
			}
			SubjectTextBlock.ApplySearchAndButrackerHighlighting(_searchQuery?.SearchString, _bugtrackers);
			DescriptionTextBlock.ApplySearchAndButrackerHighlighting(_searchQuery?.SearchString, _bugtrackers);
		}

		private void UpdateExpandAllButtonTitle(bool unused = false)
		{
			string title = AllItemsExpanded() ? "Collapse All" : "Expand All";
			ExpandAllButton.Content = Preferences.PreferencesLocalization.Translate(title, ForkPlusSettings.Default.UiLanguage);
		}

		private bool AllItemsExpanded()
		{
			bool hasRows = false;
			foreach (DiffEntry diffEntry in DiffList.Items.OfType<DiffEntry>())
			{
				hasRows = true;
				if (!diffEntry.IsExpanded)
				{
					return false;
				}
			}
			return hasRows;
		}

		private void UpdateReferences(Sha sha)
		{
			RepositoryData repositoryData = RepositoryUserControl.RepositoryData;
			if (repositoryData != null)
			{
				List<ForkPlus.Git.Reference> list = repositoryData.References.Items.Filter((ForkPlus.Git.Reference x) => x.Sha == sha);
				if (list.Count > 0)
				{
					ReferencesTextBlock.Show();
					ReferencePanel.Refresh(list, repositoryData.Remotes.Items);
					ReferencePanel.Show();
				}
				else
				{
					ReferencesTextBlock.Collapse();
					ReferencePanel.Refresh(new ForkPlus.Git.Reference[0], new Remote[0]);
					ReferencePanel.Collapse();
				}
			}
		}

		private static bool IsCommitterSectionVisible(RevisionDetails revisionDetails)
		{
			if (!(revisionDetails.Author.Name != revisionDetails.Committer.Name) && !(revisionDetails.Author.Email != revisionDetails.Committer.Email))
			{
				return revisionDetails.AuthorDate != revisionDetails.CommitterDate;
			}
			return true;
		}

		/// <summary>SHA 行的签名徽标：Good=绿 Verified，Bad=红 Bad Signature，
		/// U/X/Y/R（有效但未知/过期/吊销）=灰 Unverified，E/未知=灰 Unknown，None 隐藏。
		/// 颜色复用 Merge.StatusLabel.Green/Red（各主题均有定义）与次级信息前景。</summary>
		private void UpdateSignatureBadge(RevisionDetails revisionDetails)
		{
			string label;
			string brushKey;
			switch (revisionDetails.SignatureStatus)
			{
			case CommitSignatureStatus.Good:
				label = "Verified";
				brushKey = "Merge.StatusLabel.Green";
				break;
			case CommitSignatureStatus.Bad:
				label = "Bad Signature";
				brushKey = "Merge.StatusLabel.Red";
				break;
			case CommitSignatureStatus.Unverified:
			case CommitSignatureStatus.Expired:
			case CommitSignatureStatus.ExpiredKey:
			case CommitSignatureStatus.RevokedKey:
				label = "Unverified";
				brushKey = "ForegroundBrush.WindowsInfo";
				break;
			case CommitSignatureStatus.Error:
			case CommitSignatureStatus.Unknown:
				label = "Unknown";
				brushKey = "ForegroundBrush.WindowsInfo";
				break;
			default:
				SignatureBadgeTextBlock.IsVisible = false;
				global::Avalonia.Controls.ToolTip.SetTip(SignatureBadgeTextBlock, null);
				return;
			}
			SignatureBadgeTextBlock.Text = Preferences.PreferencesLocalization.Current(label);
			if (Application.Current?.TryFindResource(brushKey) is global::Avalonia.Media.IBrush brush)
			{
				SignatureBadgeTextBlock.Foreground = brush;
			}
			if (!string.IsNullOrEmpty(revisionDetails.Signer))
			{
				global::Avalonia.Controls.ToolTip.SetTip(SignatureBadgeTextBlock, Preferences.PreferencesLocalization.FormatCurrent("Signed by {0}", revisionDetails.Signer));
			}
			else
			{
				global::Avalonia.Controls.ToolTip.SetTip(SignatureBadgeTextBlock, null);
			}
			SignatureBadgeTextBlock.IsVisible = true;
		}

		/// <summary>AI 解释当前 commit：拉取 commit 的 subject/body/diff，打开 AiTextResultWindow 流式展示 AI 解释。</summary>
		private void AiExplainCommitButton_Click(object sender, RoutedEventArgs e)
		{
			if (!AiWindowService.EnsureAiConfigured())
			{
				// v3.10.3：未配置拦截统一走 AiWindowService（带"打开偏好设置"深链）
				return;
			}
			if (!_sha.HasValue)
			{
				return;
			}
			Sha sha = _sha.GetValueOrDefault();
			GitModule gitModule = RepositoryUserControl?.GitModule;
			if (gitModule == null)
			{
				return;
			}
			FullRevisionDetails fullRevisionDetails = RevisionDetailsUserControl?.FullRevisionDetails;
			if (fullRevisionDetails == null)
			{
				return;
			}
			fullRevisionDetails.RevisionDetails.MessageParts(out var subject, out var body);
			string commitSubject = subject ?? "";
			string commitBody = body ?? "";
			string shaStr = sha.ToString();
			string abbreviatedSha = sha.ToAbbreviatedString();

			AiTextResultWindow window = new AiTextResultWindow();
			string title = Preferences.PreferencesLocalization.FormatCurrent("AI Explain {0}", abbreviatedSha);
			AiWindowService.Show(window); // v3.10.3：弹窗行为统一（非模态+owner=主窗口+任务栏+跟随主窗口屏幕）
			window.StartStreaming(title, delegate(AiTextResultWindow w, JobMonitor monitor)
			{
				try
				{
					// 拉取 commit 的 patch（含变更文件和差异内容）
					GitCommand gitCommand = new GitCommand("--no-pager", "show", "--no-color", "--find-renames", "--submodule=short", "--unified=50", "--no-ext-diff", shaStr);
					GitRequestResult gitRequestResult = new GitRequest(gitModule).Command(gitCommand).Execute();
					string diffSummary = "";
					if (gitRequestResult.ExitCode < 2)
					{
						diffSummary = gitRequestResult.Stdout;
						// 限制 diff 体量，避免 token 爆炸
						const int maxDiffChars = 20000;
						if (diffSummary.Length > maxDiffChars)
						{
							diffSummary = diffSummary.Substring(0, maxDiffChars) + "\n... (diff truncated)\n";
						}
					}
					OpenAiService openAiService = OpenAiService.CreateFromAiReviewSettings();
					ServiceResult<OpenAiResponse> response = openAiService.ExplainCommit(commitSubject, commitBody, diffSummary, monitor, w.OnChunk);
					if (monitor.IsCanceled)
					{
						return;
					}
					if (!response.Succeeded)
					{
						w.OnError(response.Error.FriendlyMessage);
					}
					else
					{
						w.OnSuccess(response.Result.Message);
					}
				}
				catch (Exception ex)
				{
					w.OnError(ex.Message);
				}
			});
		}

		private void ListBoxItem_ContextMenuOpening(object sender, global::Avalonia.Input.ContextRequestedEventArgs e)
		{
			if (sender is ListBoxItem { DataContext: DiffEntry diffEntry } listBoxItem)
			{
				RepositoryUserControl repositoryUserControl = RepositoryUserControl;
				if (repositoryUserControl != null)
				{
					RepositoryData repositoryData = repositoryUserControl.RepositoryData;
					if (repositoryData != null)
					{
						GitModule gitModule = repositoryUserControl.GitModule;
						if (gitModule != null)
						{
							Sha? sha = _sha;
							if (sha.HasValue)
							{
								Sha valueOrDefault = sha.GetValueOrDefault();
								listBoxItem.ContextMenu.SetItems(CreateFileContextMenuItems(RevisionDetailsUserControl, repositoryUserControl, repositoryData, gitModule, valueOrDefault, diffEntry.ChangedFile));
								return;
							}
						}
					}
				}
				e.Handled = true;
			}
			else
			{
				e.Handled = true;
			}
		}

		private static IEnumerable<Control> CreateFileContextMenuItems(RevisionDetailsUserControl revisionDetailsUserControl, RepositoryUserControl repositoryUserControl, RepositoryData repositoryData, GitModule gitModule, Sha sha, ChangedFile changedFile)
		{
			bool isSubmodule = changedFile is SubmoduleChangedFile;
			SubmoduleChangedFile submoduleChangedFile = changedFile as SubmoduleChangedFile;
			if (submoduleChangedFile != null)
			{
				yield return RepositoryUserControl.Commands.OpenSubmodule.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.OpenSubmodule.Execute(repositoryUserControl, gitModule, new Submodule[1] { submoduleChangedFile.Submodule });
				});
				yield return new Separator();
			}
			if (!isSubmodule)
			{
				bool isEnabled = RepositoryUserControl.Commands.OpenFileInDefaultEditor.IsEditorAvailable(gitModule, changedFile.Path);
				yield return RepositoryUserControl.Commands.OpenFileInDefaultEditor.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.OpenFileInDefaultEditor.Execute(gitModule, changedFile.Path);
				}, isEnabled, showShortcut: false);
				List<ExternalTool> diffTools = ExternalToolManager.RevealAvailableDiffTools().Filter((ExternalTool x) => x.IsVisible);
				if (diffTools.Count > 0)
				{
					bool diffIsEnabled = !changedFile.IsDirectory;
					RunExternalDiffToolCommand.DiffTarget.Revision diffTarget = new RunExternalDiffToolCommand.DiffTarget.Revision(sha, null, changedFile);
					if (diffTools.Count == 1)
					{
						ExternalTool diffTool = diffTools[0];
						yield return RepositoryUserControl.Commands.RunExternalDiffTool.CreateMenuItemFormat("Diff in {0}", new object[1] { diffTool.Name }, delegate
						{
							RepositoryUserControl.Commands.RunExternalDiffTool.Execute(repositoryUserControl, diffTarget, diffTool);
						}, diffIsEnabled, null, showShortcut: false);
					}
					else if (diffTools.Count > 1)
					{
						MenuItem diffMenuItem = new MenuItem
						{
							Header = Preferences.PreferencesLocalization.MenuHeader("External Diff"),
							IsEnabled = diffIsEnabled
						};
						foreach (ExternalTool diffTool in diffTools)
						{
							ExternalTool capturedDiffTool = diffTool;
							MenuItem newItem = RepositoryUserControl.Commands.RunExternalDiffTool.CreateMenuItem(capturedDiffTool.Name ?? "", delegate
							{
								RepositoryUserControl.Commands.RunExternalDiffTool.Execute(repositoryUserControl, diffTarget, capturedDiffTool);
							}, isEnabled: true, null, showShortcut: false);
							diffMenuItem.Items.Add(newItem);
						}
						yield return diffMenuItem;
					}
				}
			}
			yield return RepositoryUserControl.Commands.ShowFileInFileExplorer.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowFileInFileExplorer.Execute(gitModule, changedFile.Path);
			});
			yield return new Separator();
			MenuItem resetMenuItem = new MenuItem
			{
				Header = Preferences.PreferencesLocalization.MenuHeader("Reset File To")
			};
			resetMenuItem.Items.Add(RepositoryUserControl.Commands.ResetFileToStateAtRevision.CreateMenuItem("State At Commit...", delegate
			{
				RepositoryUserControl.Commands.ResetFileToStateAtRevision.Execute(repositoryUserControl, new ChangedFile[1] { changedFile }, sha.ToString());
			}));
			resetMenuItem.Items.Add(RepositoryUserControl.Commands.ResetFileToStateAtRevision.CreateMenuItem("State Before Commit...", delegate
			{
				RepositoryUserControl.Commands.ResetFileToStateAtRevision.Execute(repositoryUserControl, new ChangedFile[1] { changedFile }, sha.ToString() + "~");
			}));
			yield return resetMenuItem;
			yield return new Separator();
			if (!isSubmodule)
			{
				yield return RepositoryUserControl.Commands.ShowBlameWindow.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.ShowBlameWindow.Execute(repositoryUserControl, changedFile.Path, sha);
				});
			}
			yield return RepositoryUserControl.Commands.ShowFileHistoryWindow.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowFileHistoryWindow.Execute(repositoryUserControl, changedFile.Mode(), sha);
			});
			yield return RepositoryUserControl.Commands.ShowFileInFileTree.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.ShowFileInFileTree.Execute(revisionDetailsUserControl, changedFile.Path);
			});
			if (!isSubmodule && repositoryData.GitLfsInitialized && repositoryData.Remotes.HasLfsCompatibleRemotes())
			{
				MenuItem lfsMenuItem = new MenuItem();
				lfsMenuItem.Header = Preferences.PreferencesLocalization.MenuHeader("LFS");
				lfsMenuItem.Items.Add(RepositoryUserControl.Commands.GitLfsLockCommand.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.GitLfsLockCommand.Execute(repositoryUserControl, new string[1] { changedFile.Path });
				}));
				lfsMenuItem.Items.Add(RepositoryUserControl.Commands.GitLfsUnlockCommand.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.GitLfsUnlockCommand.Execute(repositoryUserControl, new string[1] { changedFile.Path });
				}));
				yield return new Separator();
				yield return lfsMenuItem;
			}
			CustomCommand[] customCommands = CustomCommandManager.Current.GetCustomCommands(repositoryUserControl.RepositoryData, CustomCommandTarget.RepositoryFile);
			CustomCommandEnvironment env = new CustomCommandEnvironment(gitModule, changedFile.Path, sha);
			if (changedFile is SubmoduleChangedFile customCommandSubmoduleChangedFile)
			{
				customCommands = CustomCommandManager.Current.GetCustomCommands(repositoryUserControl.RepositoryData, CustomCommandTarget.Submodule);
				CustomCommandEnvironment.Parameter[] parameters = new CustomCommandEnvironment.SubmoduleParameter[1]
				{
					new CustomCommandEnvironment.SubmoduleParameter(customCommandSubmoduleChangedFile.Submodule)
				};
				env = new CustomCommandEnvironment(gitModule, parameters);
			}
			if (customCommands.Length != 0)
			{
				yield return new Separator();
				List<MenuItem> customMenuItems = new List<MenuItem>();
				foreach (CustomCommand customCommand in customCommands)
				{
					if (customCommand.OS.IsSupported())
					{
						customCommand.AddCustomCommandItem(repositoryUserControl, env, customCommand.Name.Split(Consts.Chars.Slash), 0, customMenuItems);
					}
				}
				foreach (MenuItem menuItem in customMenuItems)
				{
					yield return menuItem;
				}
			}
			if (!isSubmodule)
			{
				yield return new Separator();
				yield return RepositoryUserControl.Commands.SaveFile.CreateMenuItem(delegate
				{
					RepositoryUserControl.Commands.SaveFile.Execute(repositoryUserControl, changedFile, sha.ToString());
				});
			}
			yield return new Separator();
			yield return RepositoryUserControl.Commands.CopyFilePaths.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.CopyFilePaths.Execute(new string[1] { changedFile.Path });
			}, isEnabled: true, showShortcut: false);
			yield return RepositoryUserControl.Commands.CopyAbsoluteFilePaths.CreateMenuItem(delegate
			{
				RepositoryUserControl.Commands.CopyAbsoluteFilePaths.Execute(gitModule, new string[1] { changedFile.Path });
			}, isEnabled: true, showShortcut: false);
		}

	}
}
