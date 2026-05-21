using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace ForkPlus.UI.Controls
{
	public class FilePathTextBlock : SelectableTextBlock
	{
		public static readonly DependencyProperty FilePathProperty = DependencyProperty.RegisterAttached("FilePath", typeof(string), typeof(FilePathTextBlock), new PropertyMetadata(delegate(DependencyObject s, DependencyPropertyChangedEventArgs e)
		{
			(s as FilePathTextBlock).Refresh();
		}));

		public static readonly DependencyProperty OldFilePathProperty = DependencyProperty.RegisterAttached("OldFilePath", typeof(string), typeof(FilePathTextBlock));

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
			base.MouseEnter += delegate(object s, MouseEventArgs e)
			{
				e.Handled = true;
				base.ToolTip = (TextIsTrimmed() ? GetToolTipText() : null);
			};
			WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
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
			_labelBrush = Theme.LabelBrush;
			_secondaryLabelBrush = Theme.SecondaryLabelBrush;
		}

		private bool TextIsTrimmed()
		{
			if (!(base.Parent is Panel { ActualWidth: var num } panel))
			{
				return false;
			}
			foreach (FrameworkElement child in panel.Children)
			{
				if (child != this)
				{
					num -= child.ActualWidth + child.Margin.Left + child.Margin.Right;
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
