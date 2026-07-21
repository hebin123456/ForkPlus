using System.ComponentModel;
using ForkPlus.Git;

namespace ForkPlus.Avalonia.Dialogs
{
    // Avalonia 端 PartialStashFileViewModel stub（对照 WPF src/ForkPlus/UI/Dialogs/PartialStashFileViewModel.cs）。
    //
    // 背景：WPF 工程 src/ForkPlus/UI/Dialogs/PartialStashFileViewModel.cs 中的类
    // 依赖 System.Windows.Media.ImageSource / ForkPlus.UI.UserControls.IconTools，
    // 这些都是 WPF-only 类型，无法在 Avalonia 工程中引用。
    //
    // spike 策略：在 Avalonia 工程内创建一个最小 POCO，只保留 CreatePartialStashWindow
    // 真正使用的 ChangedFile / FilePath / Selected 三个属性，省略 FileTypeIcon
    // （WPF 用 ImageSource 显示文件扩展名图标，Avalonia spike 不显示图标）。
    //
    // 对照 WPF:
    //   public ChangedFile ChangedFile { get; }       // ← 保留
    //   public string FilePath { get; }               // ← 保留
    //   public bool Selected { get; set; }            // ← 保留（支持双向绑定通知）
    //   public ImageSource FileTypeIcon { get; }      // ← stub 省略
    //   public event PropertyChangedEventHandler PropertyChanged;
    public sealed class PartialStashFileViewModel : INotifyPropertyChanged
    {
        private bool _selected;

        public ChangedFile ChangedFile { get; }

        public string FilePath { get; }

        public bool Selected
        {
            get => _selected;
            set
            {
                if (_selected != value)
                {
                    _selected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Selected)));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public PartialStashFileViewModel(ChangedFile changedFile, string filePath, bool selected)
        {
            ChangedFile = changedFile;
            FilePath = filePath;
            _selected = selected;
        }
    }
}
