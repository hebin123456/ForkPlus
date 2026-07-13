using ForkPlus.Git;
using Xunit;

namespace ForkPlus.Tests
{
	/// <summary>
	/// Sha 是值类型（readonly struct）。历史上多次出现把 Sha 当引用类型用：
	/// 与 null 比较（CS0019 编译错误 / CS8073 警告恒为 false）、误用 ? 运算符（CS0023）。
	/// 这些用例锁住 struct 语义，防止回归。
	/// </summary>
	public class ShaTests
	{
		private const string SampleSha40 = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";

		[Fact]
		public void Zero_ToStringIsAllZeros()
		{
			Assert.Equal("0000000000000000000000000000000000000000", Sha.Zero.ToString());
		}

		[Fact]
		public void Zero_ToAbbreviatedStringIsSevenZeros()
		{
			Assert.Equal("0000000", Sha.Zero.ToAbbreviatedString());
		}

		[Fact]
		public void Zero_EqualsItself()
		{
			Assert.True(Sha.Zero == Sha.Zero);
			Assert.False(Sha.Zero != Sha.Zero);
			Assert.True(Sha.Zero.Equals(Sha.Zero));
			Assert.True(Sha.Zero.Equals((object)Sha.Zero));
		}

		[Fact]
		public void Parse_Valid40CharString_ReturnsSha()
		{
			Sha? parsed = Sha.Parse(SampleSha40);

			Assert.NotNull(parsed);
			Assert.Equal(SampleSha40, parsed.Value.ToString());
		}

		[Theory]
		[InlineData("")]
		[InlineData("abc")]
		[InlineData("4b825dc642cb6eb9a060e54bf8d69288fbee490")]   // 39 chars
		[InlineData("4b825dc642cb6eb9a060e54bf8d69288fbee49044")] // 41 chars
		public void Parse_InvalidLength_ReturnsNull(string input)
		{
			Assert.Null(Sha.Parse(input));
		}

		[Fact]
		public void TryParse_ValidString_Succeeds()
		{
			bool ok = Sha.TryParse(SampleSha40, out Sha result);

			Assert.True(ok);
			Assert.Equal(SampleSha40, result.ToString());
		}

		[Fact]
		public void TryParse_InvalidString_ReturnsNullShaAndFalse()
		{
			bool ok = Sha.TryParse("nope", out Sha result);

			Assert.False(ok);
			Assert.Equal(Sha.NullSha, result);
		}

		[Fact]
		public void ToAbbreviatedString_ReturnsFirstSevenHexChars()
		{
			Sha sha = Sha.Parse(SampleSha40).Value;

			// 4b825dc6 / 16 = 0x04b825dc → "x7" → "4b825dc"
			Assert.Equal("4b825dc", sha.ToAbbreviatedString());
		}

		[Fact]
		public void Equals_DifferentShas_ReturnsFalse()
		{
			Sha a = Sha.Parse(SampleSha40).Value;
			Sha b = Sha.Zero;

			Assert.False(a == b);
			Assert.True(a != b);
			Assert.False(a.Equals(b));
		}

		[Fact]
		public void NullSha_IsNotZero()
		{
			Assert.NotEqual(Sha.Zero, Sha.NullSha);
		}
	}
}
