using System.ComponentModel;
using System.Diagnostics;
using ForkPlus.Git;

// Avalonia spike 版 ReferenceViewModel（namespace ForkPlus.Avalonia）。
//
// 对照 WPF 工程 src/ForkPlus/UI/ReferenceViewModel.cs（47 行）：
//   - WPF: public abstract class ReferenceViewModel : INotifyPropertyChanged
//   - [DebuggerDisplay("{Reference.FullReference}")]
//   - abstract Reference Reference { get; }
//   - int ActiveGraphColumn { get; }
//   - string SearchString { get; set; } + PropertyChanged
//   - event PropertyChangedEventHandler PropertyChanged
//   - RaisePropertyChanged(PropertyChangedEventArgs)
//   - 依赖：ForkPlus.Git.Reference / ForkPlus.Git（Core 可用）
//
// Avalonia 版差异：
//   1. INotifyPropertyChanged → System.ComponentModel（跨平台，零改动）
//   2. ForkPlus.Git.Reference → ForkPlus.Core（Avalonia 工程已引用）
//   3. [Null] Attribute → spike 跳过（nullable disable in csproj）
//
// spike 简化（task spec 关键 API）：
//   - abstract Reference / ActiveGraphColumn / SearchString + PropertyChanged
namespace ForkPlus.Avalonia
{
    [DebuggerDisplay("{Reference.FullReference}")]
    public abstract class ReferenceViewModel : INotifyPropertyChanged
    {
        private static readonly PropertyChangedEventArgs SearchStringChangedEventArgs = new PropertyChangedEventArgs("SearchString");

        private string _searchString;

        public abstract Reference Reference { get; }

        public int ActiveGraphColumn { get; }

        public string SearchString
        {
            get => _searchString;
            set
            {
                if (!(_searchString == value))
                {
                    _searchString = value;
                    this.PropertyChanged?.Invoke(this, SearchStringChangedEventArgs);
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public ReferenceViewModel(int graphColumn)
        {
            ActiveGraphColumn = graphColumn;
        }

        protected void RaisePropertyChanged(PropertyChangedEventArgs args)
        {
            this.PropertyChanged?.Invoke(this, args);
        }
    }
}
