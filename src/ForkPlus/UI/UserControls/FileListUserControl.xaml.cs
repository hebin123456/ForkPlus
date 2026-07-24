// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Interactivity（RoutedEventArgs）
// - using System.Windows.Controls → using Avalonia.Controls（SelectionMode/SizeChangedEventArgs/SelectionChangedEventArgs）
// - using System.Windows.Input → using Avalonia.Input（DoubleTapped/TappedEventArgs）
// - using System.Windows.Markup → 移除
// - using System.Windows.Media → using Avalonia.Media（IImage）
// - using System.Windows.Media.Imaging → using Avalonia.Media.Imaging（Bitmap）+ using Avalonia.Platform（AssetLoader）
// - 新增 using Avalonia.Controls.Templates（IDataTemplate，替代 DataTemplate）
// - BitmapImage(pack:// URI) → AssetLoader.Open(avares:// URI) + new Bitmap(stream)（参考 AvatarManager）
// - ImageSource → IImage（IconTools.GetImageSourceForExtension 已返回 IImage）
// - DependencyProperty.RegisterAttached + PropertyMetadata 回调 → StyledProperty + OnPropertyChanged override（EnableMultiSelection 仅作普通属性用于 XAML）
// - DependencyPropertyChangedEventArgs → AvaloniaPropertyChangedEventArgs
// - MouseDoubleClick + MouseButtonEventArgs → DoubleTapped + RoutedEventArgs（参考 MultiselectionTreeView.OnDoubleTapped）
// - Delegate.Combine(field, handler) → field += handler
// - ActualWidth(Control) → Bounds.Width（参考 ModernTabControl）；GridViewColumn.ActualWidth 保留（GridView 兼容层属性）
// - (DataTemplate)base.Resources[key] → (IDataTemplate)base.Resources[key]（Avalonia ItemsControl.ItemTemplate 为 IDataTemplate）
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;

namespace ForkPlus.UI.UserControls
{
	public partial class FileListUserControl : UserControl
	{
		public class ChangedFileEqualityComparer : IEqualityComparer<ChangedFile>
		{
			public bool Equals(ChangedFile x, ChangedFile y)
			{
				if (x.Path == y.Path && x.ChangeType == y.ChangeType)
				{
					return x.TreeIsh == y.TreeIsh;
				}
				return false;
			}

			public int GetHashCode(ChangedFile obj)
			{
				return 17 + obj.Path.GetHashCode() * 31 + obj.ChangeType.GetHashCode() * 31;
			}
		}

		public EventHandler<FileListEventArgs> ItemDoubleClick;

		public EventHandler<FileListTreeView.DropEventArgs> ItemsDrop;

		public EventHandler<FileListEventArgs> SelectionChanged;

		private bool _stopSelectionChangedEvents;

		private const double GridViewColumnMinWidth = 120.0;

		private const int BulkRebuildChangeThreshold = 256;

		// 变更文件数达到此阈值时，重建走后台线程构建（Task.Run）并跳过选中项恢复，
		// 避免 UI 冻结。注意：不再据此把 Tree 降级为 List——变更多时也应保持树状显示。
		private const int LargeFileListBackgroundBuildThreshold = 5000;

		// 阶段 4.5：WPF BitmapImage(pack://application URI) → Avalonia AssetLoader.Open(avares:// URI) + new Bitmap(stream)（参考 AvatarManager）。
		private static readonly Bitmap FolderIcon = LoadFolderIcon();

		// 阶段 4.5：WPF ImageSource → Avalonia IImage（IconTools.GetImageSourceForExtension 已返回 IImage）。
		private static readonly ChangedFileEqualityComparer _changedFileEqualityComparer = new ChangedFileEqualityComparer();

		private readonly Dictionary<string, IImage> _fileIconCache = new Dictionary<string, IImage>(StringComparer.OrdinalIgnoreCase);

		private ChangedFile[] _rawChangedFiles;

		// 阶段 4.5：WPF DependencyProperty.RegisterAttached + PropertyMetadata 回调 → Avalonia StyledProperty<bool> + OnPropertyChanged override。
		// EnableMultiSelection 在 XAML 中作为普通属性使用（参考 StageFileUserControl.xaml），无需 AttachedProperty。
		public static readonly StyledProperty<bool> EnableMultiSelectionProperty = AvaloniaProperty.Register<FileListUserControl, bool>(nameof(EnableMultiSelection));

		private FileListMode _mode;

		private bool _locationColumnHeaderLoaded;

		private bool _restoringColumnWidth;

		public bool EnableMultiSelection
		{
			get
			{
				return GetValue(EnableMultiSelectionProperty);
			}
			set
			{
				SetValue(EnableMultiSelectionProperty, value);
			}
		}

		public FileListMode Mode
		{
			get
			{
				return _mode;
			}
			set
			{
				if (_mode != value)
				{
					_mode = value;
					RefreshTreeViewItemTemplate();
				}
			}
		}

		public bool ContainsVisibleItems => TreeView.RootItem.Children.ContainsItem((MultiselectionTreeViewItem x) => !x.IsHidden);

		public ChangedFile[] ExpandedItems => ExpandItems(TreeView.RootItem.Children);

		public ChangedFile[] Items => _rawChangedFiles;

		public ChangedFile[] ExpandedSelectedItems => ExpandItems(TreeView.SelectedItems);

		public ChangedFile[] SelectedItems
		{
			get
			{
				IList selectedItems = TreeView.SelectedItems;
				List<ChangedFile> list = new List<ChangedFile>(selectedItems.Count);
				foreach (object item in selectedItems)
				{
					ChangedFile changedFile = (item as FileListItem)?.ChangedFile;
					if (changedFile != null)
					{
						list.Add(changedFile);
					}
				}
				return list.ToArray();
			}
		}

		public string FilterString
		{
			get
			{
				return TreeView.FilterString;
			}
			set
			{
				TreeView.FilterString = value;
			}
		}

		public event EventHandler ColumnHeaderSizeChanged;

		[Null]
		private static Bitmap LoadFolderIcon()
		{
			// 阶段 4.5：WPF pack://application URI + BitmapImage → Avalonia avares:// URI + AssetLoader.Open + Bitmap（参考 AvatarManager）。
			using (Stream stream = AssetLoader.Open(new Uri("avares://ForkPlus/Assets/Folder.png")))
			{
				return new Bitmap(stream);
			}
		}

		public FileListUserControl()
		{
			InitializeComponent();
			TreeView.SelectionChanged += TreeViewSelectionChanged;
			// 阶段 4.5：WPF MouseDoubleClick → Avalonia DoubleTapped（参考 MultiselectionTreeView.OnDoubleTapped）。
			TreeView.DoubleTapped += TreeView_MouseDoubleClick;
			// 阶段 4.5：WPF Delegate.Combine(field, handler) → Avalonia field += handler。
			TreeView.ItemsDrop += TreeView_ItemsDrop;
			TreeView.RootItem = new FileListItem(new ChangedFile("", staged: true), "", null);
			RefreshTreeViewItemTemplate();
		}

		// 阶段 4.5：WPF OnPropertyChanged(DependencyPropertyChangedEventArgs) → Avalonia OnPropertyChanged(AvaloniaPropertyChangedEventArgs)。
		// 替代原 RegisterAttached 的 PropertyMetadata 回调（EnableMultiSelectionPropertyChanged）。
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == EnableMultiSelectionProperty)
			{
				if (change.NewValue is bool enableMulti && enableMulti)
				{
					TreeView.SelectionMode = SelectionMode.Extended;
				}
				else
				{
					TreeView.SelectionMode = SelectionMode.Single;
				}
			}
		}

		private void RefreshTreeViewItemTemplate()
		{
			switch (Mode)
			{
			case FileListMode.List:
				// 阶段 4.5：WPF (DataTemplate)base.Resources[key] → Avalonia (IDataTemplate)base.Resources[key]（ItemTemplate 为 IDataTemplate）。
				TreeView.ItemTemplate = (IDataTemplate)base.Resources["ListViewTemplate"];
				TreeView.Style = Theme.FileListMultiselectionTreeView.DefaultStyle;
				break;
			case FileListMode.Tree:
				TreeView.ItemTemplate = (IDataTemplate)base.Resources["TreeViewTemplate"];
				TreeView.Style = Theme.FileListMultiselectionTreeView.DefaultStyle;
				break;
			case FileListMode.CombinedList:
				TreeView.ItemTemplate = (IDataTemplate)base.Resources["ListViewTemplate"];
				TreeView.Style = Theme.FileListMultiselectionTreeView.GridViewStyle;
				break;
			}
		}

		private ChangedFile[] ExpandItems(IEnumerable items)
		{
			HashSet<ChangedFile> hashSet = new HashSet<ChangedFile>();
			foreach (FileListItem item in items)
			{
				if (item.IsHidden)
				{
					continue;
				}
				if (Mode == FileListMode.Tree)
				{
					ExpandItem(item, hashSet);
					continue;
				}
				ChangedFile changedFile = item.ChangedFile;
				if (changedFile != null)
				{
					hashSet.Add(changedFile);
				}
			}
			return hashSet.ToArray();
		}

		private void ExpandItem(FileListItem item, HashSet<ChangedFile> result)
		{
			if (item.HasChildren)
			{
				foreach (FileListItem child in item.Children)
				{
					if (!child.IsHidden)
					{
						ExpandItem(child, result);
					}
				}
				return;
			}
			result.Add(item.ChangedFile);
		}

		private void TreeViewSelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			e.Handled = true;
			if (!_stopSelectionChangedEvents)
			{
				FileListItem[] array = TreeView.SelectedItems.CompactMap((object x) => x as FileListItem);
				MultiselectionTreeViewItem[] items = array;
				items.RefreshSelectionType();
				ChangedFile selectedItem = ((array.Length != 0) ? array[0].ChangedFile : null);
				SelectionChanged?.Invoke(this, new FileListEventArgs(selectedItem));
			}
		}

		private void TreeView_MouseDoubleClick(object sender, RoutedEventArgs e)
		{
			if (TreeView.LastClickedItem is FileListItem fileListItem)
			{
				ItemDoubleClick?.Invoke(this, new FileListEventArgs(fileListItem.ChangedFile));
			}
		}

		private void TreeView_ItemsDrop(object sender, FileListTreeView.DropEventArgs e)
		{
			ItemsDrop?.Invoke(this, e);
		}

		public void SetItemSource(ChangedFile[] source, bool forceRefresh, bool restoreSelection)
		{
			using (TreeView.LockUpdates())
			{
				FileListItem[] selectedItemsToRestore = TreeView.SelectedItems.CompactMap((object x) => x as FileListItem);
				(ChangedFile[], ChangedFile[]) tuple = ArrayDiff.Diff(_rawChangedFiles, source, ChangedFile.Comparer);
				ChangedFile[] item = tuple.Item1;
				ChangedFile[] item2 = tuple.Item2;
				_rawChangedFiles = source;
				bool shouldRebuild = forceRefresh || item.Length + item2.Length > BulkRebuildChangeThreshold;
				bool isLargeRebuild = shouldRebuild && source.Length >= LargeFileListBackgroundBuildThreshold;
				if (!shouldRebuild)
				{
					ApplyAddedEntries(Mode, item2);
					ApplyRemovedEntries(Mode, item);
				}
				else
				{
					if (restoreSelection)
					{
						_stopSelectionChangedEvents = true;
					}
					RebuildItems(source);
					_stopSelectionChangedEvents = false;
				}
				if (restoreSelection && !isLargeRebuild)
				{
					Select(selectedItemsToRestore);
				}
				RefilterIfNeeded();
			}
		}

		public async Task SetItemSourceAsync(ChangedFile[] source, bool forceRefresh, bool restoreSelection)
		{
			FileListItem[] selectedItemsToRestore = TreeView.SelectedItems.CompactMap((object x) => x as FileListItem);
			(ChangedFile[], ChangedFile[]) tuple = ArrayDiff.Diff(_rawChangedFiles, source, ChangedFile.Comparer);
			ChangedFile[] item = tuple.Item1;
			ChangedFile[] item2 = tuple.Item2;
			_rawChangedFiles = source;
			bool shouldRebuild = forceRefresh || item.Length + item2.Length > BulkRebuildChangeThreshold;
			bool isLargeRebuild = shouldRebuild && source.Length >= LargeFileListBackgroundBuildThreshold;
			if (!shouldRebuild)
			{
				using (TreeView.LockUpdates())
				{
					ApplyAddedEntries(Mode, item2);
					ApplyRemovedEntries(Mode, item);
					if (restoreSelection)
					{
						Select(selectedItemsToRestore);
					}
					RefilterIfNeeded();
				}
				return;
			}
			FileListItem rootItem;
			if (isLargeRebuild)
			{
				// 大列表仍走后台线程构建以避免 UI 冻结，但不再把 Tree 降级为 List——
				// 变更多时也应保持树状显示（用户明确要求"改回树状"）。
				Dictionary<string, IImage> fileIcons = CreateFileIconCache(source);
				rootItem = await Task.Run(() => BuildRootItem(source, Mode, fileIcons));
			}
			else
			{
				rootItem = BuildRootItem(source, Mode, CreateFileIconCache(source), FolderIcon);
			}
			using (TreeView.LockUpdates())
			{
				if (restoreSelection)
				{
					_stopSelectionChangedEvents = true;
				}
				TreeView.RootItem = rootItem;
				_stopSelectionChangedEvents = false;
				if (restoreSelection && !isLargeRebuild)
				{
					Select(selectedItemsToRestore);
				}
				RefilterIfNeeded();
			}
		}

		private void RebuildItems(ChangedFile[] source)
		{
			TreeView.RootItem = BuildRootItem(source, Mode, CreateFileIconCache(source), FolderIcon);
		}

		private void RefilterIfNeeded()
		{
			if (!string.IsNullOrEmpty(TreeView.FilterString))
			{
				TreeView.Refilter();
			}
		}

		private static FileListItem BuildRootItem(ChangedFile[] source, FileListMode mode, Dictionary<string, IImage> fileIcons, IImage folderIcon = null)
		{
			FileListItem rootItem = new FileListItem(new ChangedFile("", staged: true), "", null);
			ApplyAddedEntries(rootItem, mode, source, fileIcons, folderIcon);
			return rootItem;
		}

		private Dictionary<string, IImage> CreateFileIconCache(ChangedFile[] source)
		{
			foreach (ChangedFile changedFile in source)
			{
				string extension = Path.GetExtension(changedFile.Path) ?? "";
				if (!_fileIconCache.ContainsKey(extension))
				{
					_fileIconCache[extension] = IconTools.GetImageSourceForExtension(extension);
				}
			}
			return _fileIconCache;
		}

		public void FocusSelectedElement()
		{
			TreeView.FocusSelectedItem();
		}

		public void Refresh()
		{
			if (_rawChangedFiles != null)
			{
				SetItemSource(_rawChangedFiles, forceRefresh: true, restoreSelection: true);
			}
		}

		public void SelectPreviousFile()
		{
			FileListItem fileListItem = (TreeView.SelectedItem as FileListItem)?.Previous() as FileListItem;
			int num = 0;
			while (fileListItem != null && num < 10)
			{
				if (!fileListItem.IsDirectory)
				{
					TreeView.SelectedItems.Clear();
					TreeView.SelectAndFocus(fileListItem);
					break;
				}
				fileListItem = fileListItem.Previous() as FileListItem;
				num++;
			}
		}

		public void SelectNextFile()
		{
			FileListItem fileListItem = (TreeView.SelectedItem as FileListItem)?.Next() as FileListItem;
			int num = 0;
			while (fileListItem != null && num < 10)
			{
				if (!fileListItem.IsDirectory)
				{
					TreeView.SelectedItems.Clear();
					TreeView.SelectAndFocus(fileListItem);
					break;
				}
				fileListItem = fileListItem.Next() as FileListItem;
				num++;
			}
		}

		public void SelectFile(string filePath)
		{
			SelectFile(TreeView.RootItem as FileListItem, filePath);
		}

		private bool SelectFile(FileListItem parent, string filePath)
		{
			foreach (FileListItem child in parent.Children)
			{
				if (child.IsDirectory)
				{
					if (SelectFile(child, filePath))
					{
						return true;
					}
				}
				else if (child.ChangedFile.Path == filePath)
				{
					TreeView.SelectAndFocus(child);
					TreeView.ScrollIntoView(child);
					return true;
				}
			}
			return false;
		}

		public bool SelectFirstAvailableFile()
		{
			return SelectFirstAvailableFile(TreeView.RootItem as FileListItem);
		}

		private bool SelectFirstAvailableFile([Null] FileListItem parent)
		{
			if (parent == null)
			{
				return false;
			}
			if (parent.Children.Count > 0)
			{
				foreach (MultiselectionTreeViewItem child in parent.Children)
				{
					if (child is FileListItem fileListItem)
					{
						if (!fileListItem.ChangedFile.IsDirectory)
						{
							Select(new FileListItem[1] { fileListItem });
							return true;
						}
						if (SelectFirstAvailableFile(fileListItem))
						{
							return true;
						}
					}
				}
			}
			return false;
		}

		private void Select(FileListItem[] selectedItemsToRestore)
		{
			if (selectedItemsToRestore.Length != 0)
			{
				_stopSelectionChangedEvents = true;
			}
			TreeView.SelectedItems.Clear();
			_stopSelectionChangedEvents = false;
			if (selectedItemsToRestore.Length == 0)
			{
				return;
			}
			SelectItems(TreeView.RootItem.Children, selectedItemsToRestore);
			if (TreeView.SelectedItems.Count > 0)
			{
				return;
			}
			FileListItem fileListItem = selectedItemsToRestore.Last();
			string[] pathComponents = fileListItem.ChangedFile.Path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
			FileListItem fileListItem2 = FindNextItem(TreeView.RootItem.Children, pathComponents, fileListItem.ChangedFile.Path);
			if (fileListItem2 != null)
			{
				FileListItem fileListItem3 = fileListItem2;
				int num = 0;
				while (fileListItem3 != null && num < 10)
				{
					if (!fileListItem3.IsDirectory)
					{
						TreeView.SelectAndFocus(fileListItem3);
						return;
					}
					fileListItem3 = fileListItem3.Next() as FileListItem;
					num++;
				}
				FileListItem fileListItem4 = fileListItem2;
				int num2 = 0;
				while (fileListItem4 != null && num2 < 10)
				{
					if (!fileListItem4.IsDirectory)
					{
						TreeView.SelectAndFocus(fileListItem4);
						return;
					}
					fileListItem4 = fileListItem4.Previous() as FileListItem;
					num2++;
				}
			}
			SelectLastAvailableFile();
		}

		private void SelectLastAvailableFile()
		{
			FileListItem fileListItem = FindLastItem(TreeView.RootItem.Children);
			if (fileListItem != null)
			{
				TreeView.SelectAndFocus(fileListItem);
			}
		}

		private FileListItem FindLastItem(IList<MultiselectionTreeViewItem> items)
		{
			if (items.Count > 0)
			{
				FileListItem fileListItem = items[items.Count - 1] as FileListItem;
				if (fileListItem.IsDirectory)
				{
					return FindLastItem(fileListItem.Children);
				}
				return fileListItem;
			}
			return null;
		}

		private void SelectItems(IList<MultiselectionTreeViewItem> items, FileListItem[] itemsToFind)
		{
			foreach (FileListItem item in items)
			{
				if (item.IsDirectory)
				{
					SelectItems(item.Children, itemsToFind);
				}
				foreach (FileListItem fileListItem2 in itemsToFind)
				{
					if (item.ChangedFile.Path == fileListItem2.ChangedFile.Path)
					{
						TreeView.SelectedItems.Add(item);
					}
				}
			}
		}

		private FileListItem FindNextItem(IList<MultiselectionTreeViewItem> allItems, string[] pathComponents, string path)
		{
			if (Mode == FileListMode.List || Mode == FileListMode.CombinedList)
			{
				foreach (FileListItem allItem in allItems)
				{
					if (allItem.ChangedFile.Path.CompareTo(path) > 0)
					{
						return allItem;
					}
				}
				return null;
			}
			if (Mode == FileListMode.Tree)
			{
				string text = pathComponents.FirstOrDefault();
				if (text == null)
				{
					return allItems.FirstOrDefault() as FileListItem;
				}
				bool flag = false;
				foreach (FileListItem allItem2 in allItems)
				{
					if (flag)
					{
						return allItem2;
					}
					if (allItem2.IsDirectory)
					{
						if (pathComponents.Length <= 1)
						{
							continue;
						}
						if (allItem2.Title == text)
						{
							FileListItem fileListItem3 = FindNextItem(allItem2.Children, pathComponents.Skip(1).ToArray(), path);
							if (fileListItem3 != null)
							{
								return fileListItem3;
							}
							flag = true;
						}
						else if (allItem2.Title.CompareTo(text) > 0)
						{
							return allItem2;
						}
					}
					else
					{
						if (pathComponents.Length > 1)
						{
							return allItem2;
						}
						if (allItem2.Title.CompareTo(text) > 0)
						{
							return allItem2;
						}
					}
				}
			}
			return null;
		}

		private void ApplyAddedEntries(FileListMode mode, ChangedFile[] addedEntries)
		{
			ApplyAddedEntries(TreeView.RootItem as FileListItem, mode, addedEntries, null, FolderIcon);
		}

		private static void ApplyAddedEntries(FileListItem fileListItem, FileListMode mode, ChangedFile[] addedEntries, Dictionary<string, IImage> fileIcons, IImage folderIcon = null)
		{
			foreach (ChangedFile changedFile in addedEntries)
			{
				FileListItem parent = fileListItem;
				string name;
				switch (mode)
				{
				case FileListMode.Tree:
				{
					string[] array = changedFile.Path.Split('/');
					int num = array.Length - 1;
					if (num > 0)
					{
						for (int j = 0; j < array.Length - 1; j++)
						{
							parent = FindOrCreateFolder(parent, array[j], changedFile.Staged, folderIcon);
						}
					}
					name = array[num];
					break;
				}
				case FileListMode.List:
				case FileListMode.CombinedList:
					name = changedFile.Path;
					break;
				default:
					throw new InvalidOperationException();
				}
				IImage imageSourceForExtension = GetFileIcon(changedFile.Path, fileIcons);
				FileListItem newItem = new FileListItem(changedFile, name, imageSourceForExtension);
				AddChild(parent, newItem);
			}
		}

		private static IImage GetFileIcon(string path, Dictionary<string, IImage> fileIcons)
		{
			string extension = Path.GetExtension(path) ?? "";
			if (fileIcons != null && fileIcons.TryGetValue(extension, out IImage imageSource))
			{
				return imageSource;
			}
			return IconTools.GetImageSourceForExtension(extension);
		}

		private void ApplyRemovedEntries(FileListMode mode, ChangedFile[] removedEntries)
		{
			FileListItem rootItem = TreeView.RootItem as FileListItem;
			foreach (ChangedFile changedFile in removedEntries)
			{
				FileListItem parent = rootItem;
				bool folderFound = true;
				if (mode == FileListMode.Tree)
				{
					string[] pathParts = changedFile.Path.Split('/');
					for (int i = 0; i < pathParts.Length - 1; i++)
					{
						parent = FindFolder(parent, pathParts[i]);
						if (parent == null)
						{
							Log.Warn("Can't find folder '" + pathParts[i] + "' for '" + changedFile.Path + "' in current folder structure.");
							folderFound = false;
							break;
						}
					}
				}
				if (folderFound)
				{
					DeleteItem(parent, changedFile);
				}
			}
		}

		private static void DeleteItem(FileListItem parent, ChangedFile changedFile)
		{
			foreach (FileListItem child in parent.Children)
			{
				if (_changedFileEqualityComparer.Equals(child.ChangedFile, changedFile))
				{
					parent.Children.Remove(child);
					if (parent.ParentItem != null && parent.Children.Count == 0)
					{
						DeleteItem(parent.ParentItem as FileListItem, parent.ChangedFile);
					}
					break;
				}
			}
		}

		private static FileListItem FindFolder(FileListItem parent, string name)
		{
			foreach (MultiselectionTreeViewItem child in parent.Children)
			{
				if (child.Title == name)
				{
					return child as FileListItem;
				}
			}
			return null;
		}

		private static void AddChild(FileListItem parent, FileListItem newItem)
		{
			int index = BinarySearch(parent.Children, newItem);
			parent.Children.Insert(index, newItem);
		}

		private static FileListItem FindOrCreateFolder(FileListItem parent, string name, bool staged, IImage folderIcon)
		{
			if (parent.Children.FirstOrDefault((MultiselectionTreeViewItem x) => x.Title == name && (x as FileListItem).IsDirectory) is FileListItem result)
			{
				return result;
			}
			FileListItem fileListItem = new FileListItem(new ChangedFile(Path.Combine(parent.ChangedFile.Path, name), staged), name, folderIcon);
			int index = BinarySearch(parent.Children, fileListItem);
			parent.Children.Insert(index, fileListItem);
			parent.IsExpanded = true;
			fileListItem.IsExpanded = true;
			return fileListItem;
		}

		private static int BinarySearch(MultiselectionTreeViewItemCollection items, FileListItem item)
		{
			int num = 0;
			int num2 = items.Count;
			while (num != num2)
			{
				int num3 = (num + num2) / 2;
				if (FileListItem.ByTypeThenByTitlePredicate(items[num3] as FileListItem, item))
				{
					num = num3 + 1;
				}
				else
				{
					num2 = num3;
				}
			}
			return num;
		}

		public void ClearSelection()
		{
			TreeView.SelectedItems.Clear();
		}

		private void TreeView_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			RefreshGridViewColumnsWidth();
		}

		private void GridViewColumnHeader_SizeChanged(object sender, SizeChangedEventArgs e)
		{
			if (e.NewSize.Width < 120.0)
			{
				e.Handled = true;
				((GridViewColumnHeader)sender).Column.Width = 120.0;
			}
			RefreshGridViewColumnsWidth();
			SaveGridViewColumnWidth();
			this.ColumnHeaderSizeChanged?.Invoke(this, EventArgs.Empty);
		}

		private void LocationColumnHeader_Loaded(object sender, RoutedEventArgs e)
		{
			RestoreGridViewColumnWidth();
			_locationColumnHeaderLoaded = true;
		}

		private void RefreshGridViewColumnsWidth()
		{
			if (_locationColumnHeaderLoaded && !_restoringColumnWidth)
			{
				int num = 55;
				GridView gridView = TreeView.View as GridView;
				// 阶段 4.5：WPF TreeView.ActualWidth → Avalonia TreeView.Bounds.Width（参考 ModernTabControl）。
				double actualWidth = TreeView.Bounds.Width;
				double actualWidth2 = gridView.Columns[0].ActualWidth;
				double num2 = actualWidth - actualWidth2;
				if (num2 < (double)num)
				{
					gridView.Columns[0].Width = actualWidth - (double)num;
					gridView.Columns[1].Width = num;
				}
				else
				{
					gridView.Columns[1].Width = num2;
				}
			}
		}

		public void RestoreGridViewColumnWidth()
		{
			_restoringColumnWidth = true;
			GridView obj = TreeView.View as GridView;
			// 阶段 4.5：WPF TreeView.ActualWidth → Avalonia TreeView.Bounds.Width（参考 ModernTabControl）。
			double actualWidth = TreeView.Bounds.Width;
			double num = ForkPlusSettings.Default.CommitViewCombinedListLocationColumnWidth;
			double num2 = actualWidth - num;
			if (num2 < 120.0)
			{
				num2 = 120.0;
				num = actualWidth - num2;
			}
			obj.Columns[0].Width = num2;
			obj.Columns[1].Width = num;
			_restoringColumnWidth = false;
		}

		private void SaveGridViewColumnWidth()
		{
			if (_locationColumnHeaderLoaded && !_restoringColumnWidth)
			{
				GridView gridView = TreeView.View as GridView;
				ForkPlusSettings.Default.CommitViewCombinedListLocationColumnWidth = gridView.Columns[1].Width;
				ForkPlusSettings.Default.Save();
			}
		}

	}
}
