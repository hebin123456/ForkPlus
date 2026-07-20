using System;
using ForkPlus.Git;
using Xunit;

namespace ForkPlus.Tests
{
	public class ShaCompareTests
	{
		private const string SampleSha40 = "4b825dc642cb6eb9a060e54bf8d69288fbee4904";

		[Fact]
		public void CompareTo_SameSha_ReturnsZero()
		{
			Sha sha = Sha.Parse(SampleSha40).Value;

			Assert.Equal(0, sha.CompareTo(sha));
		}

		[Fact]
		public void CompareTo_AllFiveDwordsEqual_ReturnsZero()
		{
			Sha a = new Sha(0x12345678u, 0xabcdef01u, 0xdeadbeefu, 0xfeedfaceu, 0xc0ffee42u);
			Sha b = new Sha(0x12345678u, 0xabcdef01u, 0xdeadbeefu, 0xfeedfaceu, 0xc0ffee42u);

			Assert.Equal(0, a.CompareTo(b));
		}

		[Fact]
		public void CompareTo_GreaterDw1_ReturnsPositive()
		{
			Sha smaller = new Sha(0x00000001u, 0u, 0u, 0u, 0u);
			Sha greater = new Sha(0x00000002u, 0u, 0u, 0u, 0u);

			Assert.True(greater.CompareTo(smaller) > 0);
		}

		[Fact]
		public void CompareTo_SmallerDw1_ReturnsNegative()
		{
			Sha smaller = new Sha(0x00000001u, 0u, 0u, 0u, 0u);
			Sha greater = new Sha(0x00000002u, 0u, 0u, 0u, 0u);

			Assert.True(smaller.CompareTo(greater) < 0);
		}

		[Fact]
		public void CompareTo_SameDw1DifferentDw2_ComparesByDw2()
		{
			Sha a = new Sha(0x00000001u, 0x00000001u, 0u, 0u, 0u);
			Sha b = new Sha(0x00000001u, 0x00000002u, 0u, 0u, 0u);

			Assert.True(a.CompareTo(b) < 0);
			Assert.True(b.CompareTo(a) > 0);
		}

		[Fact]
		public void CompareTo_SameDw1AndDw2DifferentDw3_ComparesByDw3()
		{
			Sha a = new Sha(0x00000001u, 0x00000001u, 0x00000001u, 0u, 0u);
			Sha b = new Sha(0x00000001u, 0x00000001u, 0x00000002u, 0u, 0u);

			Assert.True(a.CompareTo(b) < 0);
		}

		[Fact]
		public void ArraySort_OrdersShasInAscendingDw1Order()
		{
			Sha high = new Sha(0xffffffffu, 0u, 0u, 0u, 0u);
			Sha mid = new Sha(0x00000080u, 0u, 0u, 0u, 0u);
			Sha low = Sha.Zero;
			Sha[] shas = { high, mid, low };

			Array.Sort(shas);

			Assert.Equal(low, shas[0]);
			Assert.Equal(mid, shas[1]);
			Assert.Equal(high, shas[2]);
		}

		[Fact]
		public void ArraySort_OrdersShasByFullDwordSequence()
		{
			Sha greater = new Sha(0x00000001u, 0x00000002u, 0u, 0u, 0u);
			Sha lesser = new Sha(0x00000001u, 0x00000001u, 0u, 0u, 0u);
			Sha[] shas = { greater, lesser };

			Array.Sort(shas);

			Assert.Equal(lesser, shas[0]);
			Assert.Equal(greater, shas[1]);
		}

		[Fact]
		public void GetHashCode_SameSha_ReturnsSameHash()
		{
			Sha a = Sha.Parse(SampleSha40).Value;
			Sha b = Sha.Parse(SampleSha40).Value;

			Assert.Equal(a.GetHashCode(), b.GetHashCode());
		}

		[Fact]
		public void GetHashCode_ReturnsDw1AsInt()
		{
			Sha sha = new Sha(0x12345678u, 0u, 0u, 0u, 0u);

			Assert.Equal((int)0x12345678u, sha.GetHashCode());
		}

		[Fact]
		public void GetHashCode_ZeroSha_ReturnsZero()
		{
			Assert.Equal(0, Sha.Zero.GetHashCode());
		}
	}
}
