using System.Windows.Controls;

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
