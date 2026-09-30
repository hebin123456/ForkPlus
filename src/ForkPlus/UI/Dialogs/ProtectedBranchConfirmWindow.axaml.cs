using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup;
using ForkPlus.Settings;
using ForkPlus.UI.UserControls.Preferences;
using ForkPlus.UI.WpfCompat;

namespace ForkPlus.UI.Dialogs
{
	/// <summary>
	/// WS2.4（受保护分支）：四处危险操作（删除本地分支/删除远程分支/重置分支/强推）共用的
	/// 二次确认对话框。命中 RepositorySettings.ProtectedBranches 时由调用方弹出；OnSubmit
	/// 关闭返回 true 放行原流程，取消则中止。匹配规则经静态助手 MatchProtected 提供：
	/// 短名精确匹配（忽略大小写），refs/heads/ 前缀剥掉（设置条目与待检分支名两侧都兼容）。
	/// </summary>
	public partial class ProtectedBranchConfirmWindow : ForkPlusDialogWindow
	{
		private const string RefsHeadsPrefix = "refs/heads/";

		public ProtectedBranchConfirmWindow(string[] branchNames)
		{
			InitializeComponent();
			base.DialogTitle = Translate("Protected branch confirmation");
			base.DialogDescription = Translate("The following branches are protected:");
			base.SubmitButtonTitle = Translate("Continue");
			// 取消按钮走基类默认翻译（Cancel）
			HintTextBlock.Text = Translate("You can manage protected branches in the repository settings.");
			BranchesItemsControl.ItemsSource = (branchNames ?? Array.Empty<string>()).ToArray();
		}

		protected override void OnSubmit()
		{
			CloseWithOk();
		}

		/// <summary>剥掉 refs/heads/ 前缀并去除首尾空白，得到用于匹配的分支短名。</summary>
		public static string Normalize(string branchName)
		{
			if (string.IsNullOrEmpty(branchName))
			{
				return branchName;
			}
			string trimmed = branchName.Trim();
			if (trimmed.StartsWith(RefsHeadsPrefix, StringComparison.OrdinalIgnoreCase))
			{
				trimmed = trimmed.Substring(RefsHeadsPrefix.Length);
			}
			return trimmed;
		}

		/// <summary>返回 branchNames 中命中受保护列表的短名（取设置条目的规范化形式，去重、保持输入顺序）。空列表/空输入返回空数组。</summary>
		public static string[] MatchProtected(string[] protectedBranches, IEnumerable<string> branchNames)
		{
			if (protectedBranches == null || protectedBranches.Length == 0 || branchNames == null)
			{
				return Array.Empty<string>();
			}
			Dictionary<string, string> protectedMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
			foreach (string entry in protectedBranches)
			{
				string normalizedEntry = Normalize(entry);
				if (!string.IsNullOrEmpty(normalizedEntry))
				{
					_ = protectedMap.TryAdd(normalizedEntry, normalizedEntry);
				}
			}
			if (protectedMap.Count == 0)
			{
				return Array.Empty<string>();
			}
			List<string> hits = new List<string>();
			HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (string branchName in branchNames)
			{
				string normalized = Normalize(branchName);
				if (string.IsNullOrEmpty(normalized) || !protectedMap.TryGetValue(normalized, out string canonical) || !seen.Add(canonical))
				{
					continue;
				}
				hits.Add(canonical);
			}
			return hits.ToArray();
		}

		/// <summary>便捷入口：无命中返回 true（直接放行）；命中弹确认框，返回用户是否选择继续。
		/// owner 供模态居中（可空）。</summary>
		public static bool Confirm([Null] Window owner, string[] protectedBranches, IEnumerable<string> branchNames)
		{
			string[] hits = MatchProtected(protectedBranches, branchNames);
			if (hits.Length == 0)
			{
				return true;
			}
			ProtectedBranchConfirmWindow window = new ProtectedBranchConfirmWindow(hits);
			if (owner != null)
			{
				window.SetOwnerCompat(owner);
			}
			return window.ShowDialog().GetValueOrDefault();
		}

		private static string Translate(string text)
		{
			return PreferencesLocalization.Translate(text, ForkPlusSettings.Default.UiLanguage);
		}
	}
}
