using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.Helpers;

namespace ForkPlus.UI.Controls
{
	public class CommitDescriptionTextBox : SpellingPlaceholderTextBox
	{
		public static readonly DependencyProperty GuideLineMarginProperty = DependencyProperty.Register("GuideLineMargin", typeof(Thickness), typeof(CommitDescriptionTextBox), new FrameworkPropertyMetadata(new Thickness(0.0, 0.0, 0.0, 0.0)));

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
				WeakEventManager<NotificationCenter, EventArgs<int>>.AddHandler(NotificationCenter.Current, "PageGuideLinePositionChanged", delegate
				{
					RefreshGuideLine();
				});
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
