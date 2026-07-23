using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.Dialogs
{
	public partial class LongOperationWindow : ForkPlusDialogWindow
	{
		private readonly Func<Task> _operation;

		private Exception _exception;

		public LongOperationWindow(string title, string description, Action operation)
			: this(title, description, operation == null ? null : new Func<Task>(delegate
				{
					operation();
					return Task.CompletedTask;
				}))
		{
		}

		public LongOperationWindow(string title, string description, Func<Task> operation)
			: base(preventMainWindowRefresh: false)
		{
			_operation = operation ?? throw new ArgumentNullException(nameof(operation));
			InitializeComponent();
			base.DialogTitle = Translate(title);
			base.DialogDescription = Translate(description);
			base.ShowSubmitButton = false;
			base.ShowCancelButton = false;
			MessageTextBlock.Text = Translate("This operation is taking longer than usual. Please wait.");
			base.Loaded += LongOperationWindow_Loaded;
		}

		public static void Run(string title, string description, Action operation)
		{
			LongOperationWindow window = new LongOperationWindow(title, description, operation);
			window.ShowDialog();
			if (window._exception != null)
			{
				ExceptionDispatchInfo.Capture(window._exception).Throw();
			}
		}

		public static void RunAsync(string title, string description, Func<Task> operation)
		{
			LongOperationWindow window = new LongOperationWindow(title, description, operation);
			window.ShowDialog();
			if (window._exception != null)
			{
				ExceptionDispatchInfo.Capture(window._exception).Throw();
			}
		}

		private void LongOperationWindow_Loaded(object sender, RoutedEventArgs e)
		{
			Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async delegate
			{
				try
				{
					await _operation();
				}
				catch (Exception ex)
				{
					_exception = ex;
				}
				finally
				{
					CloseWithOk();
				}
			}));
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
