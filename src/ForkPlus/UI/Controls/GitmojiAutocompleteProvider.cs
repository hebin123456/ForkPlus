using System;
using System.Collections.Generic;

namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// Gitmoji 自动补全提供器：当 commit subject 中输入 ":" 后开始匹配 gitmoji 短名。
	/// 触发条件：当前光标前最近的 ":" 到光标之间的子串不含空白，且长度 ≥ 1。
	/// 选中后将 ":prefix" 替换为 "emoji "（emoji 后带一个空格，便于继续输入 commit 描述）。
	/// </summary>
	public class GitmojiAutocompleteProvider : IAutoCompleteProvider
	{
		// 限制返回的最大建议数，避免列表过长影响选择体验
		private const int MaxSuggestions = 12;

		[Null]
		public AutoCompleteSuggestions GetSuggestions(string text, int caretIndex)
		{
			if (string.IsNullOrEmpty(text) || caretIndex <= 0)
			{
				return null;
			}

			// 从光标位置向前查找最近的 ":"，作为触发字符
			// 但 ":" 到光标之间不能有空白（否则视为普通冒号，不触发）
			int colonIndex = -1;
			for (int i = caretIndex - 1; i >= 0; i--)
			{
				char c = text[i];
				if (c == ':')
				{
					colonIndex = i;
					break;
				}
				// 遇到空白、换行则不是 gitmoji 触发
				if (char.IsWhiteSpace(c))
				{
					return null;
				}
			}
			if (colonIndex < 0)
			{
				return null;
			}

			// 用户在 ":" 后输入的过滤前缀（不含 ":" 本身）
			// 例如 ":bu" → prefix = "bu"；仅输入 ":" → prefix = ""
			string prefix = text.Substring(colonIndex + 1, caretIndex - colonIndex - 1);

			// 范围覆盖 ":prefix"，选中后整体替换为 emoji
			Range replaceRange = new Range(colonIndex, caretIndex);

			List<AutoCompleteSuggestion> list = new List<AutoCompleteSuggestion>(MaxSuggestions);
			foreach (GitmojiEntry entry in GitmojiData.Entries)
			{
				// 去掉 shortName 两端的 ":"，再与用户输入做前缀匹配
				// shortName 形如 ":bug:"，去掉冒号后是 "bug"
				string shortNameNoColons = entry.ShortName;
				if (shortNameNoColons.StartsWith(":", StringComparison.Ordinal))
				{
					shortNameNoColons = shortNameNoColons.Substring(1);
				}
				if (shortNameNoColons.EndsWith(":", StringComparison.Ordinal))
				{
					shortNameNoColons = shortNameNoColons.Substring(0, shortNameNoColons.Length - 1);
				}

				bool match;
				if (prefix.Length == 0)
				{
					// 仅输入 ":" → 显示全部
					match = true;
				}
				else
				{
					match = shortNameNoColons.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
						|| shortNameNoColons.IndexOf(prefix, StringComparison.OrdinalIgnoreCase) >= 0;
				}

				if (match)
				{
					list.Add(new GitmojiAutoCompleteSuggestion(replaceRange, entry));
					if (list.Count >= MaxSuggestions)
					{
						break;
					}
				}
			}

			if (list.Count == 0)
			{
				return null;
			}
			return new AutoCompleteSuggestions(colonIndex, list.ToArray());
		}
	}
}
