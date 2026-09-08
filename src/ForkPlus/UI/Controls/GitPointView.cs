using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ForkPlus.Git;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class GitPointView : Grid
	{
		public static readonly global::Avalonia.StyledProperty<Thickness> IconMarginProperty =
    global::Avalonia.AvaloniaProperty.Register<GitPointView, Thickness>("IconMargin");

		private bool _customFontStyle;

		private IGitPoint _value;

		public Thickness IconMargin
		{
			get
			{
				return (Thickness)GetValue(IconMarginProperty);
			}
			set
			{
				SetValue(IconMarginProperty, value);
			}
		}

		public bool CustomFontStyle
		{
			get
			{
				return _customFontStyle;
			}
			set
			{
				_customFontStyle = value;
			}
		}

		public IGitPoint Value
		{
			get
			{
				return _value;
			}
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
					TextBlock textBlock3 = global::ForkPlus.UI.WpfCompat.ToolTipCompat.WithTip(new TextBlock
					{
						Margin = new Thickness(0.0, 0.0, 0.0, 0.0),						VerticalAlignment = VerticalAlignment.Center,						HorizontalAlignment = HorizontalAlignment.Left,						TextTrimming = TextTrimming.CharacterEllipsis,						Text = Description(_value)					},Description(_value)
);
					if (!CustomFontStyle)
					{
						textBlock3.FontSize = 13.0;
						textBlock3.Foreground = global::ForkPlus.UI.Theme.LabelBrush;
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
				Width = new GridLength(1.0, GridUnitType.Auto)
			});
			base.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength(1.0, GridUnitType.Auto)
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
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Center
			};
		}

		private global::Avalonia.Media.IImage GetIcon(Type type)
		{
			if (type == typeof(StashRevision))
			{
				return global::ForkPlus.UI.Theme.StashIcon;
			}
			if (type == typeof(Revision))
			{
				return global::ForkPlus.UI.Theme.RevisionIcon;
			}
			if (type == typeof(RevisionDetails))
			{
				return global::ForkPlus.UI.Theme.RevisionIcon;
			}
			if (type == typeof(LocalBranch))
			{
				return global::ForkPlus.UI.Theme.BranchIcon;
			}
			if (type == typeof(RemoteBranch))
			{
				return global::ForkPlus.UI.Theme.BranchIcon;
			}
			if (type == typeof(Tag))
			{
				return global::ForkPlus.UI.Theme.TagIcon;
			}
			return global::ForkPlus.UI.Theme.BranchIcon;
		}

		private static TextBlock CreateIdTextBlock(string identifier)
		{
			return new TextBlock
			{
				Margin = new Thickness(0.0, 0.0, 6.0, 0.0),
				VerticalAlignment = VerticalAlignment.Center,
				HorizontalAlignment = HorizontalAlignment.Left,
				FontSize = 13.0,
				Foreground = global::ForkPlus.UI.Theme.LabelBrush,
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
