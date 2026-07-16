using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Dialogs.RepositoryOverview;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI;

namespace ForkPlus.UI.Dialogs
{
	public partial class RepositoryOverviewWindow : CustomWindow, ITreemapDelegate
	{
		private readonly JobQueue _jobQueue = new JobQueue();

		private readonly GitModule _gitModule;

		private ITreemapDataSource _dataSource;

		private RepositoryOverviewData _repositoryOverviewData;

		private static readonly Typeface _typeface;

		private static readonly Brush _itemBackgroundBrush;

		private static readonly Pen _hoverBorderPen;

		private static readonly BitmapImage FolderIcon;

		private Brush _labelBrush;

		private Brush _secondaryLabelBrush;

		private Pen _borderPen;

		private Pen _selectedBorderPen;

		private readonly TextDrawer _titleGlyphDrawer;

		private readonly TextDrawer _secondaryLabelGlyphDrawer;

		private double _borderRadius = 3.0;

		private double _headerHeight = 20.0;

		private Size _itemPadding = new Size(2.0, 2.0);

		static RepositoryOverviewWindow()
		{
			_typeface = new Typeface(new FontFamily("Segoe UI Variable Display"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
			_itemBackgroundBrush = new SolidColorBrush(Color.FromArgb(8, 170, 170, 170));
			_hoverBorderPen = new Pen(new SolidColorBrush(Colors.Gray), 1.0);
			FolderIcon = new BitmapImage(new Uri("pack://application:,,,/ForkPlus;component/Assets/Folder.png"));
			_itemBackgroundBrush.Freeze();
			_hoverBorderPen.Freeze();
		}

		public RepositoryOverviewWindow(RepositoryUserControl repositoryUserControl, GitModule gitModule)
		{
			RepositoryOverviewWindow repositoryOverviewWindow = this;
			_gitModule = gitModule;
			_titleGlyphDrawer = new TextDrawer(_typeface, 12.0, VisualTreeHelper.GetDpi(this).PixelsPerDip);
			_secondaryLabelGlyphDrawer = new TextDrawer(_typeface, 11.0, VisualTreeHelper.GetDpi(this).PixelsPerDip);
			base.Title = string.Format(Translate("{0} - Repository Overview"), gitModule.RepositoryName);
			base.ShowInTaskbar = true;
			base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
			base.ResizeMode = ResizeMode.CanResizeWithGrip;
			InitializeComponent();
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			TreemapBackgroundBorder.CornerRadius = new CornerRadius(_borderRadius);
			RepositoryNameTextBlock.Text = gitModule.Path;
			LocalBranch activeBranch = repositoryUserControl.RepositoryData.References.ActiveBranch;
			if (activeBranch != null)
			{
				TargetReferenceGitPointView.Value = activeBranch;
			}
			else
			{
				TargetReferenceGitPointView.Collapse();
			}
			CommitsUserControl.Initialize(repositoryUserControl);
			Treemap.Delegate = this;
			Treemap.SelectionChanged += delegate
			{
				try
				{
					if (repositoryOverviewWindow._dataSource != null)
					{
						Treemap.IndexPath selectedIndexPath = repositoryOverviewWindow.Treemap.SelectedIndexPath;
						if (selectedIndexPath != null)
						{
							if (repositoryOverviewWindow._dataSource is CommitsCountDataSource dataSource)
							{
								string text = dataSource.GetPath(selectedIndexPath).TrimEnd('/');
								string text2 = text;
								string text3 = "";
								Sha[] shas = repositoryOverviewWindow._repositoryOverviewData.GetShas(repositoryOverviewWindow.DateRangeButton.DateRange.ToServiceCalendarDateRange().Quantize(), text);
								GitCommandResult<RevisionHeader[]> gitCommandResult = new GetRevisionHeadersGitCommand().Execute(repositoryOverviewWindow._gitModule, shas);
								if (!gitCommandResult.Succeeded)
								{
									Log.Error(gitCommandResult.Error.FriendlyDescription);
								}
								else if (gitCommandResult.Result != null && gitCommandResult.Result.Length == shas.Length)
								{
									Revision[] array = new Revision[shas.Length];
									for (int i = 0; i < shas.Length; i++)
									{
										array[i] = new Revision(shas[i], gitCommandResult.Result[i]);
									}
									repositoryOverviewWindow.CommitsUserControl.UpdateData(text, array);
								}
								else
								{
									// native 返回的 header 数与 SHA 数不匹配（悬挂对象/shallow clone/缓存不一致），
									// 跳过提交列表更新，避免 IndexOutOfRangeException 崩溃。
									Log.Error(Translate("Revision header count mismatch"));
								}
								(int, int, DateTime)[] authorStats = repositoryOverviewWindow._repositoryOverviewData.GetAuthorStats(repositoryOverviewWindow.DateRangeButton.DateRange.ToServiceCalendarDateRange().Quantize(), text);
								repositoryOverviewWindow.AuthorsUserControl.UpdateData(authorStats, repositoryOverviewWindow._repositoryOverviewData.UserIdentities);
								int num = text.LastIndexOf("/");
								if (num != -1)
								{
									text2 = text.Substring(num + 1);
									text3 = text.Substring(0, num);
								}
								repositoryOverviewWindow.SelectedFileNameTextBlock.Text = text2;
								repositoryOverviewWindow.SelectedFileIcon.Source = IconTools.GetImageSourceForPath(text2);
								repositoryOverviewWindow.SelectedFilePathTextBlock.Text = text3;
							}
						}
						else
						{
							repositoryOverviewWindow.SelectedFileNameTextBlock.Text = "";
							repositoryOverviewWindow.SelectedFileIcon.Source = null;
							repositoryOverviewWindow.SelectedFilePathTextBlock.Text = "/";
						}
					}
				}
				catch (Exception ex)
				{
					// 仓库树图点击链路涉及 native 调用与大量索引操作，任何未捕获异常都会冒到 Dispatcher
					// 导致应用级崩溃。这里兜底记录日志，保持窗口可用。
					Log.Error(ex.ToString());
				}
			};
			DateRangeButton.DateRangeChanged += delegate
			{
				repositoryOverviewWindow.RefreshData(repositoryOverviewWindow.DateRangeButton.DateRange.ToServiceCalendarDateRange().Quantize());
			};
			Fallback.Show();
			Fallback.FallbackTitle = Translate("Loading...");
			Fallback.FallbackMessage = "0.0";
			_jobQueue.Add(Translate("Read repository overview"), delegate(JobMonitor monitor)
			{
				if (!monitor.IsCanceled)
				{
					monitor.SetProgressAction(delegate
					{
						repositoryOverviewWindow.Dispatcher.Async(delegate
						{
							if (repositoryOverviewWindow.Fallback.IsVisible)
							{
								repositoryOverviewWindow.Fallback.FallbackMessage = monitor.ProgressMessage;
							}
						});
					});
					GitCommandResult<RepositoryOverviewData> overviewResponse = new GetRepositoryOverviewDataGitCommand().Execute(gitModule, monitor);
					monitor.SetProgressAction(null);
					if (!overviewResponse.Succeeded)
					{
						repositoryOverviewWindow.Dispatcher.Async(delegate
						{
							repositoryOverviewWindow.Fallback.FallbackTitle = Translate("Error");
							repositoryOverviewWindow.Fallback.FallbackMessage = overviewResponse.Error.FriendlyDescription;
						});
					}
					else
					{
						repositoryOverviewWindow.Dispatcher.Async(delegate
						{
							repositoryOverviewWindow._repositoryOverviewData = overviewResponse.Result;
							DateTime dateTime = overviewResponse.Result.AuthorDates.LastOrDefault();
							DateTime dateTime2 = overviewResponse.Result.AuthorDates.FirstOrDefault();
							repositoryOverviewWindow.DateRangeButton.Show();
							repositoryOverviewWindow.DateRangeButton.MinDate = dateTime;
							repositoryOverviewWindow.DateRangeButton.MaxDate = dateTime2;
							repositoryOverviewWindow.DateRangeButton.DateRange = new CalendarDateRange(dateTime, dateTime2);
							repositoryOverviewWindow.Fallback.Hide();
						});
					}
				}
			});
			RefreshBrushes();
			WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
		}

		private void RefreshData(ForkPlus.Services.CalendarDateRange dateRange)
		{
			RepositoryOverviewData repositoryOverviewData = _repositoryOverviewData;
			(string, List<int>)[] files = repositoryOverviewData.Files.Map((KeyValuePair<string, List<int>> x) => (Key: x.Key, Value: x.Value)).ToSortedArray(((string Key, List<int> Value) x, (string Key, List<int> Value) y) => string.CompareOrdinal(x.Key, y.Key));
			int index = 0;
			(CommitsCountDataSource.Item[], HashSet<int>) tuple = ReadCommitsCountDataSourceItems(files, ref index, "", (int x) => dateRange.Contains(repositoryOverviewData.AuthorDates[x]));
			CommitsCountDataSource.Item[] item = tuple.Item1;
			HashSet<int> item2 = tuple.Item2;
			CommitsCountDataSource.Item item3 = new CommitsCountDataSource.Item(_gitModule.RepositoryName, item2.Count, item);
			CommitsCountDataSource dataSource = (CommitsCountDataSource)(_dataSource = new CommitsCountDataSource(new CommitsCountDataSource.Item[1] { item3 }));
			Treemap.DataSource = dataSource;
			Treemap.SelectedIndexPath = dataSource.FirstVisualItem();
		}

		private (CommitsCountDataSource.Item[], HashSet<int>) ReadCommitsCountDataSourceItems((string, List<int>)[] files, ref int index, string prefix, Func<int, bool> commitCountPredicate)
		{
			HashSet<int> hashSet = new HashSet<int>();
			List<CommitsCountDataSource.Item> list = new List<CommitsCountDataSource.Item>();
			while (true)
			{
				(CommitsCountDataSource.Item, HashSet<int>)? tuple = ReadCommitsCountDataSourceItem(files, ref index, prefix, commitCountPredicate);
				if (!tuple.HasValue)
				{
					break;
				}
				list.Add(tuple.Value.Item1);
				foreach (int item in tuple.Value.Item2)
				{
					hashSet.Add(item);
				}
			}
			return (list.ToArray(), hashSet);
		}

		[Null]
		private (CommitsCountDataSource.Item, HashSet<int>)? ReadCommitsCountDataSourceItem((string, List<int>)[] files, ref int index, string prefix, Func<int, bool> commitCountPredicate)
		{
			while (index < files.Length)
			{
				var (text, source) = files[index];
				if (!string.IsNullOrEmpty(prefix) && !text.StartsWith(prefix))
				{
					break;
				}
				string text2 = text.Substring(prefix.Length);
				int num = text2.IndexOf('/');
				if (num != -1)
				{
					string prefix2 = prefix + text2.Substring(0, num + 1);
					var (children, hashSet) = ReadCommitsCountDataSourceItems(files, ref index, prefix2, commitCountPredicate);
					if (hashSet.Count != 0)
					{
						return (new CommitsCountDataSource.Item(text2.Substring(0, num), hashSet.Count, children), hashSet);
					}
				}
				else
				{
					HashSet<int> hashSet2 = new HashSet<int>(source.Where(commitCountPredicate));
					index++;
					if (hashSet2.Count != 0)
					{
						return (new CommitsCountDataSource.Item(text2, hashSet2.Count), hashSet2);
					}
				}
			}
			return null;
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				Close();
			}
		}

		private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
		{
			RefreshBrushes();
			InvalidateVisual();
		}

		string ITreemapDelegate.GetItemTitle(object array, int index)
		{
			return (array as CommitsCountDataSource.Item[])[index].Title;
		}

		void ITreemapDelegate.DrawChildInRect(DrawingContext ctx, object items, int index, Rect rect, bool isHover, bool isSelected)
		{
			if (_dataSource == null)
			{
				return;
			}
			Rect rectangle = rect.Inset(_itemPadding.Width, _itemPadding.Height);
			Pen pen = _borderPen;
			if (isSelected)
			{
				pen = _selectedBorderPen;
			}
			else if (isHover)
			{
				pen = _hoverBorderPen;
			}
			ctx.DrawRoundedRectangle(_itemBackgroundBrush, pen, rectangle, _borderRadius, _borderRadius);
			rect.DivideFromTop(_headerHeight).Deconstruct(out var item, out var item2);
			Rect rect2 = item;
			bool hasValue = _dataSource.GetItemChildrenCount(items, index).HasValue;
			string itemTitle = Treemap.Delegate.GetItemTitle(items, index);
			long itemSizeValue = _dataSource.GetItemSizeValue(items, index);
			if (hasValue)
			{
				if (Math.Min(rect2.Width, rect2.Height) <= 12.0)
				{
					return;
				}
				ctx.DrawImage(rectangle: new Rect(new Point(rect2.X + 6.0, rect2.Y + 6.0), new Size(12.0, 12.0)), imageSource: FolderIcon);
				if (!(rect2.Width < 30.0))
				{
					Rect rect3 = new Rect(rect2.X + 20.0, rect2.Y + 4.0, rect2.Width - 24.0, rect2.Height - 6.0);
					double num = _titleGlyphDrawer.DrawText(ctx, itemTitle, _labelBrush, rect3, TextAlignment.Left, trimming: true);
					if (rect3.Width - num > 20.0)
					{
						rect3.DivideFromLeft(num + 2.0).Deconstruct(out item2, out item);
						Rect rect4 = item;
						_secondaryLabelGlyphDrawer.DrawText(ctx, itemSizeValue.ToString(), _secondaryLabelBrush, rect4);
					}
				}
				return;
			}
			ImageSource imageSourceForPath = IconTools.GetImageSourceForPath(itemTitle);
			if (rect.Height > 70.0)
			{
				double num2 = Math.Min(32.0, Math.Min(rect.Width - 16.0, rect.Height - 16.0));
				Size size = new Size(num2, num2);
				double num3 = rect.X + rect.Width / 2.0;
				double num4 = rect.Y + rect.Height / 2.0;
				Rect rectangle3 = new Rect(new Point(num3 - size.Width / 2.0, num4 - size.Height / 2.0), size);
				ctx.DrawImage(imageSourceForPath, rectangle3);
				if (!(rect2.Width < 50.0))
				{
					Rect rect5 = new Rect(rect2.X + 6.0, rect2.Y + 3.0, rect2.Width - 8.0, rect2.Height - 4.0);
					_titleGlyphDrawer.DrawText(ctx, itemTitle, _labelBrush, rect5, TextAlignment.Left, trimming: true);
					Rect rect6 = new Rect(rect2.X + 2.0, rectangle3.Bottom, rect2.Width - 4.0, rect2.Height - 6.0);
					if (rect6.Left > rect.Left)
					{
						_secondaryLabelGlyphDrawer.DrawText(ctx, itemSizeValue.ToString(), _secondaryLabelBrush, rect6, TextAlignment.Center);
					}
				}
			}
			else
			{
				double num5 = Math.Min(48.0, Math.Min(rect.Width, rect.Height)) - 4.0;
				Size size2 = new Size(num5, num5);
				double num6 = rect.X + rect.Width / 2.0;
				double num7 = rect.Y + rect.Height / 2.0;
				Rect rectangle4 = new Rect(new Point(num6 - size2.Width / 2.0, num7 - size2.Height / 2.0), size2);
				ctx.DrawImage(imageSourceForPath, rectangle4);
			}
		}

		[Null]
		TooltipView ITreemapDelegate.CreateTooltip(Treemap.IndexPath indexPath)
		{
			if (_dataSource == null)
			{
				return null;
			}
			TooltipView tooltipView = new TooltipView();
			if (_dataSource is CommitsCountDataSource dataSource)
			{
				string text = dataSource.GetPath(indexPath).TrimEnd('/');
				string text2 = text;
				int num = text.LastIndexOf("/");
				if (num != -1)
				{
					text2 = text.Substring(num + 1);
				}
				ImageSource imageSourceForPath = IconTools.GetImageSourceForPath(text2);
				long valueOrDefault = dataSource.GetItemValue(indexPath).GetValueOrDefault();
				tooltipView.SetDetails(imageSourceForPath, text2, text.TrimStart('/'), string.Format(Translate("{0} commits"), valueOrDefault));
				return tooltipView;
			}
			return null;
		}

		private void RefreshBrushes()
		{
			_labelBrush = Theme.LabelBrush;
			_secondaryLabelBrush = Theme.SecondaryLabelBrush;
			_borderPen = new Pen(Theme.BorderBrush, 1.0);
			_selectedBorderPen = new Pen(Theme.AccentBrush, 2.0);
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
