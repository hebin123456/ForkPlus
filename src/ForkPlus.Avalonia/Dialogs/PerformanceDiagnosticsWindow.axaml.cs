using System;
using System.Linq;
using System.Text;
using Avalonia.Interactivity;
using ForkPlus.Services;

namespace ForkPlus.Avalonia.Dialogs
{
    // Phase 4.9b：Avalonia 版 PerformanceDiagnosticsWindow（真实迁移版）。
    //
    // 对照 WPF 工程 src/ForkPlus/UI/Dialogs/PerformanceDiagnosticsWindow.xaml.cs（74 行）：
    //   - public partial class PerformanceDiagnosticsWindow : ForkPlusDialogWindow
    //   - 构造函数：
    //     * DialogTitle = PreferencesLocalization.Current("Performance Diagnostics")
    //     * DialogDescription = PreferencesLocalization.Current("Recent Git and UI operation timings.")
    //     * CancelButtonTitle = PreferencesLocalization.Current("Close")
    //     * ShowSubmitButton = false
    //     * RefreshSamples()
    //   - RefreshButton_Click → RefreshSamples()
    //   - CopyButton_Click → ServiceLocator.Clipboard.SetText(SamplesTextBox.Text)
    //   - FormatSamples() 读 PerformanceTelemetry.SlowestSamples(20) + RecentSamples().Reverse().Take(80)
    //   - AppendSample() 格式化每行（时间 + bg/ui 标记 + 毫秒 + 名称）
    //
    // 调用方（WPF 版）：
    //   new PerformanceDiagnosticsWindow().ShowDialog()
    //
    // 调用方（Avalonia 版）：
    //   await new PerformanceDiagnosticsWindow().ShowDialog(owner)
    //
    // 依赖：PerformanceTelemetry / PerformanceSample（Core internal，已通过 InternalsVisibleTo
    // 暴露给 ForkPlus.Avalonia，参见 src/ForkPlus.Core/ForkPlus.Core.csproj）。
    public partial class PerformanceDiagnosticsWindow : global::ForkPlus.Avalonia.Dialogs.ForkPlusDialogWindow
    {
        public PerformanceDiagnosticsWindow()
        {
            ShowFooter = true;
            InitializeComponent();
            SetFooter(Footer);
            SetTitleTextBlock(TitleTextBlock);
            SetDescriptionTextBlock(DescriptionTextBlock);

            // 对照 WPF: DialogTitle / DialogDescription / CancelButtonTitle / ShowSubmitButton
            string title = Current("Performance Diagnostics");
            Title = title;
            DialogTitle = title;
            DialogDescription = Current("Recent Git and UI operation timings.");
            CancelButtonTitle = Current("Close");
            ShowSubmitButton = false;

            // 对照 WPF: RefreshSamples()
            RefreshSamples();
        }

        // 对照 WPF: private void RefreshButton_Click(object sender, RoutedEventArgs e) { RefreshSamples(); }
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshSamples();
        }

        // 对照 WPF: private void CopyButton_Click(object sender, RoutedEventArgs e) { ServiceLocator.Clipboard.SetText(SamplesTextBox.Text); }
        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ServiceLocator.Clipboard?.SetText(SamplesTextBox.Text ?? "");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to copy samples: " + ex.Message);
            }
        }

        // 对照 WPF: private void RefreshSamples() { SamplesTextBox.Text = FormatSamples(); }
        private void RefreshSamples()
        {
            SamplesTextBox.Text = FormatSamples();
        }

        // 对照 WPF: private static string FormatSamples()
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
            // 对照 WPF: PerformanceTelemetry.RecentSamples().Reverse().Take(80)
            // RecentSamples 返回按入队顺序的数组（旧→新），WPF 用 Reverse 后取前 80（即最新 80 条）
            foreach (PerformanceSample sample in PerformanceTelemetry.RecentSamples().Reverse().Take(80))
            {
                AppendSample(builder, sample);
            }
            return builder.ToString();
        }

        // 对照 WPF: private static void AppendSample(StringBuilder builder, PerformanceSample sample)
        private static void AppendSample(StringBuilder builder, PerformanceSample sample)
        {
            builder
                .Append(sample.TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff"))
                .Append(sample.BackgroundThread ? " bg " : " ui ")
                .Append(sample.ElapsedMilliseconds.ToString().PadLeft(7))
                .Append(" ms  ")
                .AppendLine(sample.Name);
        }

        // PreferencesLocalization.Current(text) → ServiceLocator.Localization.Current(text)
        private static string Current(string text)
        {
            var localization = ServiceLocator.Localization;
            return localization != null ? localization.Current(text) : text;
        }
    }
}
