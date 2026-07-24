// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.Media → using Avalonia.Media
// - SolidColorBrush 解析为 Avalonia.Media.SolidColorBrush（UserColorBrushes.GetBrush 已返回 Avalonia 画刷）
// 注：本文件仅使用 SolidColorBrush（Avalonia.Media 同名类），无 ImageSource/IBrush 接口替换。
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Media;
using ForkPlus.Git;
using ForkPlus.Settings;

namespace ForkPlus.UI
{
	[DebuggerDisplay("{Revision.Sha.ToAbbreviatedString(),nq} {Revision.Subject}")]
	public class DecoratedRevision : IRoundedSelectionListBoxViewModel, INotifyPropertyChanged
	{
		private static readonly PropertyChangedEventArgs AuthorChangedEventArgs = new PropertyChangedEventArgs("Author");

		private static readonly PropertyChangedEventArgs AuthorDateLongStringChangedEventArgs = new PropertyChangedEventArgs("AuthorDateLongString");

		private static readonly PropertyChangedEventArgs AuthorDateShortStringChangedEventArgs = new PropertyChangedEventArgs("AuthorDateShortString");

		private static readonly PropertyChangedEventArgs AuthorNameChangedEventArgs = new PropertyChangedEventArgs("AuthorName");

		private static readonly PropertyChangedEventArgs IsSearchMatchChangedEventArgs = new PropertyChangedEventArgs("IsSearchMatch");

		private static readonly PropertyChangedEventArgs SearchStringChangedEventArgs = new PropertyChangedEventArgs("SearchString");

		private static readonly PropertyChangedEventArgs SubjectChangedEventArgs = new PropertyChangedEventArgs("Subject");

		private static readonly PropertyChangedEventArgs HasBodyChangedEventArgs = new PropertyChangedEventArgs("HasBody");

		private static readonly PropertyChangedEventArgs SubjectSearchStringChangedEventArgs = new PropertyChangedEventArgs("SubjectSearchString");

		private static readonly PropertyChangedEventArgs UserBackgroundBrushChangedEventArgs = new PropertyChangedEventArgs("UserBackgroundBrush");

		private static readonly PropertyChangedEventArgs SelectionTypeChangedEventArgs = new PropertyChangedEventArgs("SelectionType");

		private static readonly UserColorBrushes _userBackgroundBrushes = new UserColorBrushes();

		[Null]
		private string _subjectSearchString;

		[Null]
		private string _searchString;

		private bool _isSearchMatch;

		private SolidColorBrush _userBackgroundBrush;

		private ListBoxSelectionType _selectionType;

		private readonly RevisionVisualGraph _revisionVisualGraph;

		private RevisionHeader? _revisionHeader;

		private byte _authorColorIndex;

		public int Row { get; }

		public Sha Sha => _revisionVisualGraph.GetShaAtRow(Row);

		public bool IsCollapsed => _revisionVisualGraph.IsRowCollapsed(Row);

		public bool IsHead { get; }

		public ActiveBranchCommitStatus UpstreamStatus { get; }

		public bool IsReachable { get; }

		[Null]
		public ReferenceViewModel[] References { get; }

		public GraphInfo GraphInfo { get; }

		[Null]
		public string SubjectSearchString
		{
			get
			{
				if (!IsRevisionHeaderLoaded)
				{
					return null;
				}
				return _subjectSearchString;
			}
			set
			{
				if (!(_subjectSearchString == value))
				{
					_subjectSearchString = value;
					this.PropertyChanged?.Invoke(this, SubjectSearchStringChangedEventArgs);
				}
			}
		}

		[Null]
		public string SearchString
		{
			get
			{
				if (!IsRevisionHeaderLoaded)
				{
					return null;
				}
				return _searchString;
			}
			set
			{
				if (_searchString == value)
				{
					return;
				}
				_searchString = value;
				this.PropertyChanged?.Invoke(this, SearchStringChangedEventArgs);
				if (References != null)
				{
					ReferenceViewModel[] references = References;
					for (int i = 0; i < references.Length; i++)
					{
						references[i].SearchString = value;
					}
				}
			}
		}

		public bool IsSearchMatch
		{
			get
			{
				return _isSearchMatch;
			}
			set
			{
				if (_isSearchMatch != value)
				{
					_isSearchMatch = value;
					this.PropertyChanged?.Invoke(this, IsSearchMatchChangedEventArgs);
				}
			}
		}

		public SolidColorBrush UserBackgroundBrush
		{
			get
			{
				if (_userBackgroundBrush == null)
				{
					_userBackgroundBrush = _userBackgroundBrushes.GetBrush(_authorColorIndex, ForkPlusSettings.Default.Theme);
				}
				return _userBackgroundBrush;
			}
			set
			{
				if (_userBackgroundBrush != value)
				{
					_userBackgroundBrush = value;
					this.PropertyChanged?.Invoke(this, UserBackgroundBrushChangedEventArgs);
				}
			}
		}

		public ListBoxSelectionType SelectionType
		{
			get
			{
				return _selectionType;
			}
			set
			{
				if (_selectionType != value)
				{
					_selectionType = value;
					this.PropertyChanged?.Invoke(this, SelectionTypeChangedEventArgs);
				}
			}
		}

		public bool IsRevisionHeaderLoaded => _revisionHeader.HasValue;

		public string Subject => _revisionHeader?.Message ?? "";

		public bool HasBody => _revisionHeader?.HasBody ?? false;

		[Null]
		public UserIdentity Author => _revisionHeader?.Author;

		public string AuthorName => _revisionHeader?.Author.Name ?? "";

		public string AbbreviatedSha => Sha.ToAbbreviatedString();

		public string AuthorDateLongString => _revisionHeader?.AuthorDate.ToString("d MMM yyyy HH:mm") ?? "";

		public string AuthorDateShortString => _revisionHeader?.AuthorDate.ToString("d MMM yyyy") ?? "";

		public event PropertyChangedEventHandler PropertyChanged;

		public DecoratedRevision(int row, RevisionVisualGraph revisionVisualGraph, bool isHead, [Null] ReferenceViewModel[] references, bool isReachable, ActiveBranchCommitStatus upstreamStatus, GraphInfo graphInfo, bool searchMatch, [Null] string subjectSearchString, [Null] string searchString)
		{
			Row = row;
			_revisionVisualGraph = revisionVisualGraph;
			References = references;
			IsHead = isHead;
			UpstreamStatus = upstreamStatus;
			IsReachable = isReachable;
			GraphInfo = graphInfo;
			_isSearchMatch = searchMatch;
			_subjectSearchString = subjectSearchString;
			_searchString = searchString;
			_userBackgroundBrush = _userBackgroundBrushes.GetBrush(_authorColorIndex, ForkPlusSettings.Default.Theme);
			if (References != null)
			{
				ReferenceViewModel[] references2 = References;
				for (int i = 0; i < references2.Length; i++)
				{
					references2[i].SearchString = searchString;
				}
			}
		}

		public bool IsStash()
		{
			return _revisionVisualGraph.IsStash(Row);
		}

		public StashRevision ToStashRevision()
		{
			return _revisionVisualGraph.GetStashRevisionAtRow(Row);
		}

		[Null]
		public Revision ToRevision()
		{
			if (!_revisionHeader.HasValue)
			{
				return null;
			}
			return new Revision(Sha, _revisionHeader.Value);
		}

		public StashRevision AsStashRevision()
		{
			return _revisionVisualGraph.GetStashRevisionAtRow(Row);
		}

		public ShaBufferIterator GetParents()
		{
			return _revisionVisualGraph.GetParentsAtRow(Row);
		}

		public void SetRevisionHeader(RevisionHeader revisionHeader, byte authorColorIndex)
		{
			_revisionHeader = revisionHeader;
			_authorColorIndex = authorColorIndex;
			_userBackgroundBrush = null;
			PropertyChangedEventHandler propertyChanged = this.PropertyChanged;
			if (propertyChanged != null)
			{
				propertyChanged(this, HasBodyChangedEventArgs);
				propertyChanged(this, SubjectChangedEventArgs);
				propertyChanged(this, AuthorChangedEventArgs);
				propertyChanged(this, AuthorNameChangedEventArgs);
				propertyChanged(this, AuthorDateShortStringChangedEventArgs);
				propertyChanged(this, AuthorDateLongStringChangedEventArgs);
				propertyChanged(this, UserBackgroundBrushChangedEventArgs);
				propertyChanged(this, SubjectSearchStringChangedEventArgs);
				propertyChanged(this, SearchStringChangedEventArgs);
			}
		}

		public void RefreshTheme()
		{
			_userBackgroundBrush = null;
			ReferenceViewModel[] references = References;
			if (references == null)
			{
				return;
			}
			ReferenceViewModel[] array = references;
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i] is BranchViewModel branchViewModel)
				{
					branchViewModel.RefreshBrushes();
				}
			}
		}
	}
}
