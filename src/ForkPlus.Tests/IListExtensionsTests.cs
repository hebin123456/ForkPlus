using System;
using System.Collections.Generic;
using Xunit;

namespace ForkPlus.Tests
{
	public class IListExtensionsTests
	{
		[Fact]
		public void AnyItem_ReturnsTrueWhenMatchExists()
		{
			IList<int> source = new List<int> { 1, 2, 3 };

			Assert.True(source.AnyItem(x => x > 2));
		}

		[Fact]
		public void AnyItem_ReturnsFalseWhenNoMatch()
		{
			IList<int> source = new List<int> { 1, 2, 3 };

			Assert.False(source.AnyItem(x => x > 5));
		}

		[Fact]
		public void AnyItem_EmptyListReturnsFalse()
		{
			IList<int> source = new List<int>();

			Assert.False(source.AnyItem(x => true));
		}

		[Fact]
		public void IndexOf_ReturnsIndexWhenFound()
		{
			IList<int> source = new List<int> { 1, 2, 3 };

			Assert.Equal(1, source.IndexOf(x => x == 2));
		}

		[Fact]
		public void IndexOf_ReturnsNegativeOneWhenNotFound()
		{
			IList<int> source = new List<int> { 1, 2, 3 };

			Assert.Equal(-1, source.IndexOf(x => x == 5));
		}

		[Fact]
		public void IndexOf_EmptyListReturnsNegativeOne()
		{
			IList<int> source = new List<int>();

			Assert.Equal(-1, source.IndexOf(x => true));
		}

		[Fact]
		public void ContainsItem_ReturnsTrueWhenMatchExists()
		{
			IList<int> source = new List<int> { 1, 2, 3 };

			Assert.True(source.ContainsItem(x => x == 2));
		}

		[Fact]
		public void ContainsItem_ReturnsFalseWhenNoMatch()
		{
			IList<int> source = new List<int> { 1, 2, 3 };

			Assert.False(source.ContainsItem(x => x == 5));
		}

		[Fact]
		public void UnstableRemove_RemovesAndReturnsMatchingElement()
		{
			var source = new List<string> { "a", "b", "c" };

			string removed = source.UnstableRemove(x => x == "b");

			Assert.Equal("b", removed);
			Assert.DoesNotContain("b", source);
			Assert.Equal(2, source.Count);
		}

		[Fact]
		public void UnstableRemove_NoMatchReturnsNull()
		{
			var source = new List<string> { "a", "b", "c" };

			string removed = source.UnstableRemove(x => x == "z");

			Assert.Null(removed);
			Assert.Equal(3, source.Count);
		}

		[Fact]
		public void UnstableRemoveStruct_RemovesAndReturnsMatchingElement()
		{
			var source = new List<int> { 1, 2, 3 };

			int? removed = source.UnstableRemoveStruct(x => x == 2);

		Assert.Equal(2, removed);
		Assert.DoesNotContain(2, source);
		Assert.Equal(2, source.Count);
		}

		[Fact]
		public void UnstableRemoveStruct_NoMatchReturnsNull()
		{
			var source = new List<int> { 1, 2, 3 };

			int? removed = source.UnstableRemoveStruct(x => x == 5);

			Assert.Null(removed);
			Assert.Equal(3, source.Count);
		}

		[Fact]
		public void UnstableRemoveAt_RemovesAndReturnsElementAtIndex()
		{
			var source = new List<string> { "a", "b", "c" };

			string removed = source.UnstableRemoveAt(0);

		Assert.Equal("a", removed);
		Assert.Equal(2, source.Count);
		Assert.DoesNotContain("a", source);
		}
	}
}
