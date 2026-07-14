using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ForkPlus.Git.Commands;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.UserControls;

namespace ForkPlus.UI.Dialogs
{
	public class ForkPlusDialogWindow : CustomWindow
	{
		private static readonly Uri ForkPlusLogo = new Uri("pack://application:,,,/ForkPlus;component/Assets/ForkPlusIcon.png");

		public static readonly Uri WarningIcon = new Uri("pack://application:,,,/ForkPlus;component/Assets/Warning.png");

		public static readonly Uri ErrorIcon = new Uri("pack://application:,,,/ForkPlus;component/Assets/Error.png");

		public static readonly Uri SuccessIcon = new Uri("pack://application:,,,/ForkPlus;component/Assets/CheckMarkStroked.png");

		private Image _warningIcon;

		private bool _showWarningIcon;

		private bool _dialogChromeInitialized;

		private string _pendingDialogTitle;

		private string _pendingDialogDescription;

		private string _pendingSubmitButtonTitle;

		private string _pendingCancelButtonTitle;

		private bool? _pendingShowSubmitButton;

		private bool? _pendingShowCancelButton;

		private TextBlock _commandPreviewLabel;

		private TextBlock _commandPreviewTextBlock;

		private bool _commandPreviewInitialized;

		public bool IsOperationInProgress { get; private set; }

		protected new bool ShowHeader { get; set; } = true;


		protected bool ShowLogo { get; set; } = true;


		protected bool ShowFooter { get; set; } = true;


		public bool ShowWarningIcon
		{
			get
			{
				return _showWarningIcon;
			}
			set
			{
				if (_showWarningIcon != value)
				{
					_showWarningIcon = value;
					if (_showWarningIcon)
					{
						AddWarningIcon();
					}
					else
					{
						RemoveWarningIcon();
					}
				}
			}
		}

		protected ForkPlusDialogFooter Footer { get; private set; }

		protected TextBlock TitleTextBlock { get; private set; }

		protected TextBlock DescriptionTextBlock { get; private set; }

		public GitCommandResult GitResult { get; protected set; }

		protected string DialogTitle
		{
			get
			{
				return TitleTextBlock?.Text ?? _pendingDialogTitle;
			}
			set
			{
				_pendingDialogTitle = value;
				if (TitleTextBlock != null)
				{
					TitleTextBlock.Text = value;
				}
				base.Title = value;
			}
		}

		protected string DialogDescription
		{
			get
			{
				return DescriptionTextBlock?.Text ?? _pendingDialogDescription;
			}
			set
			{
				_pendingDialogDescription = value;
				if (DescriptionTextBlock != null)
				{
					DescriptionTextBlock.Text = value;
				}
			}
		}

		protected bool ShowSubmitButton
		{
			get
			{
				if (Footer == null)
				{
					return _pendingShowSubmitButton.GetValueOrDefault(true);
				}
				return Footer.SubmitButton.Visibility == Visibility.Visible;
			}
			set
			{
				_pendingShowSubmitButton = value;
				if (Footer != null)
				{
					Footer.SubmitButton.Visibility = ((!value) ? Visibility.Collapsed : Visibility.Visible);
				}
			}
		}

		protected string SubmitButtonTitle
		{
			get
			{
				return (Footer?.SubmitButton.Content as string) ?? _pendingSubmitButtonTitle;
			}
			set
			{
				_pendingSubmitButtonTitle = value;
				if (Footer != null)
				{
					Footer.SubmitButton.Content = value;
				}
			}
		}

		protected bool ShowCancelButton
		{
			get
			{
				if (Footer == null)
				{
					return _pendingShowCancelButton.GetValueOrDefault(true);
				}
				return Footer.CancelButton.Visibility == Visibility.Visible;
			}
			set
			{
				_pendingShowCancelButton = value;
				if (Footer != null)
				{
					Footer.CancelButton.Visibility = ((!value) ? Visibility.Collapsed : Visibility.Visible);
				}
			}
		}

		protected string CancelButtonTitle
		{
			get
			{
				return (Footer?.CancelButton.Content as string) ?? _pendingCancelButtonTitle;
			}
			set
			{
				_pendingCancelButtonTitle = value;
				if (Footer != null)
				{
					Footer.CancelButton.Content = value;
				}
			}
		}

		protected virtual bool IsSubmitAllowed => !IsOperationInProgress;

		protected virtual bool ApplyAutomaticLocalization => true;

		private bool IsWindowModal => ComponentDispatcher.IsThreadModal;

		private IEnumerable<UIElement> EditableControls => FindVisualChildren<Control>(this);

		private bool IsDesignMode => global::ForkPlus.DesignTimeHelper.IsInDesignMode();

		public ForkPlusDialogWindow(bool preventMainWindowRefresh = true)
		{
			base.OverridesDefaultStyle = true;
			if (!IsDesignMode)
			{
				MainWindow instance = MainWindow.Instance;
				if (instance != null)
				{
					base.Owner = instance;
					if (preventMainWindowRefresh)
					{
						instance.PreventRefreshAfterChildDialogClose(GetType().Name);
					}
				}
				base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
			}
			base.ShowInTaskbar = false;
			base.ResizeMode = ResizeMode.NoResize;
			base.Initialized += ForkPlusDialogWindow_Initialized;
			base.Loaded += ForkPlusDialogWindow_Loaded;
			base.Style = Application.Current?.TryFindResource("ForkPlusDialogWindowStyle") as Style;
			if (!IsDesignMode)
			{
				WeakEventManager<NotificationCenter, EventArgs<ThemeType>>.AddHandler(NotificationCenter.Current, "ApplicationThemeChanged", ApplicationThemeChanged);
			}
		}

		public void SetStatus(ForkPlusDialogStatus status, string message)
		{
			IsOperationInProgress = status == ForkPlusDialogStatus.InProgress;
			if (status == ForkPlusDialogStatus.None)
			{
				ClearStatus();
				return;
			}
			string localizedMessage = PreferencesLocalization.Translate(message, ForkPlusSettings.Default.UiLanguage);
			Footer.StatusMessageTextBlock.Text = localizedMessage;
			Footer.StatusMessageTextBlock.ToolTip = localizedMessage;
			Footer.StatusMessageTextBlock.Visibility = Visibility.Visible;
			if (status == ForkPlusDialogStatus.InProgress)
			{
				Footer.StatusImage.Visibility = Visibility.Collapsed;
				Footer.BusyIndicator.Visibility = Visibility.Visible;
				return;
			}
			Footer.BusyIndicator.Visibility = Visibility.Collapsed;
			Footer.StatusImage.Visibility = Visibility.Visible;
			switch (status)
			{
			case ForkPlusDialogStatus.Success:
				Footer.StatusImage.Source = new BitmapImage(SuccessIcon);
				break;
			case ForkPlusDialogStatus.Warning:
				Footer.StatusImage.Source = new BitmapImage(WarningIcon);
				break;
			case ForkPlusDialogStatus.Error:
				Footer.StatusImage.Source = new BitmapImage(ErrorIcon);
				break;
			}
		}

		public void ClearStatus()
		{
			Footer.StatusImage.Visibility = Visibility.Collapsed;
			Footer.StatusMessageTextBlock.Visibility = Visibility.Collapsed;
			Footer.BusyIndicator.Visibility = Visibility.Collapsed;
		}

		public void DisableEditableControls()
		{
			foreach (UIElement editableControl in EditableControls)
			{
				editableControl.Disable();
			}
			UpdateSubmitButton();
		}

		public void EnableEditableControls()
		{
			foreach (UIElement editableControl in EditableControls)
			{
				editableControl.Enable();
			}
			UpdateSubmitButton();
		}

		private void ForkPlusDialogWindow_Loaded(object sender, RoutedEventArgs e)
		{
			if (IsDesignMode)
			{
				return;
			}
			if (ApplyAutomaticLocalization)
			{
				PreferencesLocalization.Apply(this, ForkPlusSettings.Default.UiLanguage);
			}
			(base.Content as Grid)?.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
		}

		private void ForkPlusDialogWindow_Initialized(object sender, EventArgs e)
		{
			InitializeDialogChrome();
		}

		protected override void OnContentChanged(object oldContent, object newContent)
		{
			base.OnContentChanged(oldContent, newContent);
			if (IsInitialized)
			{
				InitializeDialogChrome();
			}
		}

		private void InitializeDialogChrome()
		{
			if (_dialogChromeInitialized)
			{
				return;
			}
			Grid obj = base.Content as Grid;
			if (obj == null)
			{
				return;
			}
			_dialogChromeInitialized = true;
			RefreshWindowSize();
			obj.Margin = new Thickness(20.0, 0.0, 20.0, 20.0);
			obj.Background = Theme.ForkPlusDialogBackgroundBrush;
			RenderOptions.SetClearTypeHint(obj, ClearTypeHint.Enabled);
			if (ShowHeader)
			{
				AddDialogHeader();
			}
			if (ShowLogo)
			{
				AddForkPlusLogo();
			}
			if (ShowFooter)
			{
				AddCommandPreview();
				AddFooter();
				UpdateSubmitButton();
			}
		}

		private void RefreshWindowSize()
		{
			double num = (double)ForkPlusSettings.Default.LayoutScaling * 0.01;
			base.Height *= num;
			base.Width *= num;
		}

		private void AddDialogHeader()
		{
			Grid obj = base.Content as Grid;
			if (obj == null)
			{
				return;
			}
			TextBlock textBlock = new TextBlock
			{
				FontWeight = FontWeights.Medium,
				FontSize = 15.0,
				Text = "[Dialog Title]"
			};
			TextBlock textBlock2 = new TextBlock
			{
				TextWrapping = TextWrapping.Wrap,
				FontSize = 13.0,
				Margin = new Thickness(0.0, 2.0, 0.0, 0.0),
				Foreground = (Application.Current.TryFindResource("ForkPlusDialogDescriptionForeground") as Brush),
				Text = "[Dialog Description]"
			};
			StackPanel stackPanel = new StackPanel();
			stackPanel.SetValue(Grid.RowProperty, 0);
			stackPanel.SetValue(Grid.ColumnProperty, 1);
			stackPanel.Children.Add(textBlock);
			stackPanel.Children.Add(textBlock2);
			obj.Children.Add(stackPanel);
			TitleTextBlock = textBlock;
			DescriptionTextBlock = textBlock2;
			if (_pendingDialogTitle != null)
			{
				DialogTitle = _pendingDialogTitle;
			}
			if (_pendingDialogDescription != null)
			{
				DialogDescription = _pendingDialogDescription;
			}
		}

		/// <summary>
	/// 子类重写以提供命令预览文本。返回 null 或空字符串则不显示预览区域。
	/// </summary>
	protected virtual string GetCommandPreview()
	{
		return null;
	}

	/// <summary>
	/// 刷新命令预览区域。子类在控件事件（TextChanged/SelectionChanged/Checked 等）中调用。
	/// </summary>
	protected void RefreshCommandPreview()
	{
		if (!_commandPreviewInitialized || _commandPreviewTextBlock == null)
		{
			return;
		}
		string text = GetCommandPreview();
		if (string.IsNullOrWhiteSpace(text))
		{
			_commandPreviewLabel.Visibility = Visibility.Collapsed;
			_commandPreviewTextBlock.Visibility = Visibility.Collapsed;
			_commandPreviewTextBlock.Text = "";
		}
		else
		{
			_commandPreviewLabel.Visibility = Visibility.Visible;
			_commandPreviewTextBlock.Visibility = Visibility.Visible;
			_commandPreviewTextBlock.Text = text;
		}
	}

	private void AddCommandPreview()
	{
		if (_commandPreviewInitialized)
		{
			return;
		}
		Grid grid = base.Content as Grid;
		if (grid == null)
		{
			return;
		}
		_commandPreviewInitialized = true;
		// 在 footer 行之前插入新行用于命令预览
		int previewRow = grid.RowDefinitions.Count;
		RowDefinition rowDefinition = new RowDefinition
		{
			Height = GridLength.Auto
		};
		grid.RowDefinitions.Add(rowDefinition);
		// 命令预览放在内容列（Column 1），与上方内容区使用一致的两列布局
		// （Auto 标签列 + * 输入列），使预览标签和文本与对话框内容对齐。
		// 此前 label 放在 Column 0（80px logo 列）导致与内容标签错位。
		Grid previewGrid = new Grid
		{
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
		};
		previewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
		previewGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		previewGrid.SetValue(Grid.RowProperty, previewRow);
		previewGrid.SetValue(Grid.ColumnProperty, 1);
		_commandPreviewLabel = new TextBlock
		{
			Text = PreferencesLocalization.Current("Git Command Preview"),
			FontSize = 13.0,
			FontWeight = FontWeights.Medium,
			VerticalAlignment = VerticalAlignment.Top,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 4.0, 8.0, 0.0),
			Visibility = Visibility.Collapsed
		};
		_commandPreviewLabel.SetValue(Grid.ColumnProperty, 0);
		previewGrid.Children.Add(_commandPreviewLabel);
		_commandPreviewTextBlock = new TextBlock
		{
			FontFamily = new FontFamily("Consolas"),
			FontSize = 12.0,
			TextWrapping = TextWrapping.Wrap,
			Foreground = (Application.Current.TryFindResource("SecondaryLabelBrush") as Brush),
			Margin = new Thickness(8.0, 4.0, 0.0, 0.0),
			Visibility = Visibility.Collapsed
		};
		_commandPreviewTextBlock.SetValue(Grid.ColumnProperty, 1);
		previewGrid.Children.Add(_commandPreviewTextBlock);
		grid.Children.Add(previewGrid);
		// 初始刷新
		RefreshCommandPreview();
	}

	private void AddFooter()
		{
			Grid grid = base.Content as Grid;
			if (grid == null)
			{
				return;
			}
			ForkPlusDialogFooter forkDialogFooter = new ForkPlusDialogFooter();
		if (grid.RowDefinitions.Count <= 0)
		{
			grid.RowDefinitions.Add(new RowDefinition());
		}
		// 若最后一行已被命令预览占用（AddCommandPreview 先于 AddFooter 执行），则新增一行放 footer
		int footerRow = grid.RowDefinitions.Count - 1;
		bool lastRowOccupied = false;
		foreach (UIElement child in grid.Children)
		{
			int row = (int)child.GetValue(Grid.RowProperty);
			if (row == footerRow)
			{
				lastRowOccupied = true;
				break;
			}
		}
		if (lastRowOccupied)
		{
			grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
			footerRow = grid.RowDefinitions.Count - 1;
		}
		forkDialogFooter.SetValue(Grid.RowProperty, footerRow);
			forkDialogFooter.SetValue(Grid.ColumnProperty, 0);
			forkDialogFooter.SetValue(Grid.ColumnSpanProperty, 2);
			grid.Children.Add(forkDialogFooter);
			forkDialogFooter.Cancel += delegate
			{
				OnCancel();
			};
			forkDialogFooter.Submit += delegate
			{
				OnSubmit();
			};
			Footer = forkDialogFooter;
			if (_pendingSubmitButtonTitle != null)
			{
				SubmitButtonTitle = _pendingSubmitButtonTitle;
			}
			if (_pendingCancelButtonTitle != null)
			{
				CancelButtonTitle = _pendingCancelButtonTitle;
			}
			if (_pendingShowSubmitButton.HasValue)
			{
				ShowSubmitButton = _pendingShowSubmitButton.Value;
			}
			if (_pendingShowCancelButton.HasValue)
			{
				ShowCancelButton = _pendingShowCancelButton.Value;
			}
		}

		private void AddForkPlusLogo()
		{
			Grid obj = base.Content as Grid;
			if (obj == null)
			{
				return;
			}
			Image image = new Image
			{
				Source = new BitmapImage(ForkPlusLogo),
				Width = 64.0,
				Height = 64.0,
				HorizontalAlignment = HorizontalAlignment.Left,
				VerticalAlignment = VerticalAlignment.Top
			};
			image.SetValue(Grid.RowSpanProperty, 2);
			obj.Children.Add(image);
		}

		private void AddWarningIcon()
		{
			if (_warningIcon == null)
			{
				Grid obj = base.Content as Grid;
				if (obj == null)
				{
					return;
				}
				_warningIcon = new Image
				{
					Source = new BitmapImage(WarningIcon),
					Width = 24.0,
					Height = 24.0,
					HorizontalAlignment = HorizontalAlignment.Left,
					VerticalAlignment = VerticalAlignment.Top,
					Margin = new Thickness(38.0, 38.0, 0.0, 0.0)
				};
				_warningIcon.SetValue(Grid.RowSpanProperty, 2);
				obj.Children.Add(_warningIcon);
			}
		}

		private void RemoveWarningIcon()
		{
			if (_warningIcon != null)
			{
				(base.Content as Grid)?.Children.Remove(_warningIcon);
				_warningIcon = null;
			}
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			if (ShowFooter && ShowCancelButton && e.Key == Key.Escape)
			{
				OnCancel();
				e.Handled = true;
			}
			else
			{
				base.OnKeyDown(e);
			}
		}

		protected virtual void OnCancel()
		{
			if (base.IsVisible)
			{
				if (IsWindowModal)
				{
					base.DialogResult = false;
				}
				else
				{
					Close();
				}
			}
		}

		protected void Close(GitCommandResult gitResult)
		{
			GitResult = gitResult;
			CloseWithOk();
		}

		protected virtual void OnSubmit()
		{
			CloseWithOk();
		}

		protected void CloseWithOk()
		{
			if (base.IsVisible)
			{
				if (IsWindowModal)
				{
					base.DialogResult = true;
				}
				else
				{
					Close();
				}
			}
		}

		protected void UpdateSubmitButton()
		{
			if (Footer?.SubmitButton != null)
			{
				Footer.SubmitButton.IsEnabled = IsSubmitAllowed;
			}
		}

		private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
		{
			if (depObj == null)
			{
				yield break;
			}
			for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
			{
				DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
				if (child is T typedChild)
				{
					yield return typedChild;
				}
				foreach (T childOfChild in FindVisualChildren<T>(child))
				{
					yield return childOfChild;
				}
			}
		}

		private void ApplicationThemeChanged(object sender, EventArgs<ThemeType> e)
		{
			RefreshBrushes();
			InvalidateVisual();
		}

		private void RefreshBrushes()
		{
			Grid obj = base.Content as Grid;
			if (obj != null)
			{
				obj.Background = Theme.ForkPlusDialogBackgroundBrush;
			}
		}
	}
}

