using ForkPlus.Git;
using Xunit;

namespace ForkPlus.Tests
{
	public class ChangeTypeHelperTests
	{
		[Theory]
		[InlineData("A", ChangeType.Added)]
		[InlineData("D", ChangeType.Deleted)]
		[InlineData("R", ChangeType.Renamed)]
		[InlineData("C", ChangeType.Copied)]
		[InlineData("M", ChangeType.Modified)]
		[InlineData("U", ChangeType.Unmerged)]
		[InlineData("T", ChangeType.TypeChanged)]
		[InlineData("X", ChangeType.Unknown)]
		[InlineData("Z", ChangeType.Unknown)]
		[InlineData("", ChangeType.Unknown)]
		public void Parse_MapsSingleLetterPrefixToChangeType(string text, ChangeType expected)
		{
			Assert.Equal(expected, ChangeTypeHelper.Parse(text));
		}

		[Theory]
		[InlineData("Added", ChangeType.Added)]
		[InlineData("Deleted", ChangeType.Deleted)]
		[InlineData("Renamed", ChangeType.Renamed)]
		[InlineData("Copied", ChangeType.Copied)]
		[InlineData("Modified", ChangeType.Modified)]
		[InlineData("Unmerged", ChangeType.Unmerged)]
		[InlineData("TypeChanged", ChangeType.TypeChanged)]
		public void Parse_OnlyConsidersFirstLetter(string text, ChangeType expected)
		{
			Assert.Equal(expected, ChangeTypeHelper.Parse(text));
		}

		[Fact]
		public void GetImageSource_DeletedUsesRemoveIconNotEdit()
		{
			// 回归（2026-10-18，v5.1.5 重发布）：GetIconKey 对 Deleted 返回 IconKeys.StatusRemove
			//（"Status.Remove"），而图标桥接（BridgeExtensions）的 switch 只认 StatusDelete
			//（"Status.Delete"）→ 落入默认分支返回铅笔（黄色 Modify 图标）。断言 Deleted 的
			// 图标实例与 Modified/Added/Renamed/Copied/Untracked 互不相同（旧代码与 Modified
			// 同为 EditIcon 静态实例 → 引用相等 → 回归红灯）。
			HeadlessAppBootstrap.Run(delegate
			{
				var deletedChange = ChangeType.Deleted.GetImageSource();
				Assert.NotSame(ChangeType.Modified.GetImageSource(), deletedChange);
				Assert.NotSame(ChangeType.Added.GetImageSource(), deletedChange);
				Assert.NotSame(ChangeType.Untracked.GetImageSource(), deletedChange);
				Assert.NotSame(ChangeType.Renamed.GetImageSource(), deletedChange);
				Assert.NotSame(ChangeType.Copied.GetImageSource(), deletedChange);

				var deletedStatus = StatusType.Deleted.GetImageSource();
				Assert.NotSame(StatusType.Modified.GetImageSource(), deletedStatus);
				Assert.NotSame(StatusType.Added.GetImageSource(), deletedStatus);
				Assert.NotSame(StatusType.Renamed.GetImageSource(), deletedStatus);
			});
		}
	}
}
