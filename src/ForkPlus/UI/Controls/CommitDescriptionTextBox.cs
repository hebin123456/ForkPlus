using System;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Controls
{
	public class CommitDescriptionTextBox : SpellingPlaceholderTextBox
	{
		// 阶段 4.5：WPF DependencyProperty.Register + FrameworkPropertyMetadata → Avalonia StyledProperty.Register。
		public static readonly StyledProperty<Thickness> GuideLineMarginProperty =
			AvaloniaProperty.Register<CommitDescriptionTextBox, Thickness>(nameof(GuideLineMargin), new Thickness(0.0, 0.0, 0.0, 0.0));

		public Thickness GuideLineMargin
		{
			get => GetValue(GuideLineMarginProperty);
			set => SetValue(GuideLineMarginProperty, value);
		}

		public CommitDescriptionTextBox()
		{
			if (!global::ForkPlus.DesignTimeHelper.IsInDesignMode())
			{
				// 阶段 4.5：WPF WeakEventManager → 直接事件订阅。
				NotificationCenter.Current.PageGuideLinePositionChanged += delegate
				{
					RefreshGuideLine();
				};
			}
			base.Loaded += delegate
			{
				RefreshGuideLine();
			};
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
