using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// 新手引导向导：13 步卡片式介绍核心界面（欢迎 / 工具栏 / 分支侧栏 / 提交历史与
	/// Diff / 提交区 / git mm / 仓库树图 / 仓库统计 / Reflog / 解决冲突 / 个性化 /
	/// 对比视图插件 / 结束）。首次启动未完成引导时由 <see cref="OnboardingManager"/>
	/// 自动弹出；帮助菜单"Getting Started"可随时手动重看。
	/// 布局：步骤内容区 + 自建 footer（Back / Next→Finish / Skip Tour）。不复用基类
	/// ForkPlusDialogFooter，因为向导需要 Back 按钮且 Next 按钮文本随步骤在
	/// Next/Finish 间切换；标题/logo chrome 仍复用基类。
	/// 步骤文本全部写在 XAML（切换只改 IsVisible），基类 Loaded 时的自动本地化
	/// 一次翻译所有步骤；运行时切换的文本（步骤指示器、Next/Finish）经
	/// PreferencesLocalization.Current/FormatCurrent 取当前语言。
	/// 跳过（Skip/Esc）与完整走完均视为"已完成引导"，置位逻辑在 OnboardingManager。
	/// </summary>
	public partial class OnboardingTourWindow : ForkPlusDialogWindow
	{
		/// <summary>步骤总数：欢迎/工具栏/分支侧栏/历史与 Diff/提交区/git mm/树图/统计/Reflog/冲突/个性化/对比视图插件/结束。</summary>
		private const int StepCount = 13;

		/// <summary>步骤内容面板（XAML 里 StepPanel0..StepPanel12 的有序引用）。</summary>
		private StackPanel[] _stepPanels;

		/// <summary>当前步骤索引（0 起，供测试断言翻页行为）。</summary>
		public int CurrentStep { get; private set; }

		public OnboardingTourWindow()
		{
			InitializeComponent();
			_stepPanels = new StackPanel[]
			{
				StepPanel0, StepPanel1, StepPanel2, StepPanel3, StepPanel4, StepPanel5,
				StepPanel6, StepPanel7, StepPanel8, StepPanel9, StepPanel10, StepPanel11,
				StepPanel12
			};
			DialogTitle = PreferencesLocalization.Current("Getting Started");
			// 用自建 footer（Back/Next/Skip），不用基类 Submit/Cancel 两按钮 footer
			ShowFooter = false;
			RefreshStep();
		}

		/// <summary>当前步骤指示器文本（"Step {0} of {1}"，供测试断言）。</summary>
		public string StepIndicatorText => StepIndicatorTextBlock?.Text ?? "";

		protected override void OnKeyDown(KeyEventArgs e)
		{
			// 基类 Escape→OnCancel 仅在 ShowFooter 时生效，此处自建 footer 需自行处理
			if (e.Key == Key.Escape)
			{
				OnCancel();
				e.Handled = true;
			}
			else
			{
				base.OnKeyDown(e);
			}
		}

		private void BackButton_Click(object sender, RoutedEventArgs e)
		{
			if (CurrentStep > 0)
			{
				CurrentStep--;
				RefreshStep();
			}
		}

		private void NextButton_Click(object sender, RoutedEventArgs e)
		{
			if (CurrentStep < StepCount - 1)
			{
				CurrentStep++;
				RefreshStep();
			}
			else
			{
				CloseWithOk();
			}
		}

		private void SkipButton_Click(object sender, RoutedEventArgs e)
		{
			OnCancel();
		}

		private void RefreshStep()
		{
			for (int i = 0; i < StepCount; i++)
			{
				_stepPanels[i].IsVisible = i == CurrentStep;
			}
			StepIndicatorTextBlock.Text = PreferencesLocalization.FormatCurrent("Step {0} of {1}", CurrentStep + 1, StepCount);
			BackButton.IsVisible = CurrentStep > 0;
			bool isLastStep = CurrentStep == StepCount - 1;
			NextButton.Content = PreferencesLocalization.Current(isLastStep ? "Finish" : "Next");
		}
	}
}
