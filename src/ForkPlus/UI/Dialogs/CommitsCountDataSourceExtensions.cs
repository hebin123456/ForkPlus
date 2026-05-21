using System.Text;
using ForkPlus.UI.Controls;

namespace ForkPlus.UI.Dialogs
{
	public static class CommitsCountDataSourceExtensions
	{
		public static string GetPath(this CommitsCountDataSource dataSource, Treemap.IndexPath indexPath)
		{
			object obj = dataSource.GetRootItems();
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < indexPath.Count; i++)
			{
				int num = indexPath[i];
				if (i == 0)
				{
					obj = dataSource.GetItemChildren(obj, num);
					continue;
				}
				CommitsCountDataSource.Item item = (obj as CommitsCountDataSource.Item[])[num];
				stringBuilder.Append(item.Title);
				if (dataSource.GetItemChildrenCount(obj, num).HasValue)
				{
					stringBuilder.Append("/");
					obj = dataSource.GetItemChildren(obj, num);
				}
			}
			return stringBuilder.ToString();
		}
	}
}
