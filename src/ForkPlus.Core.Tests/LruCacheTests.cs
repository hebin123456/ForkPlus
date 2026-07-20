using Xunit;

namespace ForkPlus.Tests
{
	public class LruCacheTests
	{
		[Fact]
		public void PutAndGet_ReturnsStoredValue()
		{
			var cache = new LruCache<string, int>(2);
			cache.Put("a", 1);
			Assert.True(cache.TryGet("a", out int value));
			Assert.Equal(1, value);
		}

		[Fact]
		public void TryGet_MissingKeyReturnsFalseAndDefaultValue()
		{
			var cache = new LruCache<string, int>(2);
			cache.Put("a", 1);
			Assert.False(cache.TryGet("missing", out int value));
			Assert.Equal(0, value);
		}

		[Fact]
		public void Put_EvictsLeastRecentlyUsedWhenCapacityExceeded()
		{
			var cache = new LruCache<string, int>(2);
			cache.Put("a", 1);
			cache.Put("b", 2);
			cache.Put("c", 3);
			Assert.False(cache.TryGet("a", out _));
			Assert.True(cache.TryGet("b", out int bValue));
			Assert.Equal(2, bValue);
			Assert.True(cache.TryGet("c", out int cValue));
			Assert.Equal(3, cValue);
		}

		[Fact]
		public void TryGet_PromotesKeyToMostRecentlyUsed()
		{
			var cache = new LruCache<string, int>(2);
			cache.Put("a", 1);
			cache.Put("b", 2);
			Assert.True(cache.TryGet("a", out _));
			cache.Put("c", 3);
			Assert.False(cache.TryGet("b", out _));
			Assert.True(cache.TryGet("a", out _));
			Assert.True(cache.TryGet("c", out _));
		}

		[Fact]
		public void Put_ExistingKeyUpdatesValueAndPromotesToMostRecentlyUsed()
		{
			var cache = new LruCache<string, int>(2);
			cache.Put("a", 1);
			cache.Put("b", 2);
			cache.Put("a", 100);
			Assert.True(cache.TryGet("a", out int aValue));
			Assert.Equal(100, aValue);
			cache.Put("c", 3);
			Assert.False(cache.TryGet("b", out _));
			Assert.True(cache.TryGet("a", out _));
			Assert.True(cache.TryGet("c", out _));
		}

		[Fact]
		public void Capacity_One_EvictsPreviousEntryImmediately()
		{
			var cache = new LruCache<int, string>(1);
			cache.Put(1, "one");
			Assert.True(cache.TryGet(1, out string v1));
			Assert.Equal("one", v1);
			cache.Put(2, "two");
			Assert.False(cache.TryGet(1, out _));
			Assert.True(cache.TryGet(2, out string v2));
			Assert.Equal("two", v2);
		}

		[Fact]
		public void Put_SameKeyDoesNotExceedCapacity()
		{
			var cache = new LruCache<string, int>(2);
			cache.Put("a", 1);
			cache.Put("a", 2);
			cache.Put("a", 3);
			Assert.True(cache.TryGet("a", out int value));
			Assert.Equal(3, value);
		}
	}
}
