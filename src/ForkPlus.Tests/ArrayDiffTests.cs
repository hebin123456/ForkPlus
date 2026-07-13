using System;
using System.Collections.Generic;
using Xunit;

namespace ForkPlus.Tests
{
	public class ArrayDiffTests
	{
		[Fact]
		public void Diff_ReturnsOnlyOldAndOnlyNew()
		{
			int[] oldItems = { 1, 2, 3 };
			int[] newItems = { 2, 3, 4 };

			(int[] onlyOld, int[] onlyNew) = ArrayDiff.Diff(oldItems, newItems, Comparer<int>.Default);

			Assert.Equal(new[] { 1 }, onlyOld);
			Assert.Equal(new[] { 4 }, onlyNew);
		}

		[Fact]
		public void Diff_OldNull_ReturnsEmptyAndCopyOfNew()
		{
			int[] newItems = { 1, 2 };

			(int[] onlyOld, int[] onlyNew) = ArrayDiff.Diff(null, newItems, Comparer<int>.Default);

			Assert.Empty(onlyOld);
			Assert.Equal(new[] { 1, 2 }, onlyNew);
		}

		[Fact]
		public void Diff_NewNull_ThrowsNullReferenceException()
		{
			int[] oldItems = { 1, 2 };

			Assert.Throws<NullReferenceException>(() =>
				ArrayDiff.Diff(oldItems, null, Comparer<int>.Default));
		}

		[Fact]
		public void Diff_IdenticalArrays_ReturnsBothEmpty()
		{
			int[] oldItems = { 1, 2, 3 };
			int[] newItems = { 1, 2, 3 };

			(int[] onlyOld, int[] onlyNew) = ArrayDiff.Diff(oldItems, newItems, Comparer<int>.Default);

			Assert.Empty(onlyOld);
			Assert.Empty(onlyNew);
		}

		[Fact]
		public void Diff_CompletelyDifferent_ReturnsBothAsOriginal()
		{
			int[] oldItems = { 1, 2, 3 };
			int[] newItems = { 4, 5, 6 };

			(int[] onlyOld, int[] onlyNew) = ArrayDiff.Diff(oldItems, newItems, Comparer<int>.Default);

			Assert.Equal(new[] { 1, 2, 3 }, onlyOld);
			Assert.Equal(new[] { 4, 5, 6 }, onlyNew);
		}

		[Fact]
		public void Diff_OldNull_ReturnsCopyNotSameReference()
		{
			int[] newItems = { 1, 2 };

			(int[] onlyOld, int[] onlyNew) = ArrayDiff.Diff(null, newItems, Comparer<int>.Default);

			Assert.NotSame(newItems, onlyNew);
			Assert.Equal(newItems, onlyNew);
		}
	}
}
