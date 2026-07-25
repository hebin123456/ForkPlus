// 阶段 4.5：WPF→Avalonia 迁移。
// - using System.Windows → using Avalonia
// - using System.Windows.Controls → using Avalonia.Controls
// - using System.Windows.Input → using Avalonia.Input（KeyEventArgs / Key / PointerReleasedEventArgs）
// - using System.Windows.Markup → 移除（无对应使用）
// - 新增 using Avalonia.Threading（Dispatcher.UIThread）
// - Application.Current.Dispatcher.BeginInvoke(action) → Dispatcher.UIThread.Post(action)
//   （Application.Current.Dispatcher 在 Avalonia 不存在，改用 Dispatcher.UIThread，参考 SystemThemeHelper / RevisionsDataSource）
// - base.Dispatcher.Async 保持（自定义扩展 DispatcherExtension.Async，内部转发 Dispatcher.Post）
// - OnPreviewKeyDown (tunneling) → OnKeyDown (bubbling)（Avalonia 无 Preview 前缀，参考 AutoCompleteTextBox / ReferenceTextBox）
// - PointerPressedEventArgs → PointerReleasedEventArgs（MouseUp → PointerReleased；XAML 需同步迁移 MouseUp→PointerReleased）
// - Keyboard.IsKeyDown(Key.LeftCtrl) 为无副作用死语句（结果未使用），迁移时移除
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
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
				// 阶段 4.5：WPF Application.Current.Dispatcher.BeginInvoke → Avalonia Dispatcher.UIThread.Post。
				// 关窗后再执行命令转换；用全局 UI 线程调度而非 base.Dispatcher，避免窗口关闭后 Dispatcher 失效。
				Dispatcher.UIThread.Post((Action)delegate
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

		// 阶段 4.5：WPF OnPreviewKeyDown (tunneling) → Avalonia OnKeyDown (bubbling)。
		// TODO(4.6-c): bubbling 在子控件已处理按键时不再触发本回调；若 CommandTextBox 拦截
		// Escape/Return/Up/Down，需改用 AddHandler(KeyDownEvent, handler, handledEventsToo: true)。
		protected override void OnKeyDown(KeyEventArgs e)
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
			base.OnKeyDown(e);
		}

		// 阶段 4.5：WPF MouseUp + PointerPressedEventArgs → Avalonia PointerReleased + PointerReleasedEventArgs（XAML 需同步迁移）。
		private void RepositoriesListBox_MouseUp(object sender, PointerReleasedEventArgs e)
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
						// 原始 WPF Keyboard.IsKeyDown(Key.LeftCtrl) 为无副作用死语句（结果未使用），迁移时移除。
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
