using System;
using ForkPlus.Git;
using ForkPlus.Git.Commands;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>WS：提交签名（GPG/SSH）。CI 无 gpg，全部用 fixture 字符串验证
	/// RevisionDetailsParser 的 %G?/%GS 扩展与状态映射，不落盘、不起 git 进程。</summary>
	public class CommitSigningTests
	{
		private const string Separator = "±.";

		private static string ShaHex(int fill)
		{
			return new string('a', 39) + fill.ToString("X").ToLower();
		}

		/// <summary>按 RevisionDetailsParser.FormatString 的字段序拼一行修订详情：
		/// %H %aN %aE %at %cN %cE %ct %P %G? %GS %B。</summary>
		private static string BuildDetailsLine(string signatureCode, string signer, string message)
		{
			return string.Join(Separator, new[]
			{
				ShaHex(1),
				"Author",
				"author@example.com",
				"1700000000",
				"Committer",
				"committer@example.com",
				"1700000000",
				ShaHex(2),
				signatureCode,
				signer,
				message
			});
		}

		[Fact]
		public void FormatString_ContainsSignaturePlaceholdersBeforeMessage()
		{
			Assert.Contains("%P±.%G?±.%GS±.%B", RevisionDetailsParser.FormatString);
		}

		[Theory]
		[InlineData('G', CommitSignatureStatus.Good)]
		[InlineData('B', CommitSignatureStatus.Bad)]
		[InlineData('U', CommitSignatureStatus.Unverified)]
		[InlineData('X', CommitSignatureStatus.Expired)]
		[InlineData('Y', CommitSignatureStatus.ExpiredKey)]
		[InlineData('R', CommitSignatureStatus.RevokedKey)]
		[InlineData('E', CommitSignatureStatus.Error)]
		[InlineData('N', CommitSignatureStatus.None)]
		public void ParseSignatureStatus_MapsGitPlaceholderChars(char code, CommitSignatureStatus expected)
		{
			Assert.Equal(expected, RevisionDetailsParser.ParseSignatureStatus(code.ToString()));
		}

		[Theory]
		[InlineData("", CommitSignatureStatus.Error)]
		[InlineData("g", CommitSignatureStatus.Error)]
		[InlineData("Z", CommitSignatureStatus.Error)]
		[InlineData("N\n", CommitSignatureStatus.None)]
		public void ParseSignatureStatus_EmptyOrUnknownFallsBackToError(string code, CommitSignatureStatus expected)
		{
			Assert.Equal(expected, RevisionDetailsParser.ParseSignatureStatus(code));
		}

		[Fact]
		public void Parse_SignedCommit_PopulatesStatusAndSigner()
		{
			RevisionDetails details = RevisionDetailsParser.Parse(BuildDetailsLine("G", "Alice <alice@example.com>", "signed commit subject"));
			Assert.NotNull(details);
			Assert.Equal(CommitSignatureStatus.Good, details.SignatureStatus);
			Assert.Equal("Alice <alice@example.com>", details.Signer);
			Assert.Equal("signed commit subject", details.Message);
		}

		[Fact]
		public void Parse_UnsignedCommit_EmptySignerFieldKeepsFieldOrderStable()
		{
			// %GS 为空时输出两个连续分隔符，Split(None) 保留空字段，message 仍落在 array[10]
			RevisionDetails details = RevisionDetailsParser.Parse(BuildDetailsLine("N", "", "unsigned commit subject"));
			Assert.NotNull(details);
			Assert.Equal(CommitSignatureStatus.None, details.SignatureStatus);
			Assert.Equal(string.Empty, details.Signer);
			Assert.Equal("unsigned commit subject", details.Message);
		}

		[Fact]
		public void Parse_BadSignature_PopulatesBadStatus()
		{
			RevisionDetails details = RevisionDetailsParser.Parse(BuildDetailsLine("B", "Mallory <mallory@example.com>", "tampered"));
			Assert.NotNull(details);
			Assert.Equal(CommitSignatureStatus.Bad, details.SignatureStatus);
			Assert.Equal("Mallory <mallory@example.com>", details.Signer);
		}

		[Fact]
		public void Parse_LegacyFormatWithoutSignatureFields_ReturnsNull()
		{
			// 旧 9 字段输出（无 %G?/%GS）不再可解析，防止字段错位静默吞掉签名位
			string legacy = string.Join(Separator, new[]
			{
				ShaHex(1),
				"Author",
				"author@example.com",
				"1700000000",
				"Committer",
				"committer@example.com",
				"1700000000",
				ShaHex(2),
				"legacy message"
			});
			Assert.Null(RevisionDetailsParser.Parse(legacy));
		}

		[Fact]
		public void RevisionDetails_DefaultSignatureFields_AreNoneAndEmpty()
		{
			RevisionDetails details = new RevisionDetails(Sha.Parse(ShaHex(3)).Value, new UserIdentity("A", "a@b.c"), DateTime.UnixEpoch, new UserIdentity("A", "a@b.c"), DateTime.UnixEpoch, new Sha[0], "m");
			Assert.Equal(CommitSignatureStatus.None, details.SignatureStatus);
			Assert.Equal(string.Empty, details.Signer);
		}
	}
}
