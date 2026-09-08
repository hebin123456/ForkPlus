using System;
using System.Threading.Tasks;
using ForkPlus.UI.WpfCompat;
using System.ComponentModel;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Markup;
using ForkPlus.Settings;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Controls.Editor.Diff;
using ForkPlus.UI.UserControls.Preferences;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Interactivity;

namespace ForkPlus.UI.UserControls
{
	public partial class FileControlHeaderUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		private bool _highlightPixelsToggleButtonEnabled;

		private static readonly object SettingsSaveLock = new object();

		public static readonly global::Avalonia.StyledProperty<string> FilePathProperty =
    global::Avalonia.AvaloniaProperty.Register<FileControlHeaderUserControl, string>("FilePath");

		public static readonly global::Avalonia.StyledProperty<string> OldFilePathProperty =
    global::Avalonia.AvaloniaProperty.Register<FileControlHeaderUserControl, string>("OldFilePath");

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

		public FileControlHeaderUserControl()
		{
			InitializeComponent();
			ApplyLocalization();
			WeakEventManager<NotificationCenter, EventArgs<bool>>.AddHandler(NotificationCenter.Current,"DiffIgnoreWhitespacesChanged",delegate
(object sender, global::System.EventArgs e)			{
				UpdateIgnoreWhiteSpacesToggleButtonState();
			});
			WeakEventManager<NotificationCenter, EventArgs<bool>>.AddHandler(NotificationCenter.Current,"DiffShowHiddenSymbolsChanged",delegate
(object sender, global::System.EventArgs e)			{
				UpdateShowHiddenSymbolsToggleButtonState();
			});
			WeakEventManager<NotificationCenter, EventArgs<bool>>.AddHandler(NotificationCenter.Current,"DiffWordWrapChanged",delegate
(object sender, global::System.EventArgs e)			{
				UpdateDiffLayoutModeToggleButtonState();
				UpdateWordWrapToggleButtonState();
			});
			WeakEventManager<NotificationCenter, EventArgs<bool>>.AddHandler(NotificationCenter.Current,"DiffShowEntireFileChanged",delegate
(object sender, global::System.EventArgs e)			{
				UpdateShowEntireFileState();
			});
			WeakEventManager<NotificationCenter, EventArgs<DiffLayoutMode>>.AddHandler(NotificationCenter.Current,"DiffLayoutModeChanged",delegate
(object sender, global::System.EventArgs e)			{
				UpdateDiffLayoutModeToggleButtonState();
				UpdateWordWrapToggleButtonState();
			});
			WeakEventManager<NotificationCenter, EventArgs<bool>>.AddHandler(NotificationCenter.Current,"ImageDiffHighlightPixelsChanged",delegate
(object sender, global::System.EventArgs e)			{
				UpdateHighlightPixelsToggleButtonState();
			});
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

		protected override void OnPropertyChanged(global::Avalonia.AvaloniaPropertyChangedEventArgs e)
		{
			base.OnPropertyChanged(e);
			if (e.Property == OldFilePathProperty)
			{
				FilePathTextBlock.OldFilePath = OldFilePath;
			}
			else if (e.Property == FilePathProperty)
			{
				FilePathTextBlock.FilePath = FilePath;
				if (string.IsNullOrEmpty(FilePath))
				{
					FileTypeImage.Source = null;
				}
				else
				{
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
			if (fileDiffControl.CurrentSubView is TextDiffControl currentTextDiffControl)
			{
				return currentTextDiffControl;
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
			SaveSettingsAsync();
			NotificationCenter.Current.RaiseDiffIgnoreWhitespacesChanged(this, valueOrDefault);
		}

		private void ShowHiddenSymbolsToggleButton_Click(object sender, RoutedEventArgs e)
		{
			bool valueOrDefault = ShowHiddenSymbolsToggleButton.IsChecked.GetValueOrDefault();
			ForkPlusSettings.Default.DiffShowHiddenSymbols = valueOrDefault;
			SaveSettingsAsync();
			NotificationCenter.Current.RaiseDiffShowHiddenSymbolsChanged(this, valueOrDefault);
		}

		private void WordWrapToggleButton_Click(object sender, RoutedEventArgs e)
		{
			bool valueOrDefault = WordWrapToggleButton.IsChecked.GetValueOrDefault();
			ForkPlusSettings.Default.DiffWordWrap = valueOrDefault;
			SaveSettingsAsync();
			NotificationCenter.Current.RaiseDiffWordWrapChanged(this, valueOrDefault);
		}

		private void ShowEntireFileToggleButton_Click(object sender, RoutedEventArgs e)
		{
			bool valueOrDefault = ShowEntireFileToggleButton.IsChecked.GetValueOrDefault();
			DiffShowEntireFile = valueOrDefault;
			SaveSettingsAsync();
			NotificationCenter.Current.RaiseDiffShowEntireFileChanged(this, valueOrDefault);
		}

		private void DiffLayoutModeToggleButton_Click(object sender, RoutedEventArgs e)
		{
			DiffLayoutMode newValue = (DiffLayoutMode = (DiffLayoutModeToggleButton.IsChecked.GetValueOrDefault() ? DiffLayoutMode.SideBySide : DiffLayoutMode.Split));
			SaveSettingsAsync();
			NotificationCenter.Current.RaiseDiffLayoutModeChanged(this, newValue);
		}

		private void HighlightPixelsToggleButton_Click(object sender, RoutedEventArgs e)
		{
			bool valueOrDefault = HighlightPixelsToggleButton.IsChecked.GetValueOrDefault();
			ForkPlusSettings.Default.ImageDiffHighlightPixels = valueOrDefault;
			SaveSettingsAsync();
			NotificationCenter.Current.RaiseImageDiffHighlightPixelsChanged(this, valueOrDefault);
		}

		private void UpdateIgnoreWhiteSpacesToggleButtonState()
		{
			bool isChecked = ForkPlusSettings.Default.DiffIgnoreWhitespaces;
			IgnoreWhitespacesToggleButton.IsChecked = isChecked;
			SetToggleImageResource(IgnoreWhitespacesImage, isChecked, "IgnoreWhiteSpacesOnIcon", "IgnoreWhiteSpacesIcon");
		}

		private void UpdateShowHiddenSymbolsToggleButtonState()
		{
			bool isChecked = ForkPlusSettings.Default.DiffShowHiddenSymbols;
			ShowHiddenSymbolsToggleButton.IsChecked = isChecked;
			SetToggleImageResource(ShowHiddenSymbolsImage, isChecked, "ShowHiddenSymbolsOnIcon", "ShowHiddenSymbolsIcon");
		}

		private void UpdateWordWrapToggleButtonState()
		{
			if (DiffLayoutMode == DiffLayoutMode.Split)
			{
				bool isChecked = ForkPlusSettings.Default.DiffWordWrap;
				WordWrapToggleButton.IsChecked = isChecked;
				SetToggleImageResource(WordWrapImage, isChecked, "WordWrapOnIcon", "WordWrapIcon");
				WordWrapToggleButton.Enable();
			}
			else
			{
				WordWrapToggleButton.IsChecked = false;
				SetToggleImageResource(WordWrapImage, isChecked: false, "WordWrapOnIcon", "WordWrapIcon");
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
				SetToggleImageResource(ShowEntireFileImage, valueOrDefault, "ShowEntireFileOnIcon", "ShowEntireFileIcon");
				DecreaseNumberOfVisibleLinesButton.IsEnabled = !valueOrDefault;
				IncreaseNumberOfVisibleLinesButton.IsEnabled = !valueOrDefault;
			}
			else
			{
				ShowEntireFileToggleButton.IsEnabled = false;
				DecreaseNumberOfVisibleLinesButton.IsEnabled = false;
				IncreaseNumberOfVisibleLinesButton.IsEnabled = false;
				SetToggleImageResource(ShowEntireFileImage, isChecked: false, "ShowEntireFileOnIcon", "ShowEntireFileIcon");
			}
		}

		private void UpdateDiffLayoutModeToggleButtonState()
		{
			if (DiffLayoutMode == DiffLayoutMode.SideBySide)
			{
				DiffLayoutModeToggleButton.IsChecked = true;
				SetToggleImageResource(DiffLayoutModeImage, isChecked: true, "SideBySideOnIcon", "SideBySideIcon");
				global::Avalonia.Controls.ToolTip.SetTip(DiffLayoutModeToggleButton,Translate("Split diff"));
			}
			else
			{
				DiffLayoutModeToggleButton.IsChecked = false;
				SetToggleImageResource(DiffLayoutModeImage, isChecked: false, "SideBySideOnIcon", "SideBySideIcon");
				global::Avalonia.Controls.ToolTip.SetTip(DiffLayoutModeToggleButton,Translate("Side by side diff"));
			}
		}

		private void UpdateHighlightPixelsToggleButtonState()
		{
			if (HighlightPixelsToggleButtonEnabled)
			{
				HighlightPixelsToggleButton.Enable();
				bool isChecked = ForkPlusSettings.Default.ImageDiffHighlightPixels;
				HighlightPixelsToggleButton.IsChecked = isChecked;
				SetToggleImageResource(HighlightPixelsImage, isChecked, "HighlightPixelsOnIcon", "HighlightPixelsIcon");
			}
			else
			{
				HighlightPixelsToggleButton.Disable();
				HighlightPixelsToggleButton.IsChecked = false;
				SetToggleImageResource(HighlightPixelsImage, isChecked: false, "HighlightPixelsOnIcon", "HighlightPixelsIcon");
			}
		}

		private static void SetToggleImageResource(Image image, bool isChecked, string checkedResourceKey, string uncheckedResourceKey)
		{
			image.SetResourceReference(Image.SourceProperty, isChecked ? checkedResourceKey : uncheckedResourceKey);
		}

		private static void SaveSettingsAsync()
		{
			Task.Run(delegate
			{
				lock (SettingsSaveLock)
				{
					ForkPlusSettings.Default.Save();
				}
			});
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
