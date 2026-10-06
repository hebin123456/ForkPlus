using System.Collections.Generic;
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
	/// 偏好设置 &gt; 插件（Plugins）：对比视图插件管理页（v5.0.1）。
	///
	/// 契约：插件由 <see cref="DiffViewPluginLoader"/> 在启动期从可执行文件旁的 plugins/
	/// 目录动态加载；本页只做展示与状态切换，不参与插件实例化。
	///
	/// 能力：
	/// - 列出已注册插件（按优先级）与加载失败的 DLL，展示名称、版本号、描述、匹配扩展名与优先级；
	/// - 启用/禁用开关：即时写入注册表并持久化（<see cref="DiffViewPluginLoader.SetPluginEnabled"/>），
	///   禁用仅退出路由，条目保留、可随时恢复；
	/// - 「重新加载插件」：重建注册表并重扫描 plugins/ 目录，新增/删除插件与启禁用立即生效，
	///   无需重启 ForkPlus（用户绑定与禁用状态跨重载保持）；
	/// - 「打开插件目录」：定位到 plugins/ 目录便于投放第三方插件；
	/// - v5.0.2「下载最新插件」超链接：直达 ForkPlus-Plugins 的 Releases 页面
	///   （https://github.com/hebin123456/ForkPlus-Plugins/releases）获取最新插件。
	///
	/// v5.0.1 文案固定中文，元数据国际化留待后续版本。
	/// </summary>
	public partial class PluginsUserControl : UserControl, ForkPlus.UI.ILocalizableControl
	{
		private ForkPlusDialogWindow _parentWindow;

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
			// v5.0.1 文案固定中文，元数据国际化留待后续版本；此处仅重建动态行。
			LoadPlugins();
		}

		private void LoadPlugins()
		{
			PluginsListPanel.Children.Clear();
			DiffViewPluginInfo[] infos = DiffViewPluginLoader.GetPluginInfos();
			if (infos.Length == 0)
			{
				PluginsListPanel.Children.Add(new TextBlock
				{
					Text = "未发现插件。请把插件 DLL 放入 plugins/ 目录后点击「重新加载插件」。",
					FontSize = 13,
					Opacity = 0.7,
					Margin = new Thickness(0, 4, 0, 4)
				});
				return;
			}
			foreach (DiffViewPluginInfo info in infos)
			{
				PluginsListPanel.Children.Add(BuildPluginRow(info));
			}
		}

		private Control BuildPluginRow(DiffViewPluginInfo info)
		{
			Grid grid = new Grid();
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
			grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

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
			Grid.SetColumn(details, 0);
			grid.Children.Add(details);

			Control action = BuildPluginAction(info);
			Grid.SetColumn(action, 1);
			grid.Children.Add(action);

			return new Border
			{
				BorderBrush = new SolidColorBrush(Color.Parse("#22000000")),
				BorderThickness = new Thickness(0, 0, 0, 1),
				Padding = new Thickness(0, 8, 0, 8),
				Child = grid
			};
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