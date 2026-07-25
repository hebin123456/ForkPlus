using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ForkPlus.Git;
using Theme = ForkPlus.UI.Theme;

namespace ForkPlus.UI.Controls
{
	public class GitPointView : Grid
	{
		// 阶段 4.5：WPF DependencyProperty.Register → Avalonia StyledProperty.Register。
		public static readonly StyledProperty<Thickness> IconMarginProperty =
			AvaloniaProperty.Register<GitPointView, Thickness>(nameof(IconMargin), new Thickness(1.0, 3.0, 7.0, 1.0));

		// 阶段 5：WPF Control.FontSize / FontWeight / Foreground 桥接。
		// Grid（Avalonia Panel）不像 WPF Control 那样自动继承这些属性，需显式声明以兼容 XAML Setter。
		public static readonly StyledProperty<double> FontSizeProperty =
			AvaloniaProperty.Register<GitPointView, double>(nameof(FontSize), 13.0);

		public static readonly StyledProperty<FontWeight> FontWeightProperty =
			AvaloniaProperty.Register<GitPointView, FontWeight>(nameof(FontWeight), FontWeight.Normal);

		public static readonly StyledProperty<IBrush> ForegroundProperty =
			AvaloniaProperty.Register<GitPointView, IBrush>(nameof(Foreground));

		private bool _customFontStyle;

		private IGitPoint _value;

		public Thickness IconMargin
		{
			get => GetValue(IconMarginProperty);
			set => SetValue(IconMarginProperty, value);
		}

		/// <summary>WPF Control.FontSize 桥接。仅供 XAML 兼容；不影响内部 TextBlock 渲染。</summary>
		public double FontSize
		{
			get => GetValue(FontSizeProperty);
			set => SetValue(FontSizeProperty, value);
		}

		/// <summary>WPF Control.FontWeight 桥接。</summary>
		public FontWeight FontWeight
		{
			get => GetValue(FontWeightProperty);
			set => SetValue(FontWeightProperty, value);
		}

		/// <summary>WPF Control.Foreground 桥接。</summary>
		public IBrush Foreground
		{
			get => GetValue(ForegroundProperty);
			set => SetValue(ForegroundProperty, value);
		}

		public bool CustomFontStyle
		{
			get => _customFontStyle;
			set => _customFontStyle = value;
		}

		public IGitPoint Value
		{
			get => _value;
			set
			{
				_value = value;
				base.Children.Clear();
				if (_value != null)
				{
					Image image = CreateImage(_value?.GetType());
					image.SetValue(Grid.ColumnProperty, 0);
					base.Children.Add(image);
					if (_value is Revision revision)
					{
						string identifier = ((!(revision is StashRevision stashRevision)) ? revision.Sha.ToAbbreviatedString() : stashRevision.ReflogName);
						TextBlock textBlock = CreateIdTextBlock(identifier);
						textBlock.SetValue(Grid.ColumnProperty, 1);
						base.Children.Add(textBlock);
					}
					else if (_value is RevisionDetails { Sha: var sha })
					{
						TextBlock textBlock2 = CreateIdTextBlock(sha.ToAbbreviatedString());
						textBlock2.SetValue(Grid.ColumnProperty, 1);
						base.Children.Add(textBlock2);
					}
					TextBlock textBlock3 = new TextBlock
					{
						Margin = new Thickness(0.0, 0.0, 0.0, 0.0),
						VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
						HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
						TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
						Text = Description(_value)
					};
					// 阶段 4.5：WPF ToolTip 属性 → Avalonia ToolTip.SetTip 附加属性。
					ToolTip.SetTip(textBlock3, Description(_value));
					if (!CustomFontStyle)
					{
						textBlock3.FontSize = 13.0;
						textBlock3.Foreground = ForkPlus.UI.Theme.LabelBrush;
					}
					textBlock3.SetValue(Grid.ColumnProperty, 2);
					base.Children.Add(textBlock3);
				}
			}
		}

		public GitPointView()
		{
			base.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			base.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = GridLength.Auto
			});
			base.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Star)
			});
		}

		private Image CreateImage(Type type)
		{
			return new Image
			{
				Margin = IconMargin,
				Source = GetIcon(type),
				Width = 14.0,
				Height = 14.0,
				HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
				VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
			};
		}

		private IImage GetIcon(Type type)
		{
			if (type == typeof(StashRevision))
			{
				return ForkPlus.UI.Theme.StashIcon;
			}
			if (type == typeof(Revision))
			{
				return ForkPlus.UI.Theme.RevisionIcon;
			}
			if (type == typeof(RevisionDetails))
			{
				return ForkPlus.UI.Theme.RevisionIcon;
			}
			if (type == typeof(LocalBranch))
			{
				return ForkPlus.UI.Theme.BranchIcon;
			}
			if (type == typeof(RemoteBranch))
			{
				return ForkPlus.UI.Theme.BranchIcon;
			}
			if (type == typeof(Tag))
			{
				return ForkPlus.UI.Theme.TagIcon;
			}
			return ForkPlus.UI.Theme.BranchIcon;
		}

		private static TextBlock CreateIdTextBlock(string identifier)
		{
			return new TextBlock
			{
				Margin = new Thickness(0.0, 0.0, 6.0, 0.0),
				VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
				HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
				FontSize = 13.0,
				Foreground = ForkPlus.UI.Theme.LabelBrush,
				Text = identifier
			};
		}

		private static string Description(IGitPoint gitPoint)
		{
			if (gitPoint is Revision revision)
			{
				return revision.Message;
			}
			if (gitPoint is RevisionDetails revisionDetails)
			{
				revisionDetails.MessageParts(out var subject, out var _);
				return subject;
			}
			if (gitPoint is Reference reference)
			{
				return reference.Name;
			}
			if (gitPoint is SymbolicReference symbolicReference)
			{
				return symbolicReference.FriendlyName;
			}
			return gitPoint.ObjectName;
		}
	}
}
