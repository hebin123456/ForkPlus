using System;
using ForkPlus.UI.WpfCompat;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Layout;
using Avalonia.Styling;

namespace ForkPlus.UI.Controls
{
	public class FilePathTextBlock : SelectableTextBlock
	{
		public static readonly global::Avalonia.StyledProperty<string> FilePathProperty =
    global::Avalonia.AvaloniaProperty.Register<FilePathTextBlock, string>("FilePath");

		public static readonly global::Avalonia.StyledProperty<string> OldFilePathProperty =
    global::Avalonia.AvaloniaProperty.Register<FilePathTextBlock, string>("OldFilePath");

		private Brush _labelBrush;

		private Brush _secondaryLabelBrush;

		public string FilePath
		{
			get
			{
				return (string)GetValue(FilePathProperty);
			}
			set
			{
				SetValue(FilePathProperty, value);
			}
		}

		public string OldFilePath
		{
			get
			{
				return (string)GetValue(OldFilePathProperty);
			}
			set
			{
				SetValue(OldFilePathProperty, value);
			}
		}

		public FilePathTextBlock()
		{
			RefreshBrushes();
			base.PointerEntered += delegate(object s, global::Avalonia.Input.PointerEventArgs e)
			{
				e.Handled = true;
				global::Avalonia.Controls.ToolTip.SetTip(this, (TextIsTrimmed() ? GetToolTipText() : null));
			};
			WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
		}

		protected override void OnPropertyChanged(global::Avalonia.AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == FilePathProperty || change.Property == OldFilePathProperty)
			{
				Refresh();
			}
		}

		private void Refresh()
		{
			base.Inlines.Clear();
			string oldFilePath = OldFilePath;
			if (oldFilePath != null)
			{
				string readableFileName = PathHelper.GetReadableFileName(oldFilePath);
				int num = oldFilePath.Length - readableFileName.Length;
				if (num != 0)
				{
					base.Inlines.Add(new Run(oldFilePath.Substring(0, num))
					{
						Foreground = _secondaryLabelBrush
					});
				}
				base.Inlines.Add(new Run(readableFileName)
				{
					Foreground = _labelBrush
				});
				base.Inlines.Add(new Run(" → ")
				{
					Foreground = _labelBrush
				});
			}
			string filePath = FilePath;
			if (filePath != null)
			{
				string readableFileName2 = PathHelper.GetReadableFileName(filePath);
				int num2 = filePath.Length - readableFileName2.Length;
				if (num2 != 0)
				{
					base.Inlines.Add(new Run(filePath.Substring(0, num2))
					{
						Foreground = _secondaryLabelBrush
					});
				}
				base.Inlines.Add(new Run(readableFileName2)
				{
					Foreground = _labelBrush
				});
			}
		}

		private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
		{
			RefreshBrushes();
			Refresh();
		}

		private void RefreshBrushes()
		{
			_labelBrush = global::ForkPlus.UI.Theme.LabelBrush;
			_secondaryLabelBrush = global::ForkPlus.UI.Theme.SecondaryLabelBrush;
		}

		private bool TextIsTrimmed()
		{
			if (!(base.Parent is Panel panel))
			{
				return false;
			}
			double num = panel.Bounds.Width; // Migration note：WPF Panel.ActualWidth → Avalonia Panel.Bounds.Width
			foreach (global::Avalonia.Controls.Control child in panel.Children)
			{
				if (child != this)
				{
					num -= child.Bounds.Width + child.Margin.Left + child.Margin.Right;
				}
			}
			Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
			return num < base.DesiredSize.Width;
		}

		[Null]
		private string GetToolTipText()
		{
			string filePath = FilePath;
			if (filePath != null)
			{
				string oldFilePath = OldFilePath;
				if (oldFilePath != null)
				{
					return "Old:\t" + oldFilePath + Environment.NewLine + "New:\t" + filePath;
				}
				return filePath;
			}
			return null;
		}
	}
}
