using System;
using ForkPlus.UI.WpfCompat;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.Helpers;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class CommitDescriptionTextBox : SpellingPlaceholderTextBox
	{
		public static readonly global::Avalonia.StyledProperty<Thickness> GuideLineMarginProperty =
    global::Avalonia.AvaloniaProperty.Register<CommitDescriptionTextBox, Thickness>("GuideLineMargin");

		public Thickness GuideLineMargin
		{
			get
			{
				return (Thickness)GetValue(GuideLineMarginProperty);
			}
			set
			{
				SetValue(GuideLineMarginProperty, value);
			}
		}

		public CommitDescriptionTextBox()
		{
			if (!global::ForkPlus.DesignTimeHelper.IsInDesignMode())
			{
				WeakEventManager<NotificationCenter, EventArgs<int>>.AddHandler(NotificationCenter.Current,"PageGuideLinePositionChanged",delegate
(object sender, global::System.EventArgs e)				{
					RefreshGuideLine();
				});
			}
			base.Loaded += delegate
			{
				RefreshGuideLine();
			};
		}

		/// <summary>WPF 行为保全（E2e26 快捷键全量测试发现的迁移回归，2026-09-06）：
		/// 描述框（AcceptsReturn）内按 Ctrl+Enter（提交 secondary）/ Ctrl+Shift+Enter（提交
		/// primary）/ Ctrl+Alt+Enter（WIP 拆分）在 WPF 里由 CommandManager 在输入预处理阶段
		/// 匹配窗口/CommitUserControl 级 CommandBinding 的 RoutedCommand 手势（先于 TextBox
		/// 类处理执行）——行为是"提交且不插入换行"。迁移后 CommandRouter 挂在 TopLevel
		/// 冒泡阶段，而 Avalonia TextBox.OnKeyDown 对 AcceptsReturn 的 Enter 无条件消费
		/// （插入换行并置 Handled，不检查修饰键，见 Avalonia 12.1.1 TextBox.cs Key.Enter
		/// 分支），Ctrl+Enter 永远到不了 CommandRouter——既不提交还多出一个换行。
		/// 修复：带 Ctrl 的 Enter 不做类处理（不插换行、不置 Handled），让事件继续冒泡，
		/// 由 CommandRouter 翻译成提交命令（与 WPF 原行为一致）。Shift/Alt 单独按下的
		/// Enter 仍走 base（插入换行，WPF 同）。</summary>
		protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
		{
			if (e.Key == Avalonia.Input.Key.Return
				&& (e.KeyModifiers & Avalonia.Input.KeyModifiers.Control) == Avalonia.Input.KeyModifiers.Control)
			{
				return;
			}
			base.OnKeyDown(e);
		}

		protected override ContextMenu GetContextMenu()
		{
			ContextMenu contextMenu = new ContextMenu();
			contextMenu.AddDefaultTextBoxMenuItems(this);
			contextMenu.Items.Add(new Separator());
			MenuItem menuItem = new MenuItem();
			menuItem.Header = PreferencesLocalization.MenuHeader("Wrap Paragraph at Ruler");
			menuItem.Click += delegate
			{
				int pageGuideLinePosition = ForkPlusSettings.Default.PageGuideLinePosition;
				int width = ((pageGuideLinePosition > 0) ? pageGuideLinePosition : 72);
				base.Text = WrapString(base.Text, width);
			};
			contextMenu.Items.Add(menuItem);
			global::ForkPlus.UI.WpfCompat.ContextMenuCompat.AttachAutoDismiss(contextMenu, this);
			return contextMenu;
		}

		private string WrapString(string input, int width)
		{
			string text = Environment.NewLine + Environment.NewLine;
			string[] array = input.Split(new string[2] { text, "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < array.Length; i++)
			{
				string[] array2 = array[i].Replace(Environment.NewLine, " ").Split(Consts.Chars.Space, StringSplitOptions.RemoveEmptyEntries);
				if (array2.Length == 0)
				{
					continue;
				}
				if (i > 0)
				{
					stringBuilder.Append(text);
				}
				int num = 0;
				foreach (string text2 in array2)
				{
					if (string.IsNullOrWhiteSpace(text2))
					{
						continue;
					}
					if (num + 1 + text2.Length > width)
					{
						stringBuilder.Append(Environment.NewLine);
						stringBuilder.Append(text2);
						num = 1 + text2.Length;
						continue;
					}
					if (num > 0)
					{
						stringBuilder.Append(" ");
						num++;
					}
					stringBuilder.Append(text2);
					num += text2.Length;
				}
			}
			return stringBuilder.ToString();
		}

		private void RefreshGuideLine()
		{
			double left = TextGuidelineHelper.GuideLinePosition(this, ForkPlusSettings.Default.PageGuideLinePosition);
			GuideLineMargin = new Thickness(left, 0.0, 0.0, 0.0);
		}
	}
}
