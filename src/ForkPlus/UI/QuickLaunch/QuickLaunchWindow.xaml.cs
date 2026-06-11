using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using ForkPlus.Git;
using ForkPlus.Settings;
using ForkPlus.UI.Commands;
using ForkPlus.UI.Controls;
using ForkPlus.UI.UserControls;
using ForkPlus.UI.UserControls.Preferences;

namespace ForkPlus.UI.QuickLaunch
{
	public partial class QuickLaunchWindow : CustomWindow
	{
		private bool _closing;

		private ICommandProvider _currentCommandProvider = new DummyCommandProvider();

		private readonly DelayedAction<bool> _refreshCommandListAction;

		private bool _showCheckout;

		private RepositoryUserControl RepositoryUserControl => Application.Current.ActiveRepositoryUserControl();

		public QuickLaunchWindow(bool showCheckout = false)
		{
			InitializeComponent();
			if (global::ForkPlus.DesignTimeHelper.IsInDesignMode())
			{
				base.Title = PreferencesLocalization.Current("Quick Launch");
				return;
			}
			base.Owner = MainWindow.Instance;
			_showCheckout = showCheckout;
			_refreshCommandListAction = new DelayedAction<bool>(RefreshCommandList, 0.1);
			base.Loaded += delegate
			{
				_refreshCommandListAction.InvokeNow(parameter: false);
			};
			base.Deactivated += delegate
			{
				base.Dispatcher.Async(delegate
				{
					CloseWindow();
				});
			};
			CommandTextBox commandTextBox = CommandTextBox;
			commandTextBox.CommandArgumentsCompleted = (EventHandler<object[]>)Delegate.Combine(commandTextBox.CommandArgumentsCompleted, (EventHandler<object[]>)delegate(object s, object[] e)
			{
				CloseWindow();
				Application.Current.Dispatcher.BeginInvoke((Action)delegate
				{
					CommandTextBox.CommandDescriptor.Converter(e, RepositoryUserControl);
				});
			});
			CommandTextBox.TextChanged += delegate
			{
				_refreshCommandListAction.InvokeWithDelay(parameter: false);
			};
			CommandTextBox commandTextBox2 = CommandTextBox;
			commandTextBox2.CommandArgumentChanged = (EventHandler)Delegate.Combine(commandTextBox2.CommandArgumentChanged, (EventHandler)delegate
			{
				_refreshCommandListAction.InvokeNow(parameter: false);
			});
			Task.Run(delegate
			{
				new RescanUserRepositoriesCommand().Execute(reset: false);
				base.Dispatcher.Async(delegate
				{
					_refreshCommandListAction.InvokeNow(parameter: false);
				});
			});
		}

		protected override void OnPreviewKeyDown(KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				e.Handled = true;
				CloseWindow();
				return;
			}
			if (e.Key == Key.Return)
			{
				e.Handled = true;
				SubmitSelectedItem();
			}
			else
			{
				if (e.Key == Key.Down)
				{
					e.Handled = true;
					RepositoriesListBox.SelectNextRow(RepositoriesListBox.SelectedIndex, loop: true, (object x) => !(x is HeaderCommandProviderItem));
					return;
				}
				if (e.Key == Key.Up)
				{
					e.Handled = true;
					RepositoriesListBox.SelectPreviousRow(RepositoriesListBox.SelectedIndex, loop: true, (object x) => !(x is HeaderCommandProviderItem));
					return;
				}
			}
			base.OnPreviewKeyDown(e);
		}

		private void RepositoriesListBox_MouseUp(object sender, MouseButtonEventArgs e)
		{
			SubmitSelectedItem();
		}

		private void SubmitSelectedItem()
		{
			if (CommandTextBox.Text == "ftrace")
			{
				EnableDebugMode();
				CloseWindow();
			}
			else if (CommandTextBox.Text == "crash")
			{
				MainWindow.Commands.SendCrashReport.Execute();
				CloseWindow();
			}
			else
			{
				if (!(RepositoriesListBox?.SelectedItem is CommandProviderItem commandProviderItem))
				{
					return;
				}
				if (CommandTextBox.CommandDescriptor == null)
				{
					if (commandProviderItem is PaletteCommandItem paletteCommandItem)
					{
						if (paletteCommandItem.Command.Arguments.Length == 0)
						{
							CloseWindow();
							paletteCommandItem.Command.Converter(new object[0], RepositoryUserControl);
						}
						else
						{
							CommandTextBox.SetCommandDescriptor(paletteCommandItem.Command);
						}
					}
					else if (commandProviderItem is RepositoryInfoItem repositoryInfoItem)
					{
						Keyboard.IsKeyDown(Key.LeftCtrl);
						CloseWindow();
						RepositoryManagerUserControl.Commands.OpenRepository.Execute(repositoryInfoItem.Repository);
					}
					else
					{
						CloseWindow();
						Log.Error("root item must be a command or a repo");
					}
				}
				else
				{
					CommandTextBox.MoveNextArgument(commandProviderItem);
				}
			}
		}

		private void RefreshCommandList(bool _)
		{
			_currentCommandProvider = RefreshCommandProvider(CommandTextBox.CurrentCommandArgument);
			string filterString = CommandTextBox.Text.Trim().ToLower();
			_currentCommandProvider.Refresh(filterString);
			RepositoriesListBox.ItemsSource = _currentCommandProvider.Items;
			if (_showCheckout)
			{
				_showCheckout = false;
				if (IReadOnlyListExtensions.FirstItem(_currentCommandProvider.Items, (CommandProviderItem x) => x is PaletteCommandItem paletteCommandItem && paletteCommandItem.Command.Name == "Checkout Branch") is PaletteCommandItem item)
				{
					int row = RepositoriesListBox.Items.IndexOf(item);
					RepositoriesListBox.SelectAndScrollIntoView(row, focus: false);
					SubmitSelectedItem();
				}
			}
			RepositoriesListBox.SelectNextRow(0, loop: true, (object x) => !(x is HeaderCommandProviderItem));
		}

		private ICommandProvider RefreshCommandProvider(Argument argument)
		{
			if (argument == null)
			{
				if (_currentCommandProvider is DefaultCommandProvider)
				{
					return _currentCommandProvider;
				}
				return new DefaultCommandProvider(RepositoryUserControl?.RepositoryData);
			}
			if (argument.Type == _currentCommandProvider.Type)
			{
				return _currentCommandProvider;
			}
			RepositoryData repositoryData = RepositoryUserControl?.RepositoryData;
			if (repositoryData != null)
			{
				GitModule gitModule = RepositoryUserControl?.GitModule;
				if (gitModule != null)
				{
					switch (argument.Type)
					{
					case ArgumentType.RepositoryFile:
						return new RepositoryFileCommandProvider(gitModule);
					case ArgumentType.Remote:
						return new RemoteCommandProvider(repositoryData);
					case ArgumentType.Reference:
					case ArgumentType.Tag:
					case ArgumentType.Branch:
					case ArgumentType.LocalBranch:
					case ArgumentType.RemoteBranch:
						return new ReferenceCommandProvider(repositoryData, argument);
					case ArgumentType.FeatureBranch:
					case ArgumentType.HotfixBranch:
					case ArgumentType.ReleaseBranch:
						return new GitFlowCommandProvider(repositoryData.References.LocalBranches, repositoryData.GitFlowSettings, argument.Type);
					case ArgumentType.Workspace:
						return new WorkspaceCommandProvider(ForkPlusSettings.Default.Workspaces.All);
					default:
						return new DefaultCommandProvider(RepositoryUserControl?.RepositoryData);
					}
				}
			}
			if (argument.Type == ArgumentType.Workspace)
			{
				return new WorkspaceCommandProvider(ForkPlusSettings.Default.Workspaces.All);
			}
			return _currentCommandProvider;
		}

		private void CloseWindow()
		{
			if (_closing)
			{
				return;
			}
			_closing = true;
			try
			{
				Close();
			}
			catch (Exception ex)
			{
				Log.Warn("Failed to close window", ex);
			}
		}

		private void EnableDebugMode()
		{
			MainWindow.Commands.ToggleTraceElapsedTime.Execute();
		}

	}
}
