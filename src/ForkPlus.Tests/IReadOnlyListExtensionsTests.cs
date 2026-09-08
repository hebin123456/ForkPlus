using System;
using System.Collections.Generic;
using Xunit;

namespace ForkPlus.Tests
{
	public class IReadOnlyListExtensionsTests
	{
		[Fact]
		public void Joined_ConcatenatesWithSeparator()
		{
			IReadOnlyList<string> source = new List<string> { "a", "b", "c" };

			Assert.Equal("a/b/c", source.Joined("/"));
		}

		[Fact]
		public void Joined_EmptyListReturnsEmptyString()
		{
			IReadOnlyList<string> source = new List<string>();

			Assert.Equal("", source.Joined("/"));
		}

		[Fact]
		public void Filter_ReturnsMatchingElements()
		{
			IReadOnlyList<int> source = new List<int> { 1, 2, 3, 4 };

			Assert.Equal(new List<int> { 2, 4 }, source.Filter(x => x % 2 == 0));
		}

		[Fact]
		public void FirstItem_ReturnsFirstElement()
		{
			IReadOnlyList<string> source = new List<string> { "a", "b", "c" };

			Assert.Equal("a", source.FirstItem());
		}

		[Fact]
		public void FirstItem_EmptyListReturnsNull()
		{
			IReadOnlyList<string> source = new List<string>();

			Assert.Null(source.FirstItem());
		}

		[Fact]
		public void FirstItem_WithPredicate_ReturnsFirstMatch()
		{
			IReadOnlyList<string> source = new List<string> { "a", "bb", "ccc" };

			Assert.Equal("ccc", source.FirstItem(x => x.Length > 2));
		}

		[Fact]
		public void FirstItem_WithPredicate_NoMatchReturnsNull()
		{
			IReadOnlyList<string> source = new List<string> { "a", "bb" };

			Assert.Null(source.FirstItem(x => x.Length > 5));
		}

		[Fact]
		public void FirstItemStruct_ReturnsFirstElement()
		{
			IReadOnlyList<int> source = new List<int> { 1, 2, 3 };

			Assert.Equal(1, source.FirstItemStruct());
		}

		[Fact]
		public void FirstItemStruct_EmptyListReturnsNull()
		{
			IReadOnlyList<int> source = new List<int>();

			Assert.Null(source.FirstItemStruct());
		}

		[Fact]
		public void Map_TransformsEachElement()
		{
			IReadOnlyList<int> source = new List<int> { 1, 2, 3 };

			Assert.Equal(new[] { 2, 4, 6 }, source.Map(x => x * 2));
		}

		[Fact]
		public void Reversed_ReturnsElementsInReverseOrder()
		{
			IReadOnlyList<int> source = new List<int> { 1, 2, 3 };

			Assert.Equal(new[] { 3, 2, 1 }, source.Reversed());
		}

		[Fact]
		public void SkipFirst_SkipsGivenCount()
		{
			int[] source = { 1, 2, 3 };

			Assert.Equal(new[] { 2, 3 }, source.SkipFirst(1));
		}

		[Fact]
		public void SkipFirst_SkipExceedsLengthReturnsEmpty()
		{
			int[] source = { 1, 2, 3 };

			Assert.Empty(source.SkipFirst(10));
		}

		[Fact]
		public void Zip_PairsCorrespondingElements()
		{
			IReadOnlyList<int> target = new List<int> { 1, 2, 3 };
			IReadOnlyList<string> other = new List<string> { "a", "b", "c" };

			var result = new List<(int, string)>();
			foreach (var pair in target.Zip(other))
			{
				result.Add(pair);
			}

			Assert.Equal(new List<(int, string)> { (1, "a"), (2, "b"), (3, "c") }, result);
		}

		[Fact]
		public void Zip_UnequalLengthsStopsAtShorter()
		{
			IReadOnlyList<int> target = new List<int> { 1, 2, 3 };
			IReadOnlyList<string> other = new List<string> { "a", "b" };

			var result = new List<(int, string)>();
			foreach (var pair in target.Zip(other))
			{
				result.Add(pair);
			}

			Assert.Equal(new List<(int, string)> { (1, "a"), (2, "b") }, result);
		}

		[Fact]
		public void BinarySearchBy_FindsExistingElement()
		{
			IReadOnlyList<int> source = new List<int> { 1, 3, 5, 7 };

			Assert.Equal(2, source.BinarySearchBy(x => x.CompareTo(5)));
		}

		[Fact]
		public void BinarySearchBy_MissingElementReturnsBitwiseComplementOfInsertionPoint()
		{
			IReadOnlyList<int> source = new List<int> { 1, 3, 5, 7 };

			Assert.Equal(~2, source.BinarySearchBy(x => x.CompareTo(4)));
		}

		[Fact]
		public void BinarySearchBy_TerminatesWhenComparatorReturnsNonUnitValues()
		{
			// 回归：RepositoryManager 死循环。NumericIgnoreCaseStringComparer 在 Unix 上
			// 返回任意差值（如 "forkplus" vs "freeze-repo" 的 'o'-'r' = -3），
			// 原 switch 只匹配 -1/0/1，三分支全部落空导致 UI 线程死循环。
			IReadOnlyList<string> source = new List<string> { "ForkPlus", "freeze-repo" };

			int idx = source.BinarySearchBy(x => NumericIgnoreCaseStringComparer.Comparer.Compare(x, "git-repo"));
			Assert.True(idx < 0);
		}

		[Fact]
		public void NumericIgnoreCaseStringComparer_ReturnsOnlyUnitValues()
		{
			// 契约：比较结果恒为 -1/0/1（StrCmpLogicalW 语义），供 BinarySearchBy 等消费。
			Assert.Equal(0, NumericIgnoreCaseStringComparer.Comparer.Compare("abc", "abc"));
			Assert.Equal(0, NumericIgnoreCaseStringComparer.Comparer.Compare("a2", "A2"));
			// 数值相等但前导零不同：前导零多者在后（Explorer 行为）→ -1/1，非任意差值。
			Assert.Equal(-1, NumericIgnoreCaseStringComparer.Comparer.Compare("a2", "a02"));
			Assert.Equal(1, NumericIgnoreCaseStringComparer.Comparer.Compare("a02", "a2"));
			int less = NumericIgnoreCaseStringComparer.Comparer.Compare("ForkPlus", "freeze-repo");
			int greater = NumericIgnoreCaseStringComparer.Comparer.Compare("freeze-repo", "ForkPlus");
			Assert.Equal(-1, less);
			Assert.Equal(1, greater);
		}

		[Fact]
		public void CompactMapStruct_FiltersOutNullResults()
		{
			IReadOnlyList<string> source = new List<string> { "1", "abc", "3" };

			Assert.Equal(new List<int> { 1, 3 }, source.CompactMapStruct(s => int.TryParse(s, out int v) ? (int?)v : null));
		}
	}
}
