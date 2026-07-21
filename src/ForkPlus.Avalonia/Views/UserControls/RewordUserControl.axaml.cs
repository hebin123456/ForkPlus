using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 RewordUserControl（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/RewordUserControl.xaml.cs（147 行）：
    //   - 构造函数 RewordUserControl(string subject, string description)
    //   - 公共属性：Message（合并 subject + description）
    //   - 公共事件：RewordCancelled / MessageChanged
    //   - 公共方法：RaiseRewordCancelled / RaiseMessageChanged / Refresh(subject, description)
    //   - CommitSubjectTextBox + CommitDescriptionTextBox（两个 TextBox）
    //   - SubjectLengthLimitTextBlock（字数限制提示，变色）
    //   - 键盘快捷键：Ctrl+Enter=提交 / Escape=取消
    //
    // Avalonia 版差异：
    //   - WPF TextBox → AvaloniaEdit.TextEditor（spike 用 TextEditor 作为 commit message 编辑框）
    //   - WPF Key.Return + Keyboard.IsKeyDown → Avalonia KeyEventArgs
    //   - WPF DataObject.AddPastingHandler → spike 不实现粘贴分拆
    //   - 构造函数签名：(IServiceProvider serviceProvider)
    //
    // spike 简化：
    //   - 用 AvaloniaEdit.TextEditor 显示 commit message 编辑框
    //   - SetCommit(sha, message) / GetMessage() / ApplyReword() 公共方法
    //   - Initialize(RepositoryUserControl) 注入父控件
    //   - Ctrl+Enter=提交 / Escape=取消 键盘快捷键
    //   - 字数限制提示用 TextBlock 占位
    public partial class RewordUserControl : UserControl
    {
        // ===== 公共事件（对照 WPF）=====
        public event EventHandler<EventArgs> RewordCancelled;
        public event EventHandler<EventArgs> MessageChanged;

        // ===== 公共属性（对照 WPF）=====
        // spike 版用 object 占位，真实类型 RepositoryUserControl
        public object RepositoryUserControl { get; private set; }

        // 当前 commit sha
        public string Sha { get; private set; }

        // spike 版：commit message（subject + body 合并）
        // 对照 WPF: Message => CommitMessageHelper.CreateCommitBody(subject, description)
        public string Message => CommitMessageEditor?.Document?.Text ?? string.Empty;

        public RewordUserControl(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            Console.WriteLine("[Reword] Constructed (spike)");

            // 注册键盘事件
            KeyDown += RewordUserControl_KeyDown;
        }

        // ===== 公共方法（对照 WPF + task spec）=====

        // 对照 task spec: Initialize(RepositoryUserControl)
        //   注入父控件引用
        public void Initialize(object repositoryUserControl)
        {
            Console.WriteLine("[Reword] Initialize");
            RepositoryUserControl = repositoryUserControl;
        }

        // 对照 task spec: SetCommit(string sha, string message)
        //   设置当前 commit 的 sha 和 message，加载到编辑器
        public void SetCommit(string sha, string message)
        {
            Sha = sha;
            Console.WriteLine($"[Reword] SetCommit: sha={sha}");
            if (CommitMessageEditor != null)
            {
                CommitMessageEditor.Document.Text = message ?? string.Empty;
            }
            UpdateSubjectLengthLimit();
        }

        // 对照 task spec: GetMessage()
        //   获取编辑后的 commit message
        public string GetMessage()
        {
            return Message;
        }

        // 对照 task spec: ApplyReword()
        //   应用 reword（触发 MessageChanged 事件）
        public void ApplyReword()
        {
            Console.WriteLine("[Reword] ApplyReword");
            RaiseMessageChanged();
        }

        // 对照 WPF: public void Refresh(string subject, string description)
        //   spike 版：合并 subject + description 加载到编辑器
        public void Refresh(string subject, string description)
        {
            Console.WriteLine($"[Reword] Refresh: subject={subject}");
            string message = subject ?? string.Empty;
            if (!string.IsNullOrEmpty(description))
            {
                message += Environment.NewLine + Environment.NewLine + description;
            }
            if (CommitMessageEditor != null)
            {
                CommitMessageEditor.Document.Text = message;
            }
            UpdateSubjectLengthLimit();
        }

        // 对照 WPF: public void RaiseRewordCancelled()
        public void RaiseRewordCancelled()
        {
            RewordCancelled?.Invoke(this, EventArgs.Empty);
        }

        // 对照 WPF: public void RaiseMessageChanged()
        public void RaiseMessageChanged()
        {
            MessageChanged?.Invoke(this, EventArgs.Empty);
        }

        // ===== 事件处理 =====

        // 对照 WPF: UserControl_PreviewKeyDown
        //   Ctrl+Enter=提交 / Escape=取消
        private void RewordUserControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                RaiseMessageChanged();
                e.Handled = true;
            }
            if (e.Key == Key.Escape)
            {
                RaiseRewordCancelled();
                e.Handled = true;
            }
        }

        // 对照 WPF: CommitMessageOkButton_Click
        private void CommitMessageOkButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseMessageChanged();
        }

        // 对照 WPF: CommitMessageCancelButton_Click
        private void CommitMessageCancelButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseRewordCancelled();
        }

        // 对照 WPF: UpdateSubjectLengthLimit()
        //   spike 版：显示第一行长度限制提示
        private void UpdateSubjectLengthLimit()
        {
            if (SubjectLengthLimitTextBlock == null) return;

            string text = Message;
            int firstLineEnd = text.IndexOf('\n');
            string subject = firstLineEnd >= 0 ? text.Substring(0, firstLineEnd) : text;
            int length = subject.Length;

            if (length == 0)
            {
                SubjectLengthLimitTextBlock.IsVisible = false;
                return;
            }

            SubjectLengthLimitTextBlock.IsVisible = true;
            // spike 版：简单字数提示（50 软限制 / 72 硬限制）
            int softLimit = 50;
            int hardLimit = 72;
            int remaining = softLimit - length;

            if (length > hardLimit)
            {
                SubjectLengthLimitTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0x22, 0x22));
            }
            else if (length > softLimit)
            {
                SubjectLengthLimitTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0x22));
            }
            else
            {
                SubjectLengthLimitTextBlock.Foreground = new SolidColorBrush(Color.FromRgb(0x22, 0xAA, 0x22));
            }

            SubjectLengthLimitTextBlock.Text = remaining.ToString();
        }
    }
}
