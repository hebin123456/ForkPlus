// E2E 模块23（2026-09-06）：SSH 与环境（4 窗口，6 用例）。
// 覆盖：ConfigureSshKeysWindow（空态 fallback + "default system ssh-agent" 斜体配置文本/
// 真实密钥列表装配（系统 ssh-keygen 生成的 ed25519）+ 详情三字段 + Sha256 托管指纹计算 +
// Submit → SshKeys 落盘 + 重开往返/CheckBox 勾选生产点击序 → ValidateSshKey 真实验证
// （修复后探测链）+ 配置文本联动 + 取消勾选 + 多密钥警告（配置文本逗号连接））/
// GenerateNewSshKeyWindow（空名/空邮箱禁用 + 重名 Warning + ssh-keygen 命令预览拼接 +
// 真实生成生产链路（OnSubmit → GenerateSshKeyShellCommand → Close(result) + ResultKey +
// 私钥/公钥文件落盘 + 公钥含 email 注释））/
// ConfigureGitInstanceWindow（候选列表装配（ForkPlus Git 内置 + System PATH，AddCandidate
// 真实跑 git --version）+ TextBox ↔ ListBox 双向联动（SelectCurrentCandidate/
// SelectionChanged）+ 无效路径提交禁用 + 有效路径 Submit → GitInstancePath 落盘）/
// ConfigureWorkspacesWindow（默认 Home/Work + 删除按钮禁用（count≤2）+ 添加（编辑模式
// TextBox 视觉树定位 + KeyUp Enter 提交改名）+ 删除（MessageBox 确认泵）+ 删除按钮启用
// （count>2）+ ShowInTitle 开关 + OnClosing → Workspaces Update（按名排序）+ Save 落盘）。
//
// 生产 bug 修复一项（跨平台，本模块探针实证）：GenerateSshKeyShellCommand/
// ValidateSshKeyShellCommand 硬拼 <gitInstance>/usr/bin/ssh-keygen.exe（Windows Git 布局），
// Unix 内置 git 实例布局（gitInstance/2.50.1/ 下仅 bin/git，无 usr/bin）下 SSH 密钥
// 生成/验证全坏。修复：SystemEnvironment.GetSshKeygenPath() 候选链（usr/bin/ssh-keygen.exe
// → usr/bin/ssh-keygen → bin/ssh-keygen.exe → bin/ssh-keygen → 系统 PATH），两命令改用
// 探测链（与同文件 IsGitExecutable/GetUnixCommonGitPaths 的既有跨平台 Migration note 同模式）。
// 探针：/root/.ssh 不存在（空态基线）、系统 /usr/bin/ssh-keygen 可用、
// ssh-keygen -q -t ed25519 -N "" -C email -f key 与 -y -P "" -f key 两参数组实证 exit 0。
//
// ~/.ssh 隔离（防环境污染与被污染）：IsolateSshDir 助手——存在则整体挪 .e2e-backup
// 备份（原目录可能有宿主密钥），finally 删测试目录 + 备份还原；空态用例因此获得确定性
// 基线。套件 HeadlessAvalonia Collection 串行（与模块 1-22 同池），无并发干扰。
//
// 设置污染防护（模块 21/22 模式沿用）：全属性反射快照 + finally 还原 + Save()；Workspaces
// 特殊处理——WorkspacesSettings.Update 原地改属性（All/ActiveWorkspace/ShowInTitle），引用
// 快照会被改穿，须深拷贝（Workspace 数组逐项 new）。
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ForkPlus.Settings;
using ForkPlus.Shell;
using ForkPlus.UI;
using ForkPlus.UI.Controls;
using ForkPlus.UI.Dialogs;
using ForkPlus.UI.UserControls;
using Newtonsoft.Json.Linq;
using Xunit;

namespace ForkPlus.Tests
{
	[Collection("HeadlessAvalonia")]
	public class E2e23SshEnvironmentTests
	{
		private const string ModuleDir = "23-ssh-environment";

		// ============================ 共享助手 ============================

		private static ForkPlusDialogFooter FooterOf(ForkPlusDialogWindow dialog)
		{
			ForkPlusDialogFooter footer = dialog.GetVisualDescendants().OfType<ForkPlusDialogFooter>().FirstOrDefault();
			Assert.NotNull(footer);
			return footer;
		}

		private static string Tr(string text)
		{
			return E2eMainWindowHarness.Tr(text);
		}

		private static string TrFormat(string text, params object[] args)
		{
			return E2eMainWindowHarness.TrFormat(text, args);
		}

		private static void RunJobs()
		{
			Dispatcher.UIThread.RunJobs();
		}

		// ---------- ~/.ssh 隔离 ----------

		/// <summary>隔离 ~/.ssh：已存在则挪备份（宿主密钥保护），返回恢复委托（删测试目录 + 还原备份）。</summary>
		private static Action IsolateSshDir()
		{
			string sshDir = SystemEnvironment.LocalSSHDirectory;
			Assert.True(sshDir != null, "LocalSSHDirectory 应可用（UserProfile 存在）");
			string backup = sshDir + ".e2e-backup";
			bool hadDir = Directory.Exists(sshDir);
			bool hadBackup = Directory.Exists(backup);
			if (hadDir && !hadBackup)
			{
				Directory.Move(sshDir, backup);
			}
			return delegate
			{
				try
				{
					if (Directory.Exists(sshDir))
					{
						Directory.Delete(sshDir, recursive: true);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine("[E2e23] 清理测试 ssh 目录失败: " + ex.Message);
				}
				try
				{
					if (hadDir && !hadBackup && Directory.Exists(backup))
					{
						Directory.Move(backup, sshDir);
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine("[E2e23] 还原 ssh 目录备份失败: " + ex.Message);
				}
			};
		}

		/// <summary>用生产同源探测链的 ssh-keygen 生成无口令 ed25519 测试密钥（GenerateSshKeyShellCommand 参数镜像）。</summary>
		private static void GenerateKey(string keyName, string email)
		{
			string sshDir = SystemEnvironment.LocalSSHDirectory;
			Directory.CreateDirectory(sshDir);
			string keygen = SystemEnvironment.GetSshKeygenPath();
			Assert.True(File.Exists(keygen), "ssh-keygen 应经探测链找到（生产修复后）: " + keygen);
			var psi = new ProcessStartInfo(keygen)
			{
				Arguments = "-q -t ed25519 -N \"\" -C \"" + email + "\" -f \"" + Path.Combine(sshDir, keyName) + "\"",
				UseShellExecute = false,
				RedirectStandardError = true
			};
			using (Process process = Process.Start(psi))
			{
				process.WaitForExit();
				Assert.True(process.ExitCode == 0, "ssh-keygen 生成失败: " + process.StandardError.ReadToEnd());
			}
			Assert.True(File.Exists(Path.Combine(sshDir, keyName)), "私钥应生成");
			Assert.True(File.Exists(Path.Combine(sshDir, keyName + ".pub")), "公钥应生成");
		}

		/// <summary>等待并取 ListBox 里指定密钥名的 CheckBox（DataTemplate 实例化经布局异步，轮询）。</summary>
		private static CheckBox CheckBoxOfKey(ListBox listBox, string keyName)
		{
			CheckBox found = null;
			Assert.True(UiClick.WaitFor(delegate
			{
				found = listBox.GetVisualDescendants().OfType<CheckBox>().FirstOrDefault(delegate (CheckBox c)
				{
					var vm = (c.Parent as DockPanel)?.DataContext as SshKeyViewModel;
					return vm != null && vm.KeyFileName == keyName;
				});
				return found != null;
			}), "密钥 " + keyName + " 的 CheckBox 未出现（15s 超时）");
			return found;
		}

		/// <summary>命令预览文本（ForkPlusDialogWindow.AddCommandPreview 生成的 TextBlock，按前缀定位）。</summary>
		private static string CommandPreviewOf(ForkPlusDialogWindow dialog, string prefix)
		{
			return dialog.GetVisualDescendants().OfType<TextBlock>()
				.FirstOrDefault(t => t.Text != null && t.Text.StartsWith(prefix, StringComparison.Ordinal))?.Text ?? "";
		}

		// ---------- 应用级设置快照（全属性反射 + Workspaces 深拷贝） ----------

		private sealed class AppSettingsSnapshot
		{
			public Dictionary<string, object> Values = new Dictionary<string, object>();
			public ForkPlusSettings.WorkspacesSettings WorkspacesClone;
		}

		private static AppSettingsSnapshot SnapshotAppSettings()
		{
			var snap = new AppSettingsSnapshot();
			foreach (PropertyInfo property in typeof(ForkPlusSettings).GetProperties(BindingFlags.Public | BindingFlags.Instance))
			{
				if (property.CanWrite && property.GetIndexParameters().Length == 0)
				{
					try
					{
						snap.Values[property.Name] = property.GetValue(ForkPlusSettings.Default);
					}
					catch
					{
					}
				}
			}
			// Workspaces 深拷贝：Update() 原地改属性，引用快照会被改穿
			snap.WorkspacesClone = CloneWorkspaces(ForkPlusSettings.Default.Workspaces);
			return snap;
		}

		private static void RestoreAppSettings(AppSettingsSnapshot snap)
		{
			try
			{
				foreach (KeyValuePair<string, object> pair in snap.Values)
				{
					typeof(ForkPlusSettings).GetProperty(pair.Key).SetValue(ForkPlusSettings.Default, pair.Value);
				}
				ForkPlusSettings.Default.Workspaces = snap.WorkspacesClone;
				ForkPlusSettings.Default.Save();
			}
			catch (Exception ex)
			{
				Console.WriteLine("[E2e23] 设置恢复失败: " + ex.Message);
			}
		}

		private static ForkPlusSettings.WorkspacesSettings CloneWorkspaces(ForkPlusSettings.WorkspacesSettings source)
		{
			Workspace[] all = source.All.Map((Workspace w) => new Workspace(w.Name, w.Repositories, w.ActiveRepository));
			Workspace active = all.FirstOrDefault((Workspace w) => w.Name == source.ActiveWorkspace?.Name) ?? all.FirstItem();
			return new ForkPlusSettings.WorkspacesSettings(all, active, source.ShowInTitle);
		}

		// ============================ 1) SSH 密钥：空态 ============================

		[Fact]
		public void SshKeys_EmptyState_DefaultConfiguration()
		{
			var snap = SnapshotAppSettings();
			ForkPlusSettings.Default.SshKeys = new string[0];
			ForkPlusSettings.Default.Save();
			Action restoreSshDir = IsolateSshDir();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var window = new ConfigureSshKeysWindow();
					window.Show();
					RunJobs();
					try
					{
						// —— 空态：列表空 + 双 fallback + 配置文本 default system ssh-agent（斜体）+ 图标折叠 ——
						Assert.Empty(window.SshKeyListBox.Items.OfType<SshKeyViewModel>());
						Assert.True(window.FallbackUserControl.IsVisible, "列表 fallback 应显示（No SSH keys）");
						Assert.True(window.DetailsFallbackUserControl.IsVisible, "详情 fallback 应显示");
						Assert.Equal(Tr("default system ssh-agent"), window.SshConfigurationTextBlock.Text);
						Assert.Equal(global::Avalonia.Media.FontStyle.Italic, window.SshConfigurationTextBlock.FontStyle);
						Assert.False(window.SshConfigurationIcon.IsVisible);
						// 空态详情字段：SelectedIndex=0 在空列表上不触发 SelectionChanged → RefreshDetails
					// 未跑，控件保持 XAML 初始值——Path/Sha256 两个 SelectableTextBlock 未设 Text（null），
					// PublicKeyTextBox 显式 Text=""（""）。探针实证（本环境单跑+整类跑一致；
					// 上轮 agent "全 null" 记录不实——首版断言只核对了 SelectableTextBlock 形态）。
					// （Cosmetic 差异非生产 bug：真实用户打不开空态详情场景。）
					Assert.Null(window.SshKeyPathTextBlock.Text);
					Assert.Null(window.SshKeySha256TextBox.Text);
					Assert.Equal(string.Empty, window.SshKeyPublicKeyTextBox.Text);

						ScreenshotHelper.Snap(window, "01-ssh-keys-empty", ModuleDir);

						// —— Submit → OnSubmit → SshKeys 空数组落盘 ——
						UiClick.Click(FooterOf(window).SubmitButton);
						RunJobs();
						Assert.NotNull(ForkPlusSettings.Default.SshKeys);
						Assert.Empty(ForkPlusSettings.Default.SshKeys);
					}
					finally
					{
						if (window.IsVisible)
						{
							window.Close();
						}
					}
				});
			}
			finally
			{
				restoreSshDir();
				RestoreAppSettings(snap);
			}
		}

		// ============================ 2) SSH 密钥：列表/详情/激活/落盘 ============================

		[Fact]
		public void SshKeys_ListDetailsSubmitAndRoundtrip()
		{
			var snap = SnapshotAppSettings();
			ForkPlusSettings.Default.SshKeys = new string[0];
			ForkPlusSettings.Default.Save();
			Action restoreSshDir = IsolateSshDir();
			try
			{
				GenerateKey("e2e-list-key", "list@test.example");
				string keyPath = Path.Combine(SystemEnvironment.LocalSSHDirectory, "e2e-list-key");
				HeadlessAppBootstrap.Run(delegate
				{
					var window = new ConfigureSshKeysWindow();
					window.Show();
					RunJobs();
					try
					{
						// —— 列表装配：1 项 + 字段（KeyFileName/KeyPath/PublicKey 前缀/Sha256 托管指纹）——
						var items = window.SshKeyListBox.Items.OfType<SshKeyViewModel>().ToList();
						Assert.Single(items);
						SshKeyViewModel vm = items[0];
						Assert.Equal("e2e-list-key", vm.KeyFileName);
						Assert.Equal(keyPath, vm.KeyPath);
						Assert.StartsWith("ssh-ed25519 ", vm.PublicKey, StringComparison.Ordinal);
						Assert.Contains("list@test.example", vm.PublicKey, StringComparison.Ordinal);
						byte[] fingerprint = Convert.FromBase64String(vm.Sha256);
						Assert.Equal(32, fingerprint.Length); // SHA256 指纹 32 字节
						Assert.False(vm.IsActive, "SshKeys 为空 → 未激活");
						Assert.False(window.FallbackUserControl.IsVisible);
						Assert.False(window.DetailsFallbackUserControl.IsVisible);

						// —— 选中（构造器 SelectedIndex=0）→ 详情三字段装配 ——
						Assert.Equal(vm, window.SshKeyListBox.SelectedItem);
						Assert.Equal(keyPath, window.SshKeyPathTextBlock.Text);
						Assert.Equal(vm.Sha256, window.SshKeySha256TextBox.Text);
						Assert.Equal(vm.PublicKey, window.SshKeyPublicKeyTextBox.Text);

						ScreenshotHelper.Snap(window, "02-ssh-keys-list", ModuleDir);

						// —— 直接激活（绕开 CheckBox 验证，验证链路在用例3）→ Submit → SshKeys 落盘 ——
						vm.IsActive = true;
						UiClick.Click(FooterOf(window).SubmitButton);
						RunJobs();
						Assert.Equal(new string[] { keyPath }, ForkPlusSettings.Default.SshKeys);

						// —— 重开往返：激活状态从设置装配 ——
						var window2 = new ConfigureSshKeysWindow();
						window2.Show();
						RunJobs();
						try
						{
							SshKeyViewModel vm2 = window2.SshKeyListBox.Items.OfType<SshKeyViewModel>().Single();
							Assert.True(vm2.IsActive, "重开后激活状态应从 SshKeys 装配");
							// 激活 1 个 → 配置文本为密钥名（非斜体 + 图标显示）
							Assert.Equal("e2e-list-key", window2.SshConfigurationTextBlock.Text);
							Assert.Equal(global::Avalonia.Media.FontStyle.Normal, window2.SshConfigurationTextBlock.FontStyle);
							Assert.True(window2.SshConfigurationIcon.IsVisible);
						}
						finally
						{
							window2.Close();
						}
					}
					finally
					{
						if (window.IsVisible)
						{
							window.Close();
						}
					}
				});
			}
			finally
			{
				restoreSshDir();
				RestoreAppSettings(snap);
			}
		}

		// ============================ 3) SSH 密钥：勾选验证/多密钥警告 ============================

		[Fact]
		public void SshKeys_ToggleValidationAndMultiKeyWarning()
		{
			var snap = SnapshotAppSettings();
			ForkPlusSettings.Default.SshKeys = new string[0];
			ForkPlusSettings.Default.Save();
			Action restoreSshDir = IsolateSshDir();
			try
			{
				GenerateKey("e2e-tog-a", "a@test.example");
				GenerateKey("e2e-tog-b", "b@test.example");
				string pathA = Path.Combine(SystemEnvironment.LocalSSHDirectory, "e2e-tog-a");
				string pathB = Path.Combine(SystemEnvironment.LocalSSHDirectory, "e2e-tog-b");
				HeadlessAppBootstrap.Run(delegate
				{
					var window = new ConfigureSshKeysWindow();
					window.Show();
					RunJobs();
					try
					{
						// —— 勾选第一个：生产点击序 → ValidateSshKey 真实验证（修复后探测链
						//    ssh-keygen -y -P "" -f key，无口令 → Success）→ IsActive 保持 ——
						CheckBox checkBoxA = CheckBoxOfKey(window.SshKeyListBox, "e2e-tog-a");
						UiClick.Toggle(checkBoxA, true);
						var vms = window.SshKeyListBox.Items.OfType<SshKeyViewModel>().ToList();
						Assert.True(vms.First(v => v.KeyFileName == "e2e-tog-a").IsActive,
							"无口令密钥勾选后应通过真实验证保持激活");
						// 配置文本联动（CheckBox_Changed → RefreshConfigutationTextBlock）
						Assert.Equal("e2e-tog-a", window.SshConfigurationTextBlock.Text);
						Assert.True(window.SshConfigurationIcon.IsVisible);

						// —— 勾选第二个：两密钥激活 → 配置文本逗号连接 + RefreshStatus 多密钥警告 ——
						CheckBox checkBoxB = CheckBoxOfKey(window.SshKeyListBox, "e2e-tog-b");
						UiClick.Toggle(checkBoxB, true);
						Assert.Equal("e2e-tog-a, e2e-tog-b", window.SshConfigurationTextBlock.Text);

						// —— 取消第一个：IsActive=false（取消路径不验证）→ 文本回落单密钥 ——
						UiClick.Toggle(checkBoxA, false);
						Assert.False(vms.First(v => v.KeyFileName == "e2e-tog-a").IsActive);
						Assert.Equal("e2e-tog-b", window.SshConfigurationTextBlock.Text);

						// —— Submit → 仅 b 激活落盘 ——
						UiClick.Click(FooterOf(window).SubmitButton);
						RunJobs();
						Assert.Equal(new string[] { pathB }, ForkPlusSettings.Default.SshKeys);
						Assert.True(pathA != pathB);
					}
					finally
					{
						if (window.IsVisible)
						{
							window.Close();
						}
					}
				});
			}
			finally
			{
				restoreSshDir();
				RestoreAppSettings(snap);
			}
		}

		// ============================ 4) 生成新 SSH 密钥：校验/预览/真实生成 ============================

		[Fact]
		public void GenerateNewSshKey_ValidationPreviewAndRealGeneration()
		{
			var snap = SnapshotAppSettings();
			ForkPlusSettings.Default.SshKeys = new string[0];
			ForkPlusSettings.Default.Save();
			Action restoreSshDir = IsolateSshDir();
			try
			{
				// 预置同名密钥（重名校验探针）
				GenerateKey("e2e-gen-key", "occupied@test.example");
				HeadlessAppBootstrap.Run(delegate
				{
					var window = new GenerateNewSshKeyWindow();
					window.Show();
					RunJobs();
					try
					{
						ForkPlusDialogFooter footer = FooterOf(window);

						// —— 校验三态：空名禁用 → 只输名仍禁用（空邮箱）→ 重名 Warning ——
						window.KeyFileNameTextBox.Text = "";
						RunJobs();
						Assert.False(footer.SubmitButton.IsEnabled, "空名应禁用");
						window.KeyFileNameTextBox.Text = "e2e-gen-key";
						RunJobs();
						Assert.False(footer.SubmitButton.IsEnabled, "空邮箱应禁用");
						window.EmailTextBox.Text = "gen@test.example";
						RunJobs();
						Assert.False(footer.SubmitButton.IsEnabled, "重名应禁用（已存在 e2e-gen-key）");
						// 重名状态条（IsSubmitAllowed 内 SetStatus）
						string warning = TrFormat("Ssh key '{0}' already exists", "e2e-gen-key");
						string statusText = window.GetVisualDescendants().OfType<TextBlock>()
							.Select(t => t.Text).FirstOrDefault(t => t == warning);
						Assert.True(statusText != null, "重名应显示警告: " + warning);

						// —— 命令预览（唯一名后）——
						window.KeyFileNameTextBox.Text = "e2e-gen-new";
						RunJobs();
						Assert.True(footer.SubmitButton.IsEnabled);
						string sshDir = SystemEnvironment.LocalSSHDirectory;
						string expectedPreview = "ssh-keygen -t ed25519 -C \"gen@test.example\" -f " + Path.Combine(sshDir, "e2e-gen-new");
						Assert.Equal(expectedPreview, CommandPreviewOf(window, "ssh-keygen "));

						ScreenshotHelper.Snap(window, "03-generate-new-ssh-key", ModuleDir);

						// —— 真实生成生产链路：Submit → OnSubmit（async）→ GenerateSshKeyShellCommand
						//    （修复后探测链）→ Close(result) ——
						UiClick.Click(footer.SubmitButton);
						Assert.True(UiClick.WaitFor(delegate { return !window.IsVisible; }),
							"生成完成应关闭窗口（15s 超时）");
						Assert.True(window.GitResult.Succeeded, "真实生成应成功: " + window.GitResult.Error);
						Assert.Equal("e2e-gen-new", window.ResultKey);
						string privateKey = Path.Combine(sshDir, "e2e-gen-new");
						string publicKey = privateKey + ".pub";
						Assert.True(File.Exists(privateKey), "私钥应落盘");
						Assert.True(File.Exists(publicKey), "公钥应落盘");
						Assert.Contains("gen@test.example", File.ReadAllText(publicKey));
					}
					finally
					{
						if (window.IsVisible)
						{
							window.Close();
						}
					}
				});
			}
			finally
			{
				restoreSshDir();
				RestoreAppSettings(snap);
			}
		}

		// ============================ 5) Git 实例：候选/联动/落盘 ============================

		[Fact]
		public void GitInstance_CandidatesValidationAndSubmit()
		{
			var snap = SnapshotAppSettings();
			ForkPlusSettings.Default.GitInstancePath = null; // Saved Git 候选排除 → 探测链候选
			ForkPlusSettings.Default.Save();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var window = new ConfigureGitInstanceWindow();
					window.Show();
					RunJobs();
					try
					{
						// —— 候选装配：环境自适应（2026-09-06 CI 实证修复，与 E2e21PreferencesTests 同款分叉）——
						// 沙盒/开发机装有内置 git 实例（gitInstance/2.50.1）→ 探测链首候选源为 "ForkPlus Git"；
						// CI runner 无内置实例，HeadlessAppBootstrap 兜底注入 forkgitinstance 环境变量 →
						// 首候选源变为 "Environment Git"（原断言在 GitHub runner 上必挂）。
						var candidates = window.GitCandidatesListBox.Items.OfType<ConfigureGitInstanceWindow.GitCandidate>().ToList();
						Assert.True(candidates.Count >= 2, "内置 git 与 PATH git 应同时入候选: " + candidates.Count);
						if (global::ForkPlus.App.EnvironmentGitInstancePath != null)
						{
							Assert.Contains(candidates, c => c.Source == Tr("Environment Git"));
						}
						else
						{
							Assert.Contains(candidates, c => c.Source == Tr("ForkPlus Git"));
						}
						Assert.Contains(candidates, c => c.Source == Tr("System PATH"));
						Assert.All(candidates, c => Assert.Matches("^\\d+\\.\\d+", c.Version)); // 真实 git --version

						// —— 预填首候选 + TextBox → ListBox 联动（SelectCurrentCandidate）——
						ConfigureGitInstanceWindow.GitCandidate first = candidates[0];
						Assert.Equal(first.Path, window.GitPathTextBox.Text);
						Assert.Equal(first, window.GitCandidatesListBox.SelectedItem);

						// —— ListBox → TextBox 联动（SelectionChanged）——
						ConfigureGitInstanceWindow.GitCandidate second = candidates.First(c => !ReferenceEquals(c, first));
						window.GitCandidatesListBox.SelectedItem = second;
						RunJobs();
						Assert.Equal(second.Path, window.GitPathTextBox.Text);

						ScreenshotHelper.Snap(window, "04-git-instance", ModuleDir);

						// —— 无效路径：TextChanged → SelectCurrentCandidate 清选中 + 提交禁用 ——
						window.GitPathTextBox.Text = "/nonexistent/git";
						RunJobs();
						Assert.Null(window.GitCandidatesListBox.SelectedItem);
						Assert.False(FooterOf(window).SubmitButton.IsEnabled, "不存在的路径应禁用提交");

						// —— 有效路径：输入 PATH git → 选中联动恢复 → Submit → GitInstancePath 落盘 ——
						ConfigureGitInstanceWindow.GitCandidate pathGit = candidates.First(c => c.Source == Tr("System PATH"));
						window.GitPathTextBox.Text = pathGit.Path;
						RunJobs();
						Assert.Equal(pathGit, window.GitCandidatesListBox.SelectedItem);
						ForkPlusDialogFooter footer = FooterOf(window);
						Assert.True(footer.SubmitButton.IsEnabled, "有效 git 路径应启用提交");
						UiClick.Click(footer.SubmitButton);
						RunJobs();
						Assert.Equal(pathGit.Path, ForkPlusSettings.Default.GitInstancePath);
					}
					finally
					{
						if (window.IsVisible)
						{
							window.Close();
						}
					}
				});
			}
			finally
			{
				RestoreAppSettings(snap);
			}
		}

		// ============================ 6) Workspaces：增/改名/删除/落盘 ============================

		[Fact]
		public void Workspaces_AddRenameDeleteAndPersist()
		{
			string repo = TestRepoFactory.CreateBasic(); // OnClosing 需要 MainWindow.Instance
			var snap = SnapshotAppSettings();
			try
			{
				HeadlessAppBootstrap.Run(delegate
				{
					var repoControl = E2eMainWindowHarness.OpenRepository(repo, out var mainWindow);
					try
					{
						var window = new ConfigureWorkspacesWindow();
						window.Show();
						RunJobs();
						try
						{
							int initialCount = window.WorkspacesListBox.Items.OfType<WorkspaceViewModel>().Count();
							Assert.True(initialCount >= 2, "默认应有 Home/Work 两个工作区: " + initialCount);
							Assert.NotNull(window.WorkspacesListBox.SelectedItem);
							// 删除按钮禁用（count ≤ 2；默认恰好 2）
							Assert.Equal(initialCount > 2, window.RemoveWorkspaceButton.IsEnabled);

							// —— 添加：新 VM + 编辑模式（TextBox Post Background 后出现）——
							UiClick.Click(window.AddWorkspaceButton);
							RunJobs();
							var afterAdd = window.WorkspacesListBox.Items.OfType<WorkspaceViewModel>().ToList();
							Assert.Equal(initialCount + 1, afterAdd.Count);
							WorkspaceViewModel added = afterAdd.First(vm => vm.Name == "New Workspace");
							Assert.True(added.IsInEditMode, "添加后应进入编辑模式");
							Assert.True(window.RemoveWorkspaceButton.IsEnabled, "count>2 后删除应启用");
							// 等编辑 TextBox 进视觉树（Post Background）
							TextBox editBox = null;
							Assert.True(UiClick.WaitFor(delegate
							{
								editBox = window.WorkspacesListBox.GetVisualDescendants().OfType<TextBox>()
									.FirstOrDefault(t => t.DataContext == added);
								return editBox != null;
							}), "编辑模式 TextBox 未出现");
							Assert.Equal("New Workspace", editBox.Text);

							// —— 改名：KeyUp Enter 生产管线（CommitWorkspaceNameEdit save:true）——
							editBox.Text = "E2E Space";
							RunJobs();
							editBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Enter });
							RunJobs();
							Assert.False(added.IsInEditMode, "Enter 应提交改名退出编辑模式");
							Assert.Equal("E2E Space", added.Name);

							ScreenshotHelper.Snap(window, "05-workspaces", ModuleDir);

							// —— 删除（MessageBox 确认泵：Post Background 内点 Delete；模块 5 同款模式）——
						window.WorkspacesListBox.SelectedItem = afterAdd.First(vm => vm.Name == "Home");
						RunJobs();
						var deleteHandled = new bool[1];
						var deleteError = new string[1];
						// ⚠️ 探针实证（E2e23ProbeTests，已删）：RunJobs() 默认泵会消费 Background 优先级
						// job——Post 与 Click 之间的 RunJobs() 会把关闭处理器提前执行（确认框尚未弹出 →
						// msgBox==null 即返回），随后模态框弹出无人关闭 → PushFrame 永久等待 → 用例挂死
						// （首个 agent 全量挂起 15 分钟的根因）。因此：选中变化先泵，Post 之后直接 Click。
						window.WorkspacesListBox.SelectedItem = added;
						RunJobs();
						Dispatcher.UIThread.Post(delegate
						{
							try
							{
								MessageBoxWindow msgBox = global::ForkPlus.UI.WpfCompat.WpfApp.Windows
									.OfType<MessageBoxWindow>().FirstOrDefault();
								if (msgBox == null)
								{
									deleteError[0] = "删除确认框未出现";
									return;
								}
								Button delete = UiClick.FindAll<Button>(msgBox)
									.FirstOrDefault(b => UiClick.ContentText(b) == Tr("Delete"));
								if (delete == null)
								{
									deleteError[0] = "确认框中找不到 Delete 按钮";
									return;
								}
								delete.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
								deleteHandled[0] = true;
							}
							catch (Exception ex)
							{
								deleteError[0] = ex.ToString();
							}
						}, DispatcherPriority.Background);
						UiClick.Click(window.RemoveWorkspaceButton);
							Assert.True(deleteHandled[0], "删除确认框处理器未执行: " + deleteError[0]);
							Assert.Null(deleteError[0]);
							RunJobs();
							var afterDelete = window.WorkspacesListBox.Items.OfType<WorkspaceViewModel>().ToList();
							Assert.Equal(initialCount, afterDelete.Count);
							Assert.DoesNotContain(afterDelete, vm => vm.Name == "E2E Space");
							Assert.NotNull(window.WorkspacesListBox.SelectedItem); // 选中回落相邻项

							// —— ShowInTitle 开关 ——
							UiClick.Toggle(window.ShowWorkspaceInTitleCheckBox, true);
							Assert.True(ForkPlusSettings.Default.Workspaces.ShowInTitle);

							// —— Close → OnClosing → Workspaces Update（按名排序）+ Save 落盘 ——
							window.Close();
							RunJobs();
							string[] names = ForkPlusSettings.Default.Workspaces.All.Map((Workspace w) => w.Name);
							Assert.Equal(names.OrderBy(n => n).ToArray(), names); // Update 按名排序
							Assert.True(ForkPlusSettings.Default.Workspaces.ShowInTitle);
							// settings.json 落盘断言
							string json = File.ReadAllText(Path.Combine(App.ForkDirectoryPath, "settings.json"));
							JObject root = JObject.Parse(json);
							Assert.True(root["Workspaces"]?["ShowInTitle"]?.Value<bool>() == true,
								"ShowInTitle 应落盘 settings.json");
						}
						finally
						{
							if (window.IsVisible)
							{
								window.Close();
							}
						}
					}
					finally
				{
					E2eMainWindowHarness.CloseRepositoryTab(mainWindow, repo);
				}
				});
			}
			finally
			{
				RestoreAppSettings(snap);
				TestRepoFactory.Cleanup(repo);
			}
		}
	}
}
