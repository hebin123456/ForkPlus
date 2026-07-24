// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows.Controls → using Avalonia.Controls
// - ProgressBar 解析为 Avalonia.Controls.ProgressBar
using Avalonia.Controls;

namespace ForkPlus.UI
{
	public static class ProgressBarExtensions
	{
		public static void ShowWithProgress(this ProgressBar progressBar, double progress)
		{
			progressBar.Show();
			progressBar.Value = progress;
		}
	}
}
