using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Microsoft.Extensions.DependencyInjection;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 PathTextBoxUserControl（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/PathTextBoxUserControl.xaml.cs（81 行）：
    //   - 构造函数 PathTextBoxUserControl(ForkPlusDialogWindow parentWindow, DialogType dialogType)
    //   - StringValue 属性（PathTextBox.Text 读写）
    //   - BrowseButton_Click → SaveFile/OpenFile/OpenDirectory（OpenDialog 弹窗）
    //   - 依赖 ForkPlusDialogWindow + OpenDialog（WPF WindowsAPICodePack）
    //
    // Avalonia 版差异（spike 简化）：
    //   - WPF ForkPlusDialogWindow → object parentWindow（spike 占位）
    //   - WPF OpenDialog.SelectFileSaveLocation/SelectFile/SelectDirectory →
    //     spike 用 BrowseClicked 事件回调（调用方通过 IDialogService 处理）
    //   - WPF StringValue → Path（task spec 关键 API 命名）
    //   - task spec 关键 API：Path 属性 / BrowseClicked 事件 / SetPath(string)
    //   - WPF DialogType 枚举 → 内部保留（SaveFile/OpenFile/OpenDirectory）
    //
    // spike 简化：
    //   - Path 属性（读写 PathTextBox.Text）
    //   - BrowseClicked 事件（task spec 关键 API）
    //   - SetPath(string) 方法（task spec 关键 API）
    //   - Browse 按钮点击触发事件，由调用方处理实际弹窗
    public partial class PathTextBoxUserControl : UserControl
    {
        // ===== 对话框类型枚举（对照 WPF: CustomCommandUI.Control.PathTextBox.DialogType）=====
        public enum DialogType
        {
            SaveFile,
            OpenFile,
            OpenDirectory
        }

        // ===== 公共事件（task spec 关键 API）=====
        // Browse 按钮点击事件（对照 WPF: BrowseButton_Click 内直接弹窗）
        // spike 版改为事件回调，调用方通过 IDialogService.StorageProvider 处理
        public event EventHandler<RoutedEventArgs> BrowseClicked;

        // ===== 公共属性（task spec 关键 API）=====
        // 路径（对照 WPF: StringValue => PathTextBox.Text）
        public string Path
        {
            get => PathTextBox?.Text ?? string.Empty;
            set
            {
                if (PathTextBox != null)
                {
                    PathTextBox.Text = value;
                }
            }
        }

        // ===== 私有字段 =====
        private readonly IServiceProvider _serviceProvider;
        private object _parentWindow; // spike 占位（对照 WPF: ForkPlusDialogWindow）
        private DialogType _dialogType;

        // ===== 构造函数（spike 用 IServiceProvider）=====
        public PathTextBoxUserControl(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();
            _dialogType = DialogType.OpenFile;
        }

        // ===== SetPath(string)（task spec 关键 API）=====
        // 对照 WPF: StringValue setter
        public void SetPath(string path)
        {
            Path = path;
        }

        // ===== SetDialogType(DialogType)（spike 新增，设置对话框类型）=====
        public void SetDialogType(DialogType dialogType)
        {
            _dialogType = dialogType;
        }

        // ===== SetParentWindow(object)（spike 新增，注入父窗口）=====
        public void SetParentWindow(object parentWindow)
        {
            _parentWindow = parentWindow;
        }

        // ===== BrowseButton_Click（对照 WPF）=====
        // WPF: 直接调 OpenDialog.SelectFileSaveLocation/SelectFile/SelectDirectory
        // spike: 触发 BrowseClicked 事件，调用方处理
        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            BrowseClicked?.Invoke(this, e);
        }
    }
}
