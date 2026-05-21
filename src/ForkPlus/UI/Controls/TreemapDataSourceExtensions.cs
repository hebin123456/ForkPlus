namespace ForkPlus.UI.Controls
{
	public static class TreemapDataSourceExtensions
	{
		[Null]
		public static long? GetItemValue(this ITreemapDataSource dataSource, Treemap.IndexPath indexPath)
		{
			long? result = null;
			object array = dataSource.GetRootItems();
			for (int i = 0; i < indexPath.Count; i++)
			{
				int index = indexPath[i];
				if (i == 0)
				{
					result = dataSource.GetItemSizeValue(array, index);
					array = dataSource.GetItemChildren(array, index);
					continue;
				}
				result = dataSource.GetItemSizeValue(array, index);
				if (i != indexPath.Count - 1)
				{
					array = dataSource.GetItemChildren(array, index);
				}
			}
			return result;
		}

		[Null]
		public static Treemap.IndexPath FirstVisualItem(this ITreemapDataSource dataSource)
		{
			Treemap.IndexPath indexPath = new Treemap.IndexPath();
			object obj = dataSource.GetRootItems();
			while (true)
			{
				int? num = dataSource.MaxItemIndex(obj);
				if (!num.HasValue)
				{
					break;
				}
				int valueOrDefault = num.GetValueOrDefault();
				indexPath.Add(valueOrDefault);
				if (!(dataSource.GetItemChildrenCount(obj, valueOrDefault) is int))
				{
					break;
				}
				obj = dataSource.GetItemChildren(obj, valueOrDefault);
			}
			return indexPath;
		}

		[Null]
		public static int? MaxItemIndex(this ITreemapDataSource dataSource, object folder)
		{
			int? itemChildrenCount = dataSource.GetItemChildrenCount(folder, null);
			if (!itemChildrenCount.HasValue || itemChildrenCount == 0)
			{
				return null;
			}
			int num = 0;
			long num2 = dataSource.GetItemSizeValue(folder, num);
			for (int i = 1; i < itemChildrenCount; i++)
			{
				long itemSizeValue = dataSource.GetItemSizeValue(folder, i);
				if (itemSizeValue > num2)
				{
					num2 = itemSizeValue;
					num = i;
				}
			}
			return num;
		}
	}
}
