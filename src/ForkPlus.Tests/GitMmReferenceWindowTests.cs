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
			Assert.Contains("<th><code>-a</code></th>", result.Result);
			Assert.DoesNotContain("&lt;table&gt;", result.Result);
		}
	}
}
