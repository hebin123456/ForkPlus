using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Microsoft.Extensions.DependencyInjection;

namespace ForkPlus.Avalonia.Views.UserControls
{
    // Avalonia 版 FallbackUserControl（spike 简化新建版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/UserControls/FallbackUserControl.xaml.cs（164 行）：
    //   - 5 个 DependencyProperty（FallbackTitle/HideFallbackImage/FallbackMessage/
    //     IsMonospace/Button1Title）
    //   - OnPropertyChanged 分发：设置各 TextBlock/Button 显隐 + 文本
    //   - FallbackMessageFontSize 属性
    //   - Button1Click 事件 + ResetEvents 方法
    //   - FontConstants.MonospaceFontFamily（IsMonospace 时切换字体）
    //
    // Avalonia 版差异（spike 简化）：
    //   - WPF DependencyProperty.RegisterAttached → plain property + setter 内更新 UI
    //   - WPF OnPropertyChanged(DependencyPropertyChangedEventArgs) → 直接 property setter
    //   - WPF Visibility.Collapsed/Visible → IsVisible=false/true
    //   - WPF Image FallbackImage → emoji TextBlock（📦）
    //   - WPF FontConstants.MonospaceFontFamily → 硬编码 "Cascadia Mono,Consolas,monospace"
    //   - task spec 关键 API：SetContent(string text) / SetFile(string path)
    //
    // spike 简化：
    //   - SetContent(string text) / SetFile(string path) 公共方法（task spec 关键 API）
    //   - FallbackTitle / FallbackMessage / Button1Title plain property（setter 内更新 UI）
    //   - HideFallbackImage / IsMonospace plain property
    //   - Button1Click 事件 + ResetEvents 方法
    public partial class FallbackUserControl : UserControl
    {
        // ===== 公共事件（对照 WPF: Button1Click）=====
        public event EventHandler<RoutedEventArgs> Button1Click;

        // ===== 私有字段 =====
        private readonly IServiceProvider _serviceProvider;
        private string _fallbackTitle;
        private string _fallbackMessage;
        private string _button1Title;
        private bool _hideFallbackImage;
        private bool _isMonospace;

        // ===== 公共属性（对照 WPF 5 个 DependencyProperty，spike 用 plain property）=====

        // 对照 WPF: FallbackTitle DependencyProperty
        public string FallbackTitle
        {
            get => _fallbackTitle;
            set
            {
                _fallbackTitle = value;
                if (FallbackTitleTextBlock != null)
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        FallbackTitleTextBlock.IsVisible = false;
                    }
                    else
                    {
                        FallbackTitleTextBlock.Text = value;
                        FallbackTitleTextBlock.IsVisible = true;
                    }
                }
            }
        }

        // 对照 WPF: FallbackMessage DependencyProperty
        public string FallbackMessage
        {
            get => _fallbackMessage;
            set
            {
                _fallbackMessage = value;
                if (FallbackMessageTextBlock != null)
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        FallbackMessageTextBlock.IsVisible = false;
                    }
                    else
                    {
                        FallbackMessageTextBlock.Text = value;
                        FallbackMessageTextBlock.IsVisible = true;
                    }
                }
            }
        }

        // 对照 WPF: Button1Title DependencyProperty
        public string Button1Title
        {
            get => _button1Title;
            set
            {
                _button1Title = value;
                if (Button1 != null)
                {
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        Button1.IsVisible = false;
                    }
                    else
                    {
                        Button1.Content = value;
                        Button1.IsVisible = true;
                    }
                }
            }
        }

        // 对照 WPF: HideFallbackImage DependencyProperty
        public bool HideFallbackImage
        {
            get => _hideFallbackImage;
            set
            {
                _hideFallbackImage = value;
                if (FallbackImage != null)
                {
                    // 对照 WPF: FallbackImage.Show()/Collapse()
                    FallbackImage.IsVisible = !value;
                }
            }
        }

        // 对照 WPF: IsMonospace DependencyProperty
        public bool IsMonospace
        {
            get => _isMonospace;
            set
            {
                _isMonospace = value;
                if (FallbackMessageTextBlock != null && value)
                {
                    // 对照 WPF: FontConstants.MonospaceFontFamily
                    FallbackMessageTextBlock.FontFamily = FontFamily.Parse("Cascadia Mono,Consolas,Menlo,monospace");
                    FallbackMessageTextBlock.FontSize = 14.0;
                    FallbackMessageTextBlock.TextAlignment = TextAlignment.Left;
                    // 使用 Avalonia.Layout.HorizontalAlignment（需 using Avalonia.Layout）
                    FallbackMessageTextBlock.HorizontalAlignment = HorizontalAlignment.Left;
                }
            }
        }

        // 对照 WPF: FallbackMessageFontSize
        public double FallbackMessageFontSize
        {
            get => FallbackMessageTextBlock?.FontSize ?? 13.0;
            set
            {
                if (FallbackMessageTextBlock != null)
                {
                    FallbackMessageTextBlock.FontSize = value;
                }
            }
        }

        // ===== 构造函数（spike 用 IServiceProvider）=====
        public FallbackUserControl(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            InitializeComponent();
        }

        // ===== SetContent(string)（task spec 关键 API）=====
        // 设置 fallback 消息内容（对照 WPF FallbackMessage 属性设置）
        public void SetContent(string text)
        {
            FallbackMessage = text;
        }

        // ===== SetFile(string)（task spec 关键 API）=====
        // 设置文件路径（spike 版：显示文件名作为标题 + 路径作为消息）
        public void SetFile(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                FallbackTitle = null;
                FallbackMessage = null;
                return;
            }
            FallbackTitle = System.IO.Path.GetFileName(path);
            FallbackMessage = path;
        }

        // ===== ResetEvents()（对照 WPF）=====
        public void ResetEvents()
        {
            Button1Click = null;
        }

        // ===== Button1_Click（对照 WPF）=====
        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            Button1Click?.Invoke(sender, e);
        }
    }
}
