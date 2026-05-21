using ForkPlus.UI.Controls;

namespace ForkPlus.UI.Dialogs
{
	public class CommitsCountDataSource : ITreemapDataSource
	{
		public struct Item
		{
			public string Title { get; }

			public int SizeValue { get; }

			[Null]
			public Item[] Children { get; }

			public Item(string title, int sizeValue, Item[] children = null)
			{
				Title = title;
				SizeValue = sizeValue;
				Children = children;
			}
		}

		private Item[] Items { get; }

		public CommitsCountDataSource(Item[] items)
		{
			Items = items;
		}

		public object GetRootItems()
		{
			return Items;
		}

		public object GetItemChildren(object array, int index)
		{
			Item item = (array as Item[])[index];
			_ = item.Children;
			return item.Children;
		}

		public int? GetItemChildrenCount(object array, int? index)
		{
			Item[] array2 = (Item[])array;
			if (index.HasValue)
			{
				int valueOrDefault = index.GetValueOrDefault();
				return array2[valueOrDefault].Children?.Length;
			}
			return array2.Length;
		}

		public long GetItemSizeValue(object array, int index)
		{
			Item item = (array as Item[])[index];
			return item.SizeValue;
		}
	}
}
