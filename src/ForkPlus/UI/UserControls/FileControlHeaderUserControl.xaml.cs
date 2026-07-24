// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia + using Avalonia.Interactivity（RoutedEventArgs）
// - using System.Windows.Controls → using Avalonia.Controls
// - using System.Windows.Controls.Primitives → using Avalonia.Controls.Primitives（ToggleButton）
// - using System.Windows.Markup → 移除（IComponentConnector 不需要）
// - DependencyProperty.Register → AvaloniaProperty.Register<,>（StyledProperty<T>，参考 BindableGitPointView）
// - DependencyPropertyChangedEventArgs → AvaloniaPropertyChangedEventArgs（OnPropertyChanged override）
// - WeakEventManager<NotificationCenter, EventArgs<T>>.AddHandler(NotificationCenter.Current, "EventName", handler)
//   → NotificationCenter.Current.EventName += handler（参考 ForkPlusDialogWindow.ApplicationThemeChanged）
// - control.ToolTip = value → ToolTip.SetTip(control, value)（参考 PreferencesLocalization）
using System;
using System.ComponentModel;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.UserControls
{
	public partial class FileControlHeaderUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		private bool _highlightPixelsToggleButtonEnabled;

		// 阶段 4.5：WPF DependencyProperty.Register(name, typeof(T), typeof(Owner))
		// → Avalonia StyledProperty<T> + AvaloniaProperty.Register<Owner, T>(nameof(...))（参考 BindableGitPointView）。
		public static readonly StyledProperty<string> FilePathProperty = AvaloniaProperty.Register<FileControlHeaderUserControl, string>(nameof(FilePath));

		public static readonly StyledProperty<string> OldFilePathProperty = AvaloniaProperty.Register<FileControlHeaderUserControl, string>(nameof(OldFilePath));

		public FileDiffControlTarget Target { get; set; }

		public bool HighlightPixelsToggleButtonEnabled
		{
			get
			{
				return _highlightPixelsToggleButtonEnabled;
			}
			set
			{
				_highlightPixelsToggleButtonEnabled = value;
				UpdateHighlightPixelsToggleButtonState();
			}
		}

		private DiffLayoutMode DiffLayoutMode
		{
			get
			{
				switch (Target)
				{
				case FileDiffControlTarget.Commit:
					return ForkPlusSettings.Default.CommitDiffLayoutMode;
				case FileDiffControlTarget.History:
				case FileDiffControlTarget.HunkHistory:
					return ForkPlusSettings.Default.HistoryDiffLayoutMode;
				case FileDiffControlTarget.Popup:
					return ForkPlusSettings.Default.PopupDiffLayoutMode;
				case FileDiffControlTarget.Revision:
					return ForkPlusSettings.Default.RevisionDiffLayoutMode;
				case FileDiffControlTarget.RevisionWindow:
					return ForkPlusSettings.Default.RevisionWindowDiffLayoutMode;
				default:
					return ForkPlusSettings.Default.RevisionDiffLayoutMode;
				}
			}
			set
			{
				switch (Target)
				{
				case FileDiffControlTarget.Commit:
					ForkPlusSettings.Default.CommitDiffLayoutMode = value;
					break;
				case FileDiffControlTarget.History:
				case FileDiffControlTarget.HunkHistory:
					ForkPlusSettings.Default.HistoryDiffLayoutMode = value;
					break;
				case FileDiffControlTarget.Popup:
					ForkPlusSettings.Default.PopupDiffLayoutMode = value;
					break;
				case FileDiffControlTarget.Revision:
					ForkPlusSettings.Default.RevisionDiffLayoutMode = value;
					break;
				case FileDiffControlTarget.RevisionWindow:
					ForkPlusSettings.Default.RevisionWindowDiffLayoutMode = value;
					break;
				}
			}
		}

		private bool? DiffShowEntireFile
		{
			get
			{
				switch (Target)
				{
				case FileDiffControlTarget.Revision:
				case FileDiffControlTarget.Commit:
				case FileDiffControlTarget.Popup:
				case FileDiffControlTarget.History:
					return ForkPlusSettings.Default.DiffShowEntireFile;
				case FileDiffControlTarget.RevisionWindow:
					return ForkPlusSettings.Default.RevisionWindowDiffShowEntireFile;
				case FileDiffControlTarget.HunkHistory:
					return null;
				default:
					return ForkPlusSettings.Default.DiffShowEntireFile;
				}
			}
			set
			{
				switch (Target)
				{
				case FileDiffControlTarget.Revision:
				case FileDiffControlTarget.Commit:
				case FileDiffControlTarget.Popup:
				case FileDiffControlTarget.History:
					ForkPlusSettings.Default.DiffShowEntireFile = value.GetValueOrDefault();
					break;
				case FileDiffControlTarget.RevisionWindow:
					ForkPlusSettings.Default.RevisionWindowDiffShowEntireFile = value.GetValueOrDefault();
					break;
				case FileDiffControlTarget.HunkHistory:
					break;
				}
			}
		}

		public string FilePath
		{
			get
			{
				return GetValue(FilePathProperty);
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
				return GetValue(OldFilePathProperty);
			}
			set
			{
				SetValue(OldFilePathProperty, value);
			}
		}

		public FileControlHeaderUserControl()
		{
			InitializeComponent();
			ApplyLocalization();
			// 阶段 4.5：WPF WeakEventManager<NotificationCenter, EventArgs<T>>.AddHandler(NotificationCenter.Current, "EventName", handler)
			// → Avalonia NotificationCenter.Current.EventName += handler（参考 ForkPlusDialogWindow.ApplicationThemeChanged）。
			NotificationCenter.Current.DiffIgnoreWhitespacesChanged += delegate
			{
				UpdateIgnoreWhiteSpacesToggleButtonState();
			};
			NotificationCenter.Current.DiffShowHiddenSymbolsChanged += delegate
			{
				UpdateShowHiddenSymbolsToggleButtonState();
			};
			NotificationCenter.Current.DiffWordWrapChanged += delegate
			{
				UpdateDiffLayoutModeToggleButtonState();
				UpdateWordWrapToggleButtonState();
			};
			NotificationCenter.Current.DiffShowEntireFileChanged += delegate
			{
				UpdateShowEntireFileState();
			};
			NotificationCenter.Current.DiffLayoutModeChanged += delegate
			{
				UpdateDiffLayoutModeToggleButtonState();
				UpdateWordWrapToggleButtonState();
			};
			NotificationCenter.Current.ImageDiffHighlightPixelsChanged += delegate
			{
				UpdateHighlightPixelsToggleButtonState();
			};
			base.Loaded += delegate
			{
				UpdateIgnoreWhiteSpacesToggleButtonState();
				UpdateShowHiddenSymbolsToggleButtonState();
				UpdateWordWrapToggleButtonState();
				UpdateShowEntireFileState();
				UpdateDiffLayoutModeToggleButtonState();
			};
		}

		public void ApplyLocalization()
		{
			PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			UpdateDiffLayoutModeToggleButtonState();
		}

		public void Show(string filePath, [Null] string oldFilePath, FileControlHeaderMode mode = FileControlHeaderMode.None)
		{
			OldFilePath = oldFilePath;
			FilePath = filePath;
			RefreshToolbarLayout(mode);
			this.Show();
		}

		// 阶段 4.5：WPF OnPropertyChanged(DependencyPropertyChangedEventArgs e) → Avalonia OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)。
		protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
		{
			base.OnPropertyChanged(change);
			if (change.Property == OldFilePathProperty)
			{
				FilePathTextBlock.OldFilePath = OldFilePath;
			}
			else if (change.Property == FilePathProperty)
			{
				FilePathTextBlock.FilePath = FilePath;
				if (string.IsNullOrEmpty(FilePath))
				{
					FileTypeImage.Source = null;
				}
				else
				{
					// 阶段 4.5：IconTools.GetImageSourceForExtension 已返回 Avalonia IImage（参考 IconTools.cs），可直接赋给 Image.Source。
					FileTypeImage.Source = IconTools.GetImageSourceForExtension(Path.GetExtension(FilePath));
				}
			}
		}

		private void PreviousButton_Click(object sender, RoutedEventArgs e)
		{
			TargetTextDiffControl()?.ScrollToPreviousCustomHunk();
		}

		private void NextButton_Click(object sender, RoutedEventArgs e)
		{
			TargetTextDiffControl()?.ScrollToNextCustomHunk();
		}

		[Null]
		private TextDiffControl TargetTextDiffControl()
		{
			if (!(base.Parent is FileDiffControl fileDiffControl))
			{
				return null;
			}
			TextDiffControl[] array = fileDiffControl.Children.CompactMap((object x) => x as TextDiffControl);
			if (array.Length != 0)
			{
				return array[0];
			}
			return null;
		}

		private void DecreaseVisibleLines_Click(object sender, RoutedEventArgs e)
		{
			int num = ForkPlusSettings.Default.DiffContextSize - 1;
			ForkPlusSettings.Default.DiffContextSize = num;
			NotificationCenter.Current.RaiseDiffContextSizeChanged(this, num);
		}

		private void IncreaseVisibleLines_Click(object sender, RoutedEventArgs e)
		{
			int num = ForkPlusSettings.Default.DiffContextSize + 1;
			ForkPlusSettings.Default.DiffContextSize = num;
			NotificationCenter.Current.RaiseDiffContextSizeChanged(this, num);
		}

		private void IgnoreWhitespacesToggleButton_Click(object sender, RoutedEventArgs e)
		{
			bool valueOrDefault = IgnoreWhitespacesToggleButton.IsChecked.GetValueOrDefault();
			ForkPlusSettings.Default.DiffIgnoreWhitespaces = valueOrDefault;
			NotificationCenter.Current.RaiseDiffIgnoreWhitespacesChanged(this, valueOrDefault);
		}

		private void ShowHiddenSymbolsToggleButton_Click(object sender, RoutedEventArgs e)
		{
			bool valueOrDefault = ShowHiddenSymbolsToggleButton.IsChecked.GetValueOrDefault();
			ForkPlusSettings.Default.DiffShowHiddenSymbols = valueOrDefault;
			NotificationCenter.Current.RaiseDiffShowHiddenSymbolsChanged(this, valueOrDefault);
		}

		private void WordWrapToggleButton_Click(object sender, RoutedEventArgs e)
		{
			bool valueOrDefault = WordWrapToggleButton.IsChecked.GetValueOrDefault();
			ForkPlusSettings.Default.DiffWordWrap = valueOrDefault;
			NotificationCenter.Current.RaiseDiffWordWrapChanged(this, valueOrDefault);
		}

		private void ShowEntireFileToggleButton_Click(object sender, RoutedEventArgs e)
		{
			bool valueOrDefault = ShowEntireFileToggleButton.IsChecked.GetValueOrDefault();
			DiffShowEntireFile = valueOrDefault;
			NotificationCenter.Current.RaiseDiffShowEntireFileChanged(this, valueOrDefault);
		}

		private void DiffLayoutModeToggleButton_Click(object sender, RoutedEventArgs e)
		{
			DiffLayoutMode newValue = (DiffLayoutMode = (DiffLayoutModeToggleButton.IsChecked.GetValueOrDefault() ? DiffLayoutMode.SideBySide : DiffLayoutMode.Split));
			NotificationCenter.Current.RaiseDiffLayoutModeChanged(this, newValue);
		}

		private void HighlightPixelsToggleButton_Click(object sender, RoutedEventArgs e)
		{
			bool valueOrDefault = HighlightPixelsToggleButton.IsChecked.GetValueOrDefault();
			ForkPlusSettings.Default.ImageDiffHighlightPixels = valueOrDefault;
			NotificationCenter.Current.RaiseImageDiffHighlightPixelsChanged(this, valueOrDefault);
		}

		private void UpdateIgnoreWhiteSpacesToggleButtonState()
		{
			IgnoreWhitespacesToggleButton.IsChecked = ForkPlusSettings.Default.DiffIgnoreWhitespaces;
		}

		private void UpdateShowHiddenSymbolsToggleButtonState()
		{
			ShowHiddenSymbolsToggleButton.IsChecked = ForkPlusSettings.Default.DiffShowHiddenSymbols;
		}

		private void UpdateWordWrapToggleButtonState()
		{
			if (DiffLayoutMode == DiffLayoutMode.Split)
			{
				WordWrapToggleButton.IsChecked = ForkPlusSettings.Default.DiffWordWrap;
				WordWrapToggleButton.Enable();
			}
			else
			{
				WordWrapToggleButton.IsChecked = false;
				WordWrapToggleButton.Disable();
			}
		}

		private void UpdateShowEntireFileState()
		{
			bool? diffShowEntireFile = DiffShowEntireFile;
			if (diffShowEntireFile.HasValue)
			{
				bool valueOrDefault = diffShowEntireFile.GetValueOrDefault();
				ShowEntireFileToggleButton.IsEnabled = true;
				ShowEntireFileToggleButton.IsChecked = valueOrDefault;
				DecreaseNumberOfVisibleLinesButton.IsEnabled = !valueOrDefault;
				IncreaseNumberOfVisibleLinesButton.IsEnabled = !valueOrDefault;
			}
			else
			{
				ShowEntireFileToggleButton.IsEnabled = false;
				DecreaseNumberOfVisibleLinesButton.IsEnabled = false;
				IncreaseNumberOfVisibleLinesButton.IsEnabled = false;
			}
		}

		private void UpdateDiffLayoutModeToggleButtonState()
		{
			if (DiffLayoutMode == DiffLayoutMode.SideBySide)
			{
				DiffLayoutModeToggleButton.IsChecked = true;
				// 阶段 4.5：WPF control.ToolTip = value → Avalonia ToolTip.SetTip(control, value)（参考 PreferencesLocalization）。
				ToolTip.SetTip(DiffLayoutModeToggleButton, Translate("Split diff"));
			}
			else
			{
				DiffLayoutModeToggleButton.IsChecked = false;
				ToolTip.SetTip(DiffLayoutModeToggleButton, Translate("Side by side diff"));
			}
		}

		private void UpdateHighlightPixelsToggleButtonState()
		{
			if (HighlightPixelsToggleButtonEnabled)
			{
				HighlightPixelsToggleButton.Enable();
				HighlightPixelsToggleButton.IsChecked = ForkPlusSettings.Default.ImageDiffHighlightPixels;
			}
			else
			{
				HighlightPixelsToggleButton.Disable();
				HighlightPixelsToggleButton.IsChecked = false;
			}
		}

		private void RefreshToolbarLayout(FileControlHeaderMode mode)
		{
			switch (mode)
			{
			case FileControlHeaderMode.None:
				TextModeButtonsContainer.Collapse();
				TextModeNavigationButtonsContainer.Collapse();
				ImageModeButtonsContainer.Collapse();
				break;
			case FileControlHeaderMode.Text:
				TextModeButtonsContainer.Show();
				TextModeNavigationButtonsContainer.Show();
				ImageModeButtonsContainer.Collapse();
				break;
			case FileControlHeaderMode.Image:
				TextModeButtonsContainer.Collapse();
				TextModeNavigationButtonsContainer.Collapse();
				ImageModeButtonsContainer.Show();
				break;
			// v3.1.0：Hex 模式下不显示 Text/Image 工具栏按钮（HexContentControl 自带工具栏）
			case FileControlHeaderMode.Hex:
				TextModeButtonsContainer.Collapse();
				TextModeNavigationButtonsContainer.Collapse();
				ImageModeButtonsContainer.Collapse();
				break;
			}
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}

	}
}
