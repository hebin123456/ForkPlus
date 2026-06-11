using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Markup;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.Services;

namespace ForkPlus.UI.Dialogs
{
	public partial class PerformanceDiagnosticsWindow : ForkPlusDialogWindow
	{
		public PerformanceDiagnosticsWindow()
		{
			InitializeComponent();
			base.DialogTitle = Translate("Performance Diagnostics");
			base.DialogDescription = Translate("Recent Git and UI operation timings.");
			base.CancelButtonTitle = Translate("Close");
			base.ShowSubmitButton = false;
			RefreshSamples();
		}

		private void RefreshButton_Click(object sender, RoutedEventArgs e)
		{
			RefreshSamples();
		}

		private void CopyButton_Click(object sender, RoutedEventArgs e)
		{
			ServiceLocator.Clipboard.SetText(SamplesTextBox.Text);
		}

		private void RefreshSamples()
		{
			SamplesTextBox.Text = FormatSamples();
		}

		private static string FormatSamples()
		{
			StringBuilder builder = new StringBuilder();
			PerformanceSample[] slowest = PerformanceTelemetry.SlowestSamples(20);
			builder.AppendLine("Slowest samples");
			builder.AppendLine("===============");
			foreach (PerformanceSample sample in slowest)
			{
				AppendSample(builder, sample);
			}
			builder.AppendLine();
			builder.AppendLine("Recent samples");
			builder.AppendLine("==============");
			foreach (PerformanceSample sample in PerformanceTelemetry.RecentSamples().Reverse().Take(80))
			{
				AppendSample(builder, sample);
			}
			return builder.ToString();
		}

		private static void AppendSample(StringBuilder builder, PerformanceSample sample)
		{
			builder
				.Append(sample.TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff"))
				.Append(sample.BackgroundThread ? " bg " : " ui ")
				.Append(sample.ElapsedMilliseconds.ToString().PadLeft(7))
				.Append(" ms  ")
				.AppendLine(sample.Name);
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
