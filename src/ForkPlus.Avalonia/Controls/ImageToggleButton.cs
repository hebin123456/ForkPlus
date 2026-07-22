using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ForkPlus.Avalonia.Controls
{
    // Avalonia 版 ImageToggleButton（spike 简化版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Controls/ImageToggleButton.cs（46 行）：
    //   - WPF ImageToggleButton : Button（internal class）
    //     注意 WPF 类名虽叫 ImageToggleButton，但实际继承 Button 而非 ToggleButton
    //   - private bool _state（当前是否选中态）
    //   - private readonly Image _imageControl = new Image()
    //   - bool State { get; set; }（setter 调用 RefreshImages）
    //   - ImageSource Image { get; set; }（选中态图像）
    //   - ImageSource AlternativeImage { get; set; }（非选中态图像）
    //   - 构造函数 base.Content = _imageControl
    //   - RefreshImages：State=true → _imageControl.Source = Image
    //                    State=false → _imageControl.Source = AlternativeImage
    //
    // Avalonia 版差异（spike 简化策略）：
    //   1. 基类 System.Windows.Controls.Button → Avalonia.Controls.Button（API 一致）
    //   2. WPF ImageSource → Avalonia.Media.IImage（API 等价）
    //   3. WPF System.Windows.Controls.Image → Avalonia.Controls.Image（API 一致）
    //   4. spike 保持 internal 类（与 WPF 一致，仅本工程内部使用）
    //   5. task spec 描述"继承 ToggleButton"是泛称，spike 按 WPF 实际继承 Button
    //
    // spike 简化：
    //   - 与 WPF 一致：State / Image / AlternativeImage 属性 + RefreshImages 方法
    internal class ImageToggleButton : Button
    {
        // 对照 WPF: private bool _state
        private bool _state;

        // 对照 WPF: private readonly Image _imageControl = new Image()
        private readonly Image _imageControl = new Image();

        // 对照 WPF: public bool State { get; set; }
        public bool State
        {
            get => _state;
            set
            {
                _state = value;
                RefreshImages();
            }
        }

        // 对照 WPF: public ImageSource Image { get; set; }
        public IImage Image { get; set; }

        // 对照 WPF: public ImageSource AlternativeImage { get; set; }
        public IImage AlternativeImage { get; set; }

        // 对照 WPF: public ImageToggleButton() { base.Content = _imageControl; }
        public ImageToggleButton()
        {
            Content = _imageControl;
        }

        // 对照 WPF: private void RefreshImages()
        private void RefreshImages()
        {
            if (State)
            {
                _imageControl.Source = Image;
            }
            else
            {
                _imageControl.Source = AlternativeImage;
            }
        }
    }
}
