namespace ForkPlus.UI.Controls
{
	public interface ITreemapDataSource
	{
		object GetRootItems();

		object GetItemChildren(object array, int index);

		int? GetItemChildrenCount(object array, int? index);

		long GetItemSizeValue(object array, int index);
	}
}
