using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Media;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI;

// Avalonia spike 版 DecoratedRevision（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/DecoratedRevision.cs（283 行）：
//   - WPF: public class DecoratedRevision : IRoundedSelectionListBoxViewModel, INotifyPropertyChanged
//   - [DebuggerDisplay("{Revision.Sha.ToAbbreviatedString(),nq} {Revision.Subject}")]
//   - 10+ static PropertyChangedEventArgs 字段
//   - UserColorBrushes _userBackgroundBrushes（spike 已创建）
//   - 属性：Row / Sha / IsCollapsed / IsHead / UpstreamStatus / IsReachable /
//     References / GraphInfo / SubjectSearchString / SearchString / IsSearchMatch /
//     UserBackgroundBrush / SelectionType / IsRevisionHeaderLoaded / Subject / HasBody /
//     Author / AuthorName / AbbreviatedSha / AuthorDateLongString / AuthorDateShortString
//   - 方法：IsStash / ToStashRevision / ToRevision / AsStashRevision / GetParents /
//     SetRevisionHeader / RefreshTheme
//   - 依赖：RevisionVisualGraph / RevisionHeader / Revision / UserIdentity /
//     ActiveBranchCommitStatus / GraphInfo / Sha / ShaBufferIterator / ListBoxSelectionType /
//     ForkPlusSettings / UserColorBrushes（spike 已创建）/ IRoundedSelectionListBoxViewModel（spike 已创建）
//
// Avalonia 版差异（spike 简化策略）：
//   1. WPF System.Windows.Media.SolidColorBrush → Avalonia.Media.SolidColorBrush
//   2. WPF INotifyPropertyChanged → System.ComponentModel（跨平台）
//   3. WPF [Null] Attribute → spike 跳过（nullable disable in csproj）
//   4. 其余 Git 类型来自 ForkPlus.Core（Avalonia 工程已引用）
//
// spike 简化（task spec 关键 API）：
//   - 实现 IRoundedSelectionListBoxViewModel + INotifyPropertyChanged
//   - 所有属性 + SetRevisionHeader / RefreshTheme
namespace ForkPlus.Avalonia
{
    [DebuggerDisplay("{Sha.ToAbbreviatedString(),nq}")]
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

        private string _subjectSearchString;
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

        public ReferenceViewModel[] References { get; }

        public GraphInfo GraphInfo { get; }

        public string SubjectSearchString
        {
            get
            {
                if (!IsRevisionHeaderLoaded) return null;
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

        public string SearchString
        {
            get
            {
                if (!IsRevisionHeaderLoaded) return null;
                return _searchString;
            }
            set
            {
                if (_searchString == value) return;
                _searchString = value;
                this.PropertyChanged?.Invoke(this, SearchStringChangedEventArgs);
                if (References != null)
                {
                    for (int i = 0; i < References.Length; i++)
                    {
                        References[i].SearchString = value;
                    }
                }
            }
        }

        public bool IsSearchMatch
        {
            get => _isSearchMatch;
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
            get => _selectionType;
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

        public UserIdentity Author => _revisionHeader?.Author;

        public string AuthorName => _revisionHeader?.Author.Name ?? "";

        public string AbbreviatedSha => Sha.ToAbbreviatedString();

        public string AuthorDateLongString => _revisionHeader?.AuthorDate.ToString("d MMM yyyy HH:mm") ?? "";

        public string AuthorDateShortString => _revisionHeader?.AuthorDate.ToString("d MMM yyyy") ?? "";

        public event PropertyChangedEventHandler PropertyChanged;

        public DecoratedRevision(int row, RevisionVisualGraph revisionVisualGraph, bool isHead,
            ReferenceViewModel[] references, bool isReachable, ActiveBranchCommitStatus upstreamStatus,
            GraphInfo graphInfo, bool searchMatch, string subjectSearchString, string searchString)
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
                for (int i = 0; i < References.Length; i++)
                {
                    References[i].SearchString = searchString;
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

        public Revision ToRevision()
        {
            if (!_revisionHeader.HasValue) return null;
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
            if (References == null) return;
            for (int i = 0; i < References.Length; i++)
            {
                if (References[i] is BranchViewModel branchViewModel)
                {
                    branchViewModel.RefreshBrushes();
                }
            }
        }
    }
}
