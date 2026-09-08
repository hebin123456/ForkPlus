using ForkPlus.UI.Dialogs;
using Xunit;

namespace ForkPlus.Tests
{
	public class GitMmReferenceWindowTests
	{
		[Fact]
		public void MarkdownToHtml_RendersPipeTablesAsHtmlTables()
		{
			string markdown = "## Flags\n\n| Flag | Description |\n| --- | --- |\n| `-a` | Fetch all branches. |\n";

			var result = GitMmReferenceWindow.MarkdownToHtml(markdown);

			Assert.True(result.Succeeded, result.Error?.FriendlyDescription);
			Assert.Contains("<table>", result.Result);
			// 迁移修复：原版此断言写错（原仓库就坏，与平台无关）——`-a` 在第三行是数据行
			// （tbody/td），不是表头（thead/th）。实测输出：header 行 → <th>Flag</th>，
			// 数据行 → <td><code>-a</code></td>（代码片段转换正确）。
			Assert.Contains("<th>Flag</th>", result.Result);
			Assert.Contains("<td><code>-a</code></td>", result.Result);
			Assert.DoesNotContain("&lt;table&gt;", result.Result);
		}
	}
}
