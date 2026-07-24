using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace ForkPlus.UI.Controls
{
	public class FilePathTextBlock : SelectableTextBlock
	{
		public static readonly StyledProperty<string> FilePathProperty =
			AvaloniaProperty.Register<FilePathTextBlock, string>(nameof(FilePath));

		public static readonly StyledProperty<string> OldFilePathProperty =
			AvaloniaProperty.Register<FilePathTextBlock, string>(nameof(OldFilePath));

		private IBrush _labelBrush;

		private IBrush _secondaryLabelBrush;

		public string FilePath
		{
			get => GetValue(FilePathProperty);
			set => SetValue(FilePathProperty, value);
		}

		public string OldFilePath
		{
			get => GetValue(OldFilePathProperty);
			set => SetValue(OldFilePathProperty, value);
		}

		public FilePathTextBlock()
		{
			RefreshBrushes();
			// 阶段 4.5：WPF MouseEnter → Avalonia PointerEnter。
			PointerEnter += delegate(object s, PointerEventArgs e)
			{
				e.Handled = true;
				ToolTip.SetTip(this, TextIsTrimmed() ? GetToolTipText() : null);
			};
			// 阶段 4.5：WPF WeakEventManager → 直接事件订阅。
			NotificationCenter.Current.ApplicationThemeChanged += ApplicationThemeChanged;
		}

		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == FilePathProperty || change.Property == OldFilePathProperty)
			{
				Refresh();
			}
		}

		private void Refresh()
		{
			base.Inlines!.Clear();
			string oldFilePath = OldFilePath;
			if (oldFilePath != null)
			{
				string readableFileName = PathHelper.GetReadableFileName(oldFilePath);
				int num = oldFilePath.Length - readableFileName.Length;
				if (num != 0)
				{
					// TODO(4.5-g): Avalonia Run 不支持 Foreground 属性。路径着色待后续自定义渲染恢复。
					base.Inlines.Add(new Run(oldFilePath.Substring(0, num)));
				}
				base.Inlines.Add(new Run(readableFileName));
				base.Inlines.Add(new Run(" → "));
			}
			string filePath = FilePath;
			if (filePath != null)
			{
				string readableFileName2 = PathHelper.GetReadableFileName(filePath);
				int num2 = filePath.Length - readableFileName2.Length;
				if (num2 != 0)
				{
					base.Inlines.Add(new Run(filePath.Substring(0, num2)));
				}
				base.Inlines.Add(new Run(readableFileName2));
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
			// 阶段 4.5：WPF Panel.ActualWidth + FrameworkElement.ActualWidth → Avalonia Bounds.Width + Control.Bounds.Width。
			if (!(base.Parent is Panel panel))
			{
				return false;
			}
			double num = panel.Bounds.Width;
			foreach (Control child in panel.Children)
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
