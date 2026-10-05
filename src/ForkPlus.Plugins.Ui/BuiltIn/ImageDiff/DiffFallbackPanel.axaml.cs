using Avalonia.Controls;

namespace ForkPlus.Plugins.BuiltIn.ImageDiff
{
	/// <summary>
	/// v5.0.0：对比视图的回退层（双侧均无内容时显示），自主工程 FallbackUserControl 最小移植。
	/// v5.0.0：从 Image 插件移入共享组件工程（Ui），图片/Hex 插件视图共用。
	/// </summary>
	public partial class DiffFallbackPanel : UserControl
	{
		public string FallbackMessage
		{
			get
			{
				return FallbackMessageTextBlock.Text;
			}
			set
			{
				FallbackMessageTextBlock.Text = value;
			}
		}

		public DiffFallbackPanel()
		{
			InitializeComponent();
		}
	}
}
