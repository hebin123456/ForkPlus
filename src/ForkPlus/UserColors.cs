using System.Collections.Generic;

namespace ForkPlus
{
	public class UserColors
	{
		public static readonly UserColors Empty = new UserColors(new Dictionary<string, byte>());

		private readonly Dictionary<string, byte> _colorMapping;

		public UserColors(Dictionary<string, byte> colorMapping)
		{
			_colorMapping = colorMapping;
		}

		public byte GetColorId(string email)
		{
			if (_colorMapping.TryGetValue(email, out var value))
			{
				return value;
			}
			return 0;
		}

		public static bool AreEqual(UserColors current, UserColors old)
		{
			if (current._colorMapping.Count != old._colorMapping.Count)
			{
				Log.Debug("Detected usercolors change");
				return false;
			}
			foreach (string key in current._colorMapping.Keys)
			{
				if (!old._colorMapping.TryGetValue(key, out var value) || value != current._colorMapping[key])
				{
					Log.Debug("Detected usercolor change");
					return false;
				}
			}
			return true;
		}
	}
}
