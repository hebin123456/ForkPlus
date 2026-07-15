namespace ForkPlus.UI.Controls
{
	/// <summary>
	/// Gitmoji 自动补全建议：携带 emoji 字形和描述用于列表显示。
	/// 选中后插入到 commit subject 的是 <see cref="AutoCompleteSuggestion.Suggestion"/>
	/// （即 emoji 字形 + 一个空格），替换用户已输入的 :prefix。
	/// </summary>
	public class GitmojiAutoCompleteSuggestion : AutoCompleteSuggestion
	{
		/// <summary>原始 Gitmoji 条目，供 DataTemplate 显示用。</summary>
		public GitmojiEntry Entry { get; }

		public GitmojiAutoCompleteSuggestion(Range range, GitmojiEntry entry)
			: base(range, entry.Emoji + " ")
		{
			Entry = entry;
		}
	}
}
