using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Interactivity;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.Plugins;

namespace ForkPlus.UI.UserControls.Preferences
{
	/// <summary>
	/// 偏好设置 &gt; 插件（Plugins）：对比视图插件管理页（v5.0.1 起，v5.0.3 增安装/卸载）。
	///
	/// 契约：插件由 <see cref="DiffViewPluginLoader"/> 在启动期从可执行文件旁的 plugins/
	/// 目录动态加载；本页只做展示、状态切换与安装/卸载，不参与插件实例化。
	///
	/// 能力：
	/// - 列出已注册插件（按优先级）与加载失败的 DLL，展示名称、版本号、描述、匹配扩展名与优先级；
	/// - 启用/禁用开关：即时写入注册表并持久化（<see cref="DiffViewPluginLoader.SetPluginEnabled"/>），
	///   禁用仅退出路由，条目保留、可随时恢复；
	/// - 「安装插件...」：选择插件压缩包（zip），由 <see cref="PluginPackageInstaller"/> 解压并
	///   安装到 plugins/ 目录后重新加载；
	/// - 「卸载选中」：勾选若干第三方插件批量卸载（<see cref="PluginUninstaller"/>），
	///   内置插件（图片对比 / Hex 对比）不可卸载；
	/// - 「重新加载插件」：重建注册表并重扫描 plugins/ 目录，无需重启 ForkPlus；
	/// - 「打开插件目录」「下载最新插件」：定位插件目录 / 直达插件发布页。
	///
	/// 本页文案固定中文，元数据国际化留待后续版本（插件名称/描述已按宿主语言本地化）。
	/// </summary>
	public partial class PluginsUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		private ForkPlusDialogWindow _parentWindow;

		/// <summary>当前勾选（待卸载）的插件 Id。</summary>
		private readonly HashSet<string> _checkedIds = new HashSet<string>();

		/// <summary>当前列表中可勾选（可卸载）的行复选框。</summary>
		private readonly List<CheckBox> _rowCheckboxes = new List<CheckBox>();

		private int _checkableCount;

		/// <summary>批量修改勾选状态时抑制单行/全选事件回环。</summary>
		private bool _syncingSelection;

		public PluginsUserControl()
		{
			InitializeComponent();
		}

		public void Initialize(ForkPlusDialogWindow parentWindow)
		{
			_parentWindow = parentWindow;
			LoadPlugins();
		}

		public void ApplyLocalization()
		{
			// 本页文案固定中文，元数据国际化留待后续版本；此处仅重建动态行。
			LoadPlugins();
		}

		private void LoadPlugins()
		{
			_syncingSelection = true;
			SelectAllCheckBox.IsChecked = false;
			_syncingSelection = false;
			_checkedIds.Clear();
			_rowCheckboxes.Clear();
			_checkableCount = 0;
			PluginsListPanel.Children.Clear();
			DiffViewPluginInfo[] infos = DiffViewPluginLoader.GetPluginInfos();
			if (infos.Length == 0)
			{
				PluginsListPanel.Children.Add(new TextBlock
				{
					Text = "未发现插件。可点击「安装插件...」安装插件压缩包，或把插件 DLL 放入 plugins/ 目录后点击「重新加载插件」。",
					FontSize = 13,
					Opacity = 0.7,
					Margin = new Thickness(0, 4, 0, 4)
				});
			}
			else
			{
				foreach (DiffViewPluginInfo info in infos)
				{
					if (info.CanUninstall)
					{
						_checkableCount++;
					}
					PluginsListPanel.Children.Add(BuildPluginRow(info));
				}
			}
			SelectAllCheckBox.IsEnabled = _checkableCount > 0;
			UpdateUninstallButton();
		}

		private Control BuildPluginRow(DiffViewPluginInfo info)
		{
			Grid grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

			Control checkbox = BuildUninstallCheckbox(info);
			Grid.SetColumn(checkbox, 0);
			grid.Children.Add(checkbox);

			StackPanel details = new StackPanel();
			StackPanel title = new StackPanel { Orientation = Orientation.Horizontal };
			title.Children.Add(new TextBlock
			{
				Text = info.Name,
				FontSize = 13,
				FontWeight = FontWeight.SemiBold,
				VerticalAlignment = VerticalAlignment.Center
			});
			title.Children.Add(new TextBlock
			{
				Text = "v" + info.Version,
				FontSize = 11,
				Opacity = 0.6,
				Margin = new Thickness(8, 0, 0, 0),
				VerticalAlignment = VerticalAlignment.Center
			});
			if (info.IsBuiltIn)
			{
				title.Children.Add(new TextBlock
				{
					Text = "内置",
					FontSize = 11,
					Opacity = 0.6,
					Margin = new Thickness(8, 0, 0, 0),
					VerticalAlignment = VerticalAlignment.Center
				});
			}
			details.Children.Add(title);

			if (!string.IsNullOrWhiteSpace(info.Description))
			{
				details.Children.Add(new TextBlock
				{
					Text = info.Description,
					FontSize = 12,
					Opacity = 0.75,
					TextWrapping = TextWrapping.Wrap,
					Margin = new Thickness(0, 3, 12, 0)
				});
			}

			string extensions = info.FileExtensions.Count == 0 ? "—" : string.Join(", ", info.FileExtensions);
			details.Children.Add(new TextBlock
			{
				Text = "匹配扩展名：" + extensions + "　优先级：" + info.Priority,
				FontSize = 11,
				Opacity = 0.55,
				Margin = new Thickness(0, 3, 0, 0)
			});

			if (info.Status == DiffViewPluginStatus.Failed)
			{
				details.Children.Add(new TextBlock
				{
					Text = "加载失败：" + info.Error,
					FontSize = 11,
					Foreground = Brushes.OrangeRed,
					TextWrapping = TextWrapping.Wrap,
					Margin = new Thickness(0, 3, 0, 0)
				});
			}
			Grid.SetColumn(details, 1);
			grid.Children.Add(details);

			Control action = BuildPluginAction(info);
			Grid.SetColumn(action, 2);
			grid.Children.Add(action);

			return new Border
			{
				BorderBrush = new SolidColorBrush(Color.Parse("#22000000")),
				BorderThickness = new Thickness(0, 0, 0, 1),
				Padding = new Thickness(0, 8, 0, 8),
				Child = grid
			};
		}

		/// <summary>卸载勾选框：可卸载插件为可用复选框，内置插件为禁用占位，其余（失败条目）留空。</summary>
		private Control BuildUninstallCheckbox(DiffViewPluginInfo info)
		{
			if (info.CanUninstall)
			{
				CheckBox checkbox = new CheckBox
				{
					IsChecked = _checkedIds.Contains(info.Id),
					Tag = info.Id,
					VerticalAlignment = VerticalAlignment.Center,
					Margin = new Thickness(0, 0, 8, 0)
				};
				checkbox.IsCheckedChanged += RowCheckBox_Changed;
				_rowCheckboxes.Add(checkbox);
				return checkbox;
			}
			if (info.IsBuiltIn)
			{
				CheckBox builtIn = new CheckBox
				{
					IsEnabled = false,
					VerticalAlignment = VerticalAlignment.Center,
					Margin = new Thickness(0, 0, 8, 0)
				};
				ToolTip.SetTip(builtIn, "内置插件，暂不支持卸载");
				return builtIn;
			}
			return new Border { Margin = new Thickness(0, 0, 8, 0) };
		}

		private void RowCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			if (_syncingSelection)
			{
				return;
			}
			if (!(sender is CheckBox checkbox) || !(checkbox.Tag is string pluginId))
			{
				return;
			}
			if (checkbox.IsChecked.GetValueOrDefault())
			{
				_checkedIds.Add(pluginId);
			}
			else
			{
				_checkedIds.Remove(pluginId);
			}
			UpdateSelectAllState();
			UpdateUninstallButton();
		}

		private void SelectAllCheckBox_Changed(object sender, RoutedEventArgs e)
		{
			if (_syncingSelection)
			{
				return;
			}
			bool selectAll = SelectAllCheckBox.IsChecked.GetValueOrDefault();
			_syncingSelection = true;
			foreach (CheckBox checkbox in _rowCheckboxes)
			{
				checkbox.IsChecked = selectAll;
			}
			_syncingSelection = false;
			_checkedIds.Clear();
			if (selectAll)
			{
				foreach (CheckBox checkbox in _rowCheckboxes)
				{
					if (checkbox.Tag is string pluginId)
					{
						_checkedIds.Add(pluginId);
					}
				}
			}
			UpdateUninstallButton();
		}

		private void UpdateSelectAllState()
		{
			_syncingSelection = true;
			SelectAllCheckBox.IsChecked = _checkableCount > 0 && _checkedIds.Count >= _checkableCount;
			_syncingSelection = false;
		}

		private void UpdateUninstallButton()
		{
			int count = _checkedIds.Count;
			UninstallButton.IsEnabled = count > 0;
			UninstallButton.Content = count > 0 ? $"卸载选中（{count}）" : "卸载选中";
		}

		private Control BuildPluginAction(DiffViewPluginInfo info)
		{
			if (!info.CanToggle)
			{
				return new TextBlock
				{
					Text = "不可用",
					FontSize = 12,
					Opacity = 0.6,
					VerticalAlignment = VerticalAlignment.Center
				};
			}
			ToggleSwitch toggle = new ToggleSwitch
			{
				IsChecked = info.Status == DiffViewPluginStatus.Enabled,
				Tag = info.Id,
				OnContent = "启用",
				OffContent = "禁用",
				VerticalAlignment = VerticalAlignment.Center
			};
			// 先赋初值再订阅事件，避免装配期触发 handler 造成回写。
			toggle.IsCheckedChanged += PluginToggle_Changed;
			return toggle;
		}

		private void PluginToggle_Changed(object sender, RoutedEventArgs e)
		{
			if (!(sender is ToggleSwitch toggle) || !(toggle.Tag is string pluginId))
			{
				return;
			}
			bool enabled = toggle.IsChecked.GetValueOrDefault();
			// 即时生效并持久化（禁用仅退出路由，插件条目保留）；随后重建列表刷新视觉状态。
			DiffViewPluginLoader.SetPluginEnabled(pluginId, enabled);
			LoadPlugins();
			SetStatus("已" + (enabled ? "启用" : "禁用") + "插件：" + pluginId);
		}

		private void InstallButton_Click(object sender, RoutedEventArgs e)
		{
			Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
			{
				Filter = "插件压缩包 (*.zip)|*.zip|所有文件 (*.*)|*.*",
				Title = "选择插件压缩包"
			};
			if (dialog.ShowDialog() != true)
			{
				return;
			}
			PluginInstallResult result = PluginPackageInstaller.InstallFromZip(dialog.FileName);
			if (!result.Success)
			{
				SetStatus("安装失败：" + result.Error);
				return;
			}
			DiffViewPluginLoader.Reload();
			LoadPlugins();
			string message = "已安装插件：复制 " + result.CopiedFiles + " 个文件";
			if (result.SkippedSharedAssemblies > 0)
			{
				message += "，跳过宿主共享程序集 " + result.SkippedSharedAssemblies + " 个";
			}
			message += "；当前共 " + DiffViewPluginLoader.RegisteredCount + " 个插件。";
			if (result.LockedFiles.Count > 0)
			{
				message += " 有 " + result.LockedFiles.Count + " 个文件被占用未能覆盖，请关闭 ForkPlus 后重试。";
			}
			SetStatus(message);
		}

		private void UninstallButton_Click(object sender, RoutedEventArgs e)
		{
			if (_checkedIds.Count == 0)
			{
				return;
			}
			string[] pluginIds = _checkedIds.ToArray();
			bool? confirmed = new MessageBoxWindow(
				"确认卸载",
				"将卸载 " + pluginIds.Length + " 个插件（删除其 DLL 文件），卸载后如需使用请重新安装。\n\n是否继续？",
				"卸载",
				"取消",
				showCancelButton: true,
				showWarningIcon: true).ShowDialog();
			if (confirmed != true)
			{
				return;
			}
			PluginUninstallResult result = PluginUninstaller.Uninstall(pluginIds);
			DiffViewPluginLoader.Reload();
			LoadPlugins();
			string message = "已卸载 " + result.RemovedIds.Count + " 个插件。";
			if (result.SkippedBuiltInIds.Count > 0)
			{
				message += " 跳过内置插件 " + result.SkippedBuiltInIds.Count + " 个。";
			}
			if (result.NotFoundIds.Count > 0)
			{
				message += " " + result.NotFoundIds.Count + " 个未找到来源文件。";
			}
			if (result.HasPendingRestart)
			{
				message += " 部分文件被占用，将在下次启动完成删除。";
			}
			SetStatus(message);
		}

		private void ReloadButton_Click(object sender, RoutedEventArgs e)
		{
			DiffViewPluginLoader.Reload();
			LoadPlugins();
			int count = DiffViewPluginLoader.RegisteredCount;
			SetStatus("插件已重新加载，共 " + count + " 个注册成功。");
		}

		private void OpenDirectoryButton_Click(object sender, RoutedEventArgs e)
		{
			string directory = DiffViewPluginLoader.EnsurePluginDirectory();
			FileHelper.OpenInWindowsExplorer(directory);
		}

		private void SetStatus(string message)
		{
			StatusTextBlock.Text = message ?? string.Empty;
		}
	}
}
