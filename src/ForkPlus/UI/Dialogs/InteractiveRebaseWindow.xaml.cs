using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Threading;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using ForkPlus.IO.Ipc;
using ForkPlus.Jobs;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;
using Newtonsoft.Json;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.Dialogs
{
	public partial class InteractiveRebaseWindow : ForkPlusDialogWindow, IDisposable
	{
		public static readonly InteractiveRebaseComboBoxItem[] InteractiveRebaseComboBoxItems = new InteractiveRebaseComboBoxItem[8]
		{
			new InteractiveRebaseComboBoxItem(InteractiveRebaseAction.Pick, "Pick", "Use Commit", "P"),
			new InteractiveRebaseComboBoxItem(InteractiveRebaseAction.Edit, "Edit", "Stop for amending", "E"),
			new InteractiveRebaseComboBoxItem(InteractiveRebaseAction.Reword, "Reword", "Edit the commit message", "R"),
			new InteractiveRebaseComboBoxItem(InteractiveRebaseAction.Squash, "Squash", "Meld commit into previous one and keep message", "S"),
			new InteractiveRebaseComboBoxItem(InteractiveRebaseAction.Fixup, "Fixup", "Meld commit into previous one and discard message", "F"),
			new InteractiveRebaseComboBoxItem(InteractiveRebaseAction.Drop, "Drop", "Remove commit", "D"),
			new InteractiveRebaseComboBoxItem(null, "Move Up", "", "Ctrl+↑", isSelectable: false),
			new InteractiveRebaseComboBoxItem(null, "Move Down", "", "Ctrl+↓", isSelectable: false)
		};

		private static readonly int Timeout = 900000;

		private readonly GitModule _gitModule;

		private readonly LocalBranch _sourceBranch;

		[Null]
		private readonly IGitPoint _destination;

		private readonly IrAction _initialAction;

		private readonly IpcServer _riIpcServer;

		private readonly Task _riProcessRunner;

		private readonly Semaphore _finishRiProcessSemaphore = new Semaphore(0, 1);

		private readonly ObservableCollection<RevisionEntry> _todoList = new ObservableCollection<RevisionEntry>();

		private readonly DelayedAction<string> _refreshRevisionDetails;

		private (InteractiveRebaseAction Action, string Sha)[] _initialTodoList;

		private bool _closing;

		private bool _rebaseProcessRunning;

		private bool _suppressRewordDialog;

		private string _response = "";

		private string _todoListPath;

		private CheckBox _backupCurrentStateCheckBox;

		private readonly RepositoryReferences _references;

		private bool _updateInProgress;

		private RewordAdorner _adorner;

		public InteractiveRebaseComboBoxItem[] InteractiveRebaseComboBoxItemsSource { get; }

		protected override bool IsSubmitAllowed
		{
			get
			{
				if (_todoList.Count > 0)
				{
					return !_closing;
				}
				return false;
			}
		}

		public InteractiveRebaseWindow(RepositoryUserControl repositoryUserControl, GitModule gitModule, LocalBranch sourceBranch, [Null] IGitPoint destination, IrAction initialAction)
		{
			InteractiveRebaseWindow interactiveRebaseWindow = this;
			_gitModule = gitModule;
			_sourceBranch = sourceBranch;
			_destination = destination;
			_initialAction = initialAction;
			_references = repositoryUserControl.RepositoryData?.References ?? RepositoryReferences.Empty;
			_riIpcServer = new IpcServer("RI", IpcMessageHandler);
			SubmodulesToUpdate submodulesToUpdate = repositoryUserControl.SubmodulesToUpdate();
			base.ShowLogo = false;
			base.ShowHeader = false;
			InitializeComponent();
			UpdateRefsCheckBox.IsChecked = ForkPlusSettings.Default.InteractiveRebase_UpdateRefs;
			_backupCurrentStateCheckBox = new CheckBox
			{
				Content = Translate("Backup current state with a temporary branch"),
				VerticalAlignment = VerticalAlignment.Center,
				Margin = new Thickness(0.0, 2.0, 0.0, 0.0)
			};
			_backupCurrentStateCheckBox.IsChecked = ForkPlusSettings.Default.InteractiveRebase_CreateBackup;
			VisualTreeAttachmentHelper.TrySetContent(base.Footer.CustomSection, _backupCurrentStateCheckBox, GetType().Name + ".Footer.CustomSection");
			base.Footer.AlignStatusRight();
			base.ShowCancelButton = true;
			base.SubmitButtonTitle = Translate("Rebase");
			SourceGitPointView.Value = _sourceBranch;
			DestinationGitPointView.Value = _destination;
			InteractiveRebaseComboBoxItemsSource = InteractiveRebaseComboBoxItems;
			RevisionListView.ItemsSource = _todoList;
			RevisionListView.DataContext = this;
			RevisionDetails.Initialize(repositoryUserControl, RevisionDetailsUserControlMode.InteractiveRebase);
			RevisionListFallbackUserControl.Show();
			RevisionListFallbackUserControl.FallbackMessage = Translate("Loading...");
			SetStatus(ForkPlusDialogStatus.InProgress, "");
			_refreshRevisionDetails = new DelayedAction<string>(delegate(string shaString)
			{
				Sha.TryParse(shaString, out var result);
				interactiveRebaseWindow.RevisionDetails.ShowRevisionDetails(new RevisionDiffTarget.Revision(result));
			});
			_rebaseProcessRunning = true;
			_riProcessRunner = Task.Run(delegate
			{
				GitCommandResult rebaseResult = new RebaseInteractiveGitCommand().Execute(interactiveRebaseWindow._gitModule, destination);
				if (rebaseResult.Succeeded && submodulesToUpdate.Length > 0)
				{
					GitCommandResult updateSubmodulesResult = new UpdateSubmodulesGitCommand().Execute(gitModule, submodulesToUpdate, new JobMonitor());
					if (!updateSubmodulesResult.Succeeded)
					{
						interactiveRebaseWindow.Dispatcher.Async(delegate
						{
							interactiveRebaseWindow._rebaseProcessRunning = false;
							interactiveRebaseWindow.Close(updateSubmodulesResult);
						});
						return;
					}
				}
				interactiveRebaseWindow.Dispatcher.Async(delegate
				{
					interactiveRebaseWindow._rebaseProcessRunning = false;
					interactiveRebaseWindow.Close(rebaseResult);
				});
			});
		}

		public void Dispose()
		{
			_riIpcServer.Dispose();
		}

		protected override void OnClosing(CancelEventArgs e)
		{
			if (!_rebaseProcessRunning)
			{
				base.OnClosing(e);
				return;
			}
			e.Cancel = true;
			if (!IsTodoListChanged() || IrCancelConfirmed())
			{
				StopRebaseInteractiveProcess("cancel");
			}
		}

		private bool IsTodoListChanged()
		{
			if (_initialTodoList == null)
			{
				return false;
			}
			for (int i = 0; i < _initialTodoList.Length; i++)
			{
				if (_todoList[i].Action != _initialTodoList[i].Action || _todoList[i].Sha != _initialTodoList[i].Sha)
				{
					return true;
				}
			}
			return false;
		}

		private bool IrCancelConfirmed()
		{
			return new MessageBoxWindow("Do you really want to cancel Interactive Rebase?", "All your changes will be discarded.", "Yes", "No", showCancelButton: true, 550.0).ShowDialog().GetValueOrDefault();
		}

		protected override void OnSubmit()
		{
			SetStatus(ForkPlusDialogStatus.InProgress, "Rebasing...");
			bool updateRefs = UpdateRefsCheckBox.IsChecked.GetValueOrDefault();
			string contents = string.Concat(from x in _todoList.Reverse()
				select x.AsTodoListString(updateRefs));
			File.WriteAllText(_todoListPath, contents);
			SaveMessageArchiveForTodoList(_todoListPath);
			ForkPlusSettings.Default.InteractiveRebase_UpdateRefs = updateRefs;
			ForkPlusSettings.Default.InteractiveRebase_CreateBackup = _backupCurrentStateCheckBox.IsChecked.GetValueOrDefault();
			if (_backupCurrentStateCheckBox.IsChecked.GetValueOrDefault())
			{
				CreateBackupBranch();
			}
			StopRebaseInteractiveProcess("start");
		}

		private void StopRebaseInteractiveProcess(string response)
		{
			if (!_closing)
			{
				_closing = true;
				_response = response;
				_finishRiProcessSemaphore.Release();
				DisableEditableControls();
			}
		}

		private void CreateBackupBranch()
		{
			string branchName = "backup/" + _sourceBranch.Name + "-" + DateTime.Now.ToString("HH-mm-ss");
			new CreateNewBranchGitCommand().Execute(_gitModule, branchName, checkout: false, new SymbolicReference("HEAD"), new JobMonitor());
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

		private void SaveMessageArchiveForTodoList(string todoListPath)
		{
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			List<RevisionEntry> list = new List<RevisionEntry>();
			foreach (RevisionEntry todo in _todoList)
			{
				if (todo.Action == InteractiveRebaseAction.Drop)
				{
					continue;
				}
				if (todo.Action == InteractiveRebaseAction.Squash || todo.Action == InteractiveRebaseAction.Fixup)
				{
					list.Add(todo);
				}
				else if (list.Count > 0)
				{
					list.Add(todo);
					foreach (RevisionEntry item in list)
					{
						dictionary[item.Sha] = todo.Message;
					}
					list.Clear();
				}
				else
				{
					dictionary[todo.Sha] = todo.Message;
				}
			}
			string contents = JsonConvert.SerializeObject(dictionary);
			try
			{
				File.WriteAllText(Path.Combine(Path.GetDirectoryName(todoListPath), "fork-message-archive"), contents);
			}
			catch (Exception ex)
			{
				Log.Error("Failed to write IR message archive for '" + todoListPath + "'", ex);
			}
		}

		protected override void OnPreviewKeyDown(KeyEventArgs e)
		{
			base.OnPreviewKeyDown(e);
			RevisionEntry[] array = null;
			if (e.OriginalSource is ListViewItem || e.OriginalSource is ListView)
			{
				array = RevisionListView.SelectedItems.CompactMap((object x) => x as RevisionEntry);
			}
			else if (e.OriginalSource is ComboBoxItem container && ItemsControl.ItemsControlFromItemContainer(container)?.DataContext is RevisionEntry revisionEntry)
			{
				array = new RevisionEntry[1] { revisionEntry };
			}
			if (array == null || array.Length == 0)
			{
				return;
			}
			if (e.Key == Key.P)
			{
				RevisionEntry[] array2 = array;
				for (int i = 0; i < array2.Length; i++)
				{
					array2[i].Action = InteractiveRebaseAction.Pick;
				}
			}
			else if (e.Key == Key.E)
			{
				RevisionEntry[] array2 = array;
				for (int i = 0; i < array2.Length; i++)
				{
					array2[i].Action = InteractiveRebaseAction.Edit;
				}
			}
			else if (e.Key == Key.R && array.Length == 1)
			{
				e.Handled = true;
				RevisionEntry revisionEntry2 = array.FirstItem();
				revisionEntry2.Action = InteractiveRebaseAction.Reword;
				ShowRewordPopup(revisionEntry2);
			}
			else if (e.Key == Key.S)
			{
				RevisionEntry[] array2 = array;
				for (int i = 0; i < array2.Length; i++)
				{
					array2[i].Action = InteractiveRebaseAction.Squash;
				}
			}
			else if (e.Key == Key.F)
			{
				e.Handled = true;
				RevisionEntry[] array2 = array;
				for (int i = 0; i < array2.Length; i++)
				{
					array2[i].Action = InteractiveRebaseAction.Fixup;
				}
			}
			else if (e.Key == Key.D)
			{
				RevisionEntry[] array2 = array;
				for (int i = 0; i < array2.Length; i++)
				{
					array2[i].Action = InteractiveRebaseAction.Drop;
				}
			}
			else if (e.Key == Key.Up && Keyboard.IsKeyDown(Key.LeftCtrl))
			{
				e.Handled = true;
				MoveUp(array);
			}
			else if (e.Key == Key.Down && Keyboard.IsKeyDown(Key.LeftCtrl))
			{
				e.Handled = true;
				MoveDown(array);
			}
		}

		private void RevisionListView_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			UpdateListViewColumnsWidth();
		}

		private void RevisionListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			e.Handled = true;
			IRoundedSelectionListBoxViewModel[] selectedItems;
			IRoundedSelectionListBoxViewModel[] source = (selectedItems = RevisionListView.SelectedItems.CompactMap((object x) => x as RevisionEntry));
			selectedItems.RefreshSelectionType();
			RevisionEntry revisionEntry = ((IReadOnlyList<RevisionEntry>)source).FirstItem();
			if (revisionEntry != null)
			{
				_refreshRevisionDetails.InvokeWithDelay(revisionEntry.Sha);
			}
		}

		private void UpdateListViewColumnsWidth()
		{
			GridView gridView = RevisionListView.View as GridView;
			double num = 0.0;
			for (int i = 0; i < gridView.Columns.Count; i++)
			{
				if (i != 2)
				{
					num += gridView.Columns[i].ActualWidth;
				}
			}
			double num2 = RevisionListView.ActualWidth - SystemParameters.VerticalScrollBarWidth - 5.0 - num;
			gridView.Columns[2].Width = ((num2 > 0.0) ? num2 : 0.0);
		}

		private void RevisionListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
		{
			if (!e.IsClickedOnScrollbar() && RevisionListView.SelectedItem is RevisionEntry { Action: not InteractiveRebaseAction.Drop, Action: not InteractiveRebaseAction.Fixup, Action: not InteractiveRebaseAction.Squash } revisionEntry)
			{
				ShowRewordPopup(revisionEntry);
			}
		}

		private void ActionsComboBox_DropDownClosed(object sender, EventArgs e)
		{
			if ((sender as ComboBox)?.DataContext is RevisionEntry selectedItem)
			{
				RevisionListView.SelectedItem = selectedItem;
			}
		}

		private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (e.RemovedItems.Count == 0)
			{
				return;
			}
			e.Handled = true;
			if (_updateInProgress)
			{
				return;
			}
			_updateInProgress = true;
			InteractiveRebaseComboBoxItem interactiveRebaseComboBoxItem = e.AddedItems.FirstItem<InteractiveRebaseComboBoxItem>();
			InteractiveRebaseAction? action = interactiveRebaseComboBoxItem.Action;
			InteractiveRebaseComboBoxItem interactiveRebaseComboBoxItem2 = e.RemovedItems.FirstItem<InteractiveRebaseComboBoxItem>();
			ComboBox comboBox = (ComboBox)sender;
			RevisionEntry revisionEntry = comboBox.DataContext as RevisionEntry;
			RevisionEntry[] array = RevisionListView.SelectedItems.CompactMap((object x) => x as RevisionEntry);
			if (!array.Contains(revisionEntry) && revisionEntry != null)
			{
				array = new RevisionEntry[1] { revisionEntry };
			}
			if (!action.HasValue)
			{
				if (interactiveRebaseComboBoxItem.Title == "Move Up")
				{
					MoveUp(array);
				}
				else if (interactiveRebaseComboBoxItem.Title == "Move Down")
				{
					MoveDown(array);
				}
				comboBox.SelectedItem = interactiveRebaseComboBoxItem2;
				_updateInProgress = false;
			}
			else if (action.GetValueOrDefault() == InteractiveRebaseAction.Reword && array.Length == 1)
			{
				if (interactiveRebaseComboBoxItem2 != null)
				{
					ShowRewordPopup(array.FirstItem());
				}
				_updateInProgress = false;
			}
			else
			{
				RevisionEntry[] array2 = array;
				for (int i = 0; i < array2.Length; i++)
				{
					array2[i].Action = action.Value;
				}
				UpdateTodoList();
				_updateInProgress = false;
			}
		}

		private void PerformAction(IrAction action)
		{
			_updateInProgress = true;
			if (action is IrAction.Squash squash)
			{
				RevisionEntry revisionEntry = null;
				foreach (string item in squash.Shas.Reverse())
				{
					RevisionEntry revisionBySha = GetRevisionBySha(item);
					if (revisionBySha == null)
					{
						return;
					}
					revisionBySha.Action = InteractiveRebaseAction.Squash;
					if (revisionEntry == null)
					{
						revisionEntry = revisionBySha;
					}
				}
				UpdateTodoList();
				SelectAndScrollIntoView(revisionEntry);
			}
			else if (action is IrAction.Fixup fixup)
			{
				RevisionEntry revisionBySha2 = GetRevisionBySha(fixup.Sha);
				if (revisionBySha2 == null)
				{
					return;
				}
				revisionBySha2.Action = InteractiveRebaseAction.Fixup;
				Sha? initialDst = fixup.InitialDst;
				if (initialDst.HasValue)
				{
					int? rowBySha = GetRowBySha(initialDst.GetValueOrDefault().ToString());
					if (rowBySha.HasValue)
					{
						int valueOrDefault = rowBySha.GetValueOrDefault();
						MoveItemToRow(revisionBySha2, valueOrDefault);
					}
				}
				UpdateTodoList();
				SelectAndScrollIntoView(revisionBySha2);
			}
			else if (action is IrAction.Move move)
			{
				RevisionEntry revisionBySha3 = GetRevisionBySha(move.Sha);
				if (revisionBySha3 == null)
				{
					return;
				}
				revisionBySha3.Action = InteractiveRebaseAction.Pick;
				Sha? initialDst = move.InitialDst;
				if (initialDst.HasValue)
				{
					int? rowBySha = GetRowBySha(initialDst.GetValueOrDefault().ToString());
					if (rowBySha.HasValue)
					{
						int valueOrDefault2 = rowBySha.GetValueOrDefault();
						MoveItemToRow(revisionBySha3, valueOrDefault2);
					}
				}
				UpdateTodoList();
				SelectAndScrollIntoView(revisionBySha3);
			}
			else if (action is IrAction.Drop drop)
			{
				RevisionEntry revisionEntry2 = null;
				foreach (string item2 in drop.Shas.Reverse())
				{
					RevisionEntry revisionBySha4 = GetRevisionBySha(item2);
					if (revisionBySha4 == null)
					{
						return;
					}
					revisionBySha4.Action = InteractiveRebaseAction.Drop;
					if (revisionEntry2 == null)
					{
						revisionEntry2 = revisionBySha4;
					}
				}
				UpdateTodoList();
				SelectAndScrollIntoView(revisionEntry2);
			}
			else if (action is IrAction.Reword reword)
			{
				RevisionEntry revisionBySha5 = GetRevisionBySha(reword.Sha);
				if (revisionBySha5 == null)
				{
					return;
				}
				SelectAndScrollIntoView(revisionBySha5);
				ShowRewordPopup(revisionBySha5);
			}
			else if (action is IrAction.Edit edit)
			{
				RevisionEntry revisionBySha6 = GetRevisionBySha(edit.Sha);
				if (revisionBySha6 == null)
				{
					return;
				}
				revisionBySha6.Action = InteractiveRebaseAction.Edit;
				UpdateTodoList();
				SelectAndScrollIntoView(revisionBySha6);
			}
			_updateInProgress = false;
		}

		private void MoveItemToRow(RevisionEntry item, int row)
		{
			RevisionEntry revisionEntry = _todoList[row];
			int num = -1;
			int num2 = -1;
			for (int i = 0; i < _todoList.Count; i++)
			{
				if (_todoList[i] == item)
				{
					num = i;
				}
				if (_todoList[i] == revisionEntry)
				{
					num2 = i;
				}
			}
			if (num != -1 && num2 != -1)
			{
				RevisionEntry[] revisionsToMove = new RevisionEntry[1] { item };
				int num3 = num2 - num;
				if (num3 > 0)
				{
					MoveDown(revisionsToMove, num3 - 1);
				}
				else if (num3 < 0)
				{
					MoveUp(revisionsToMove, Math.Abs(num3));
				}
			}
		}

		private RevisionEntry GetRevisionBySha(string sha)
		{
			for (int i = 0; i < _todoList.Count(); i++)
			{
				if (sha == _todoList[i].Sha)
				{
					return _todoList[i];
				}
			}
			return null;
		}

		private int? GetRowBySha(string sha)
		{
			for (int i = 0; i < _todoList.Count(); i++)
			{
				if (sha == _todoList[i].Sha)
				{
					return i;
				}
			}
			return null;
		}

		private void MoveUp(RevisionEntry[] revisionsToMove, int rowsNumber = 1)
		{
			List<RevisionEntry> list = new List<RevisionEntry>(revisionsToMove.Length);
			int num = -1;
			for (int i = 0; i < _todoList.Count; i++)
			{
				RevisionEntry revisionItem = _todoList[i];
				if (revisionsToMove.ContainsItem((RevisionEntry x) => x == revisionItem))
				{
					if (list.Count == 0)
					{
						num = i;
					}
					_todoList.RemoveAt(i);
					list.Add(revisionItem);
					i--;
				}
			}
			int num2 = Math.Max(num - rowsNumber, 0);
			RevisionListView.SelectedItems.Clear();
			foreach (RevisionEntry item in list)
			{
				_todoList.Insert(num2, item);
				RevisionListView.SelectedItems.Add(item);
				num2++;
			}
			_updateInProgress = true;
			UpdateTodoList();
			_updateInProgress = false;
			RevisionListView.ScrollIntoView(list[0]);
			RevisionListView.FocusSelectedItem();
		}

		private void MoveDown(RevisionEntry[] revisionsToMove, int rowsNumber = 1)
		{
			List<RevisionEntry> list = new List<RevisionEntry>(revisionsToMove.Length);
			int num = -1;
			for (int i = 0; i < _todoList.Count; i++)
			{
				RevisionEntry revisionItem = _todoList[i];
				if (revisionsToMove.ContainsItem((RevisionEntry x) => x == revisionItem))
				{
					if (list.Count == 0)
					{
						num = i;
					}
					_todoList.RemoveAt(i);
					list.Add(revisionItem);
					i--;
				}
			}
			int num2 = Math.Min(num + rowsNumber, _todoList.Count);
			RevisionListView.SelectedItems.Clear();
			foreach (RevisionEntry item in list)
			{
				_todoList.Insert(num2, item);
				RevisionListView.SelectedItems.Add(item);
				num2++;
			}
			_updateInProgress = true;
			UpdateTodoList();
			_updateInProgress = false;
			RevisionListView.ScrollIntoView(list[0]);
			RevisionListView.FocusSelectedItem();
		}

		private void ShowRewordPopup(RevisionEntry revision)
		{
			if (!_suppressRewordDialog)
			{
				if (_adorner == null)
				{
					_adorner = new RewordAdorner(RevisionListView);
					_adorner.Child = CreateAdornerContent(revision);
					AdornerLayer.GetAdornerLayer(RevisionListView)?.Add(_adorner);
				}
				else if (_adorner.Child is RewordUserControl rewordUserControl)
				{
					rewordUserControl.Refresh(revision.Subject, revision.Description);
				}
				if (RevisionListView.ItemContainerGenerator.ContainerFromItem(revision) is ListViewItem listViewItem)
				{
					UpdateAdornerMargin(listViewItem);
					return;
				}
				CloseRewordPopup();
				SelectAndFocusRevision(revision);
			}
		}

		private void UpdateAdornerMargin(ListViewItem listViewItem)
		{
			int num = 130;
			int num2 = 22;
			Point point = listViewItem.TransformToAncestor(RevisionListView).Transform(new Point(num, 0.0));
			_adorner.Margin = new Thickness(num, point.Y + (double)num2, 0.0, 0.0);
		}

		private RewordUserControl CreateAdornerContent(RevisionEntry revision)
		{
			RewordUserControl rewordPopup = new RewordUserControl(revision.Subject, revision.Description);
			rewordPopup.RewordCancelled += delegate
			{
				UpdateTodoList();
				CloseRewordPopup();
				SelectAndFocusRevision(revision);
			};
			rewordPopup.MessageChanged += delegate
			{
				_suppressRewordDialog = true;
				revision.Action = InteractiveRebaseAction.Reword;
				revision.CustomMessage = rewordPopup.Message;
				UpdateTodoList();
				CloseRewordPopup();
				SelectAndFocusRevision(revision);
				_suppressRewordDialog = false;
			};
			return rewordPopup;
		}

		private void CloseRewordPopup()
		{
			if (_adorner != null)
			{
				_adorner.Child = null;
				AdornerLayer.GetAdornerLayer(RevisionListView)?.Remove(_adorner);
				_adorner = null;
			}
		}

		private void SelectAndFocusRevision(RevisionEntry revision)
		{
			RevisionListView.Focus();
			RevisionListView.SelectedItem = revision;
			RevisionListView.FocusSelectedItem();
		}

		private void SelectAndScrollIntoView(RevisionEntry revision)
		{
			RevisionListView.SelectedItem = revision;
			RevisionListView.ScrollIntoView(revision);
		}

		private void IpcMessageHandler(NamedPipeServerStream pipeServer)
		{
			string text = pipeServer.ReadString();
			if (text.StartsWith("prepareTodoListForRebase "))
			{
				string todoListPath = text.Substring("prepareTodoListForRebase ".Length);
				PrepareTodoListForRebase(todoListPath);
			}
			_finishRiProcessSemaphore.WaitOne(Timeout);
			pipeServer.WriteString(_response);
		}

		private void PrepareTodoListForRebase(string todoListPath)
		{
			_todoListPath = todoListPath;
			GitCommandResult<InteractiveRebaseTodoListItem[]> todoListResult = new GetRebaseTodoListCommand().Execute(_gitModule, _todoListPath, _references);
			base.Dispatcher.Invoke(delegate
			{
				if (!todoListResult.Succeeded)
				{
					base.GitResult = todoListResult.ToGitCommandResult();
					Close();
					return;
				}
				SetStatus(ForkPlusDialogStatus.None, "");
				RevisionListFallbackUserControl.Hide();
				_todoList.Clear();
				InteractiveRebaseTodoListItem[] array = todoListResult.Result.Reverse().ToArray();
				for (int i = 0; i < array.Length; i++)
				{
					_todoList.Add(new RevisionEntry(i, array[i]));
				}
				UpdateSubmitButton();
				if (_todoList.Count > 0)
				{
					RevisionListView.Focus();
					RevisionListView.SelectedIndex = 0;
					RevisionListView.FocusRow(0);
				}
				base.Dispatcher.InvokeAsync(delegate
				{
					PerformAction(_initialAction);
					_initialTodoList = _todoList.Map((RevisionEntry e) => (Action: e.Action, Sha: e.Sha)).ToArray();
				}, DispatcherPriority.ContextIdle);
			});
		}

		private void UpdateTodoList()
		{
			List<RevisionEntry> list = new List<RevisionEntry>(_todoList.Count);
			int num = 0;
			foreach (RevisionEntry todo in _todoList)
			{
				todo.Row = num;
				todo.GroupMessage = null;
				if (todo.Action != InteractiveRebaseAction.Reword)
				{
					todo.CustomMessage = null;
				}
				if (todo.Action == InteractiveRebaseAction.Squash || todo.Action == InteractiveRebaseAction.Fixup)
				{
					todo.GraphType = ((list.Count <= 0) ? GraphItemType.Start : GraphItemType.Middle);
					list.Add(todo);
				}
				else if (todo.Action == InteractiveRebaseAction.Drop && list.Count > 0)
				{
					todo.GraphType = GraphItemType.Through;
				}
				else if (list.Count > 0)
				{
					todo.GraphType = GraphItemType.Target;
					list.Add(todo);
					if (list.Any((RevisionEntry x) => x.Action == InteractiveRebaseAction.Squash))
					{
						if (todo.Action == InteractiveRebaseAction.Pick || todo.Action == InteractiveRebaseAction.Reword)
						{
							todo.Action = InteractiveRebaseAction.Reword;
							IEnumerable<string> values = (from x in list
								where x.Action != InteractiveRebaseAction.Fixup
								select x.OriginalMessage).Reverse();
							todo.GroupMessage = string.Join("\n\n", values);
						}
					}
					else if (todo.CustomMessage == null && todo.Action != 0)
					{
						todo.Action = InteractiveRebaseAction.Pick;
					}
					list.Clear();
				}
				else
				{
					if (todo.CustomMessage == null && todo.Action == InteractiveRebaseAction.Reword)
					{
						todo.Action = InteractiveRebaseAction.Pick;
					}
					todo.GraphType = GraphItemType.None;
				}
				num++;
			}
		}

		private void RevisionListViewItem_Drop(object sender, DragEventArgs e)
		{
			if (!(sender is MultiselectionListViewItem { DataContext: RevisionEntry dataContext } multiselectionListViewItem) || !(e.Data.GetData(typeof(RevisionEntry[])) is RevisionEntry[] array) || array.Length == 0 || array.Contains(dataContext))
			{
				return;
			}
			List<RevisionEntry> list = new List<RevisionEntry>(array.Length);
			int num = -1;
			for (int i = 0; i < _todoList.Count; i++)
			{
				RevisionEntry revisionItem = _todoList[i];
				if (revisionItem == dataContext)
				{
					num = i;
				}
				if (array.ContainsItem((RevisionEntry x) => x == revisionItem))
				{
					_todoList.RemoveAt(i);
					list.Add(revisionItem);
					i--;
				}
			}
			int num2 = num;
			if (multiselectionListViewItem.DropPosition == DropPosition.Bottom && num < _todoList.Count)
			{
				num2 = num + 1;
			}
			RevisionListView.SelectedItems.Clear();
			foreach (RevisionEntry item in list)
			{
				_todoList.Insert(num2, item);
				RevisionListView.SelectedItems.Add(item);
				num2++;
			}
			UpdateTodoList();
			RevisionListView.ScrollIntoView(list[0]);
			RevisionListView.FocusSelectedItem();
		}

	}
}
