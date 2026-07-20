using System;
using System.Collections.Generic;
using Xunit;

namespace ForkPlus.Tests
{
	public class ArrayExtensionsTests
	{
		[Fact]
		public void ContainsItem_Value_EmptyArray_ReturnsFalse()
		{
			int[] source = new int[0];

			Assert.False(source.ContainsItem(1));
		}

		[Fact]
		public void ContainsItem_Value_Hit_ReturnsTrue()
		{
			int[] source = { 1, 2, 3 };

			Assert.True(source.ContainsItem(2));
		}

		[Fact]
		public void ContainsItem_Value_Miss_ReturnsFalse()
		{
			int[] source = { 1, 2, 3 };

			Assert.False(source.ContainsItem(5));
		}

		[Fact]
		public void ContainsItem_Predicate_EmptyArray_ReturnsFalse()
		{
			int[] source = new int[0];

			Assert.False(source.ContainsItem(x => x == 1));
		}

		[Fact]
		public void ContainsItem_Predicate_Hit_ReturnsTrue()
		{
			int[] source = { 1, 2, 3 };

			Assert.True(source.ContainsItem(x => x > 2));
		}

		[Fact]
		public void ContainsItem_Predicate_Miss_ReturnsFalse()
		{
			int[] source = { 1, 2, 3 };

			Assert.False(source.ContainsItem(x => x > 10));
		}

		[Fact]
		public void IndexOfItem_Hit_ReturnsIndex()
		{
			int[] source = { 10, 20, 30 };

			int? actual = source.IndexOfItem(x => x == 20);

			Assert.NotNull(actual);
			Assert.Equal(1, actual.Value);
		}

		[Fact]
		public void IndexOfItem_Miss_ReturnsNull()
		{
			int[] source = { 10, 20, 30 };

			int? actual = source.IndexOfItem(x => x == 999);

			Assert.Null(actual);
		}

		[Fact]
		public void IndexOfItem_EmptyArray_ReturnsNull()
		{
			int[] source = new int[0];

			Assert.Null(source.IndexOfItem(x => x == 1));
		}

		[Fact]
		public void FirstItem_Hit_ReturnsMatchingElement()
		{
			string[] source = { "a", "bb", "ccc" };

			string actual = source.FirstItem(3, (s, len) => s.Length == len);

			Assert.Equal("ccc", actual);
		}

		[Fact]
		public void FirstItem_Miss_ReturnsNull()
		{
			string[] source = { "a", "bb", "ccc" };

			Assert.Null(source.FirstItem(10, (s, len) => s.Length == len));
		}

		[Fact]
		public void SingleItem_SingleElementArray_ReturnsElement()
		{
			string[] source = { "only" };

			Assert.Equal("only", source.SingleItem());
		}

		[Fact]
		public void SingleItem_EmptyArray_ReturnsNull()
		{
			string[] source = new string[0];

			Assert.Null(source.SingleItem());
		}

		[Fact]
		public void SingleItem_MultiElementArray_ReturnsNull()
		{
			string[] source = { "a", "b" };

			Assert.Null(source.SingleItem());
		}

		[Fact]
		public void LastItem_NonEmptyArray_ReturnsLastElement()
		{
			string[] source = { "a", "b", "c" };

			Assert.Equal("c", source.LastItem());
		}

		[Fact]
		public void LastItem_EmptyArray_ReturnsNull()
		{
			string[] source = new string[0];

			Assert.Null(source.LastItem());
		}

		[Fact]
		public void CompactMap_FiltersNullResults()
		{
			string[] source = { "a", null, "b", null, "c" };

			string[] result = source.CompactMap(s => s);

			Assert.Equal(new[] { "a", "b", "c" }, result);
		}

		[Fact]
		public void CompactMap_TransformsAndFiltersNulls()
		{
			string[] source = { "a", null, "b" };

			string[] result = source.CompactMap(s => s?.ToUpper());

			Assert.Equal(new[] { "A", "B" }, result);
		}

		[Fact]
		public void AnyItem_Matching_ReturnsTrue()
		{
			int[] source = { 1, 2, 3 };

			Assert.True(source.AnyItem(x => x > 2));
		}

		[Fact]
		public void AnyItem_NoMatch_ReturnsFalse()
		{
			int[] source = { 1, 2, 3 };

			Assert.False(source.AnyItem(x => x > 10));
		}

		[Fact]
		public void AnyItem_EmptyArray_ReturnsFalse()
		{
			int[] source = new int[0];

			Assert.False(source.AnyItem(x => true));
		}

		[Fact]
		public void AllItems_AllMatch_ReturnsTrue()
		{
			int[] source = { 2, 4, 6 };

			Assert.True(source.AllItems(x => x % 2 == 0));
		}

		[Fact]
		public void AllItems_SomeDoNotMatch_ReturnsFalse()
		{
			int[] source = { 2, 3, 6 };

			Assert.False(source.AllItems(x => x % 2 == 0));
		}

		[Fact]
		public void AllItems_EmptyArray_ReturnsTrue()
		{
			int[] source = new int[0];

			Assert.True(source.AllItems(x => false));
		}

		[Fact]
		public void Subsequence_ReturnsSlice()
		{
			int[] source = { 1, 2, 3, 4 };

			int[] result = source.Subsequence(1, 2);

			Assert.Equal(new[] { 2, 3 }, result);
		}

		[Fact]
		public void Subsequence_SkipZero_ReturnsFromStart()
		{
			int[] source = { 1, 2, 3, 4 };

			int[] result = source.Subsequence(0, 2);

			Assert.Equal(new[] { 1, 2 }, result);
		}

		[Fact]
		public void Subsequence_TakeZero_ReturnsEmpty()
		{
			int[] source = { 1, 2, 3, 4 };

			int[] result = source.Subsequence(1, 0);

			Assert.Empty(result);
		}

		[Fact]
		public void Subsequence_SkipBeyondLength_ReturnsEmpty()
		{
			int[] source = { 1, 2, 3, 4 };

			int[] result = source.Subsequence(10, 2);

			Assert.Empty(result);
		}

		[Fact]
		public void Subsequence_TakeBeyondLength_ReturnsUpToEnd()
		{
			int[] source = { 1, 2, 3, 4 };

			int[] result = source.Subsequence(2, 10);

			Assert.Equal(new[] { 3, 4 }, result);
		}

		[Fact]
		public void ToSortedArray_SortsCopyAndLeavesOriginalUnchanged()
		{
			int[] source = { 3, 1, 2 };

			int[] result = source.ToSortedArray((a, b) => b.CompareTo(a));

			Assert.Equal(new[] { 3, 2, 1 }, result);
			Assert.Equal(new[] { 3, 1, 2 }, source);
		}

		[Fact]
		public void CreateIndex_ReturnsSortedIndexesAndLeavesOriginalUnchanged()
		{
			int[] source = { 3, 1, 2 };

			int[] index = source.CreateIndex((a, b) => a.CompareTo(b));

			Assert.Equal(new[] { 1, 2, 0 }, index);
			Assert.Equal(new[] { 3, 1, 2 }, source);
		}

		[Fact]
		public void CopyArray_ReturnsDistinctCopy()
		{
			int[] source = { 1, 2, 3 };

			int[] copy = source.CopyArray();

			Assert.NotSame(source, copy);
			Assert.Equal(source, copy);

			copy[0] = 99;
			Assert.Equal(1, source[0]);
		}

		[Fact]
		public void GroupIndexes_GroupsByFirstLetter()
		{
			string[] source = { "a", "b", "a" };

			Dictionary<char, int[]> result = source.GroupIndexes(s => s[0]);

			Assert.Equal(2, result.Count);
			Assert.Equal(new[] { 0, 2 }, result['a']);
			Assert.Equal(new[] { 1 }, result['b']);
		}
	}
}
