using System;
using System.Collections.Generic;

namespace ForkPlus
{
	public static class FuzzySearch
	{
		public const double SCORE_MIN = double.MinValue;

		public const double SCORE_MAX = double.MaxValue;

		public const double SCORE_GAP_LEADING = -0.005;

		public const double SCORE_GAP_TRAILING = -0.005;

		public const double SCORE_GAP_INNER = -0.01;

		public const double SCORE_MATCH_CONSECUTIVE = 1.0;

		public const double SCORE_MATCH_SLASH = 0.9;

		public const double SCORE_MATCH_WORD = 0.8;

		public const double SCORE_MATCH_CAPITAL = 0.7;

		public const double SCORE_MATCH_DOT = 0.6;

		private static readonly int[] BonusIndex = CreateBonusIndex();

		private static readonly double[,] BonusStates = CreateBonusStates();

		public static IReadOnlyList<T> FuzzyFilter<T>(this IReadOnlyList<T> entries, string filterString, Func<T, string> primarySelector, Func<T, string> secondarySelector = null)
		{
			if (string.IsNullOrEmpty(filterString))
			{
				return entries;
			}
			List<KeyValuePair<double, T>> list = new List<KeyValuePair<double, T>>(entries.Count);
			foreach (T entry in entries)
			{
				string target = primarySelector(entry);
				double num = 0.0;
				if (target.HasFuzzyMatch(filterString))
				{
					double num2 = target.Match(filterString);
					num += num2;
				}
				if (secondarySelector != null)
				{
					string target2 = secondarySelector(entry);
					if (target2.HasFuzzyMatch(filterString))
					{
						double num3 = target2.Match(filterString);
						num += num3;
					}
				}
				if (num > 0.0)
				{
					list.Add(new KeyValuePair<double, T>(num, entry));
				}
			}
			list.Sort((KeyValuePair<double, T> x, KeyValuePair<double, T> y) => -1 * x.Key.CompareTo(y.Key));
			return list.Map((KeyValuePair<double, T> x) => x.Value);
		}

		public static bool HasFuzzyMatch(this string target, string substring)
		{
			int startIndex = 0;
			foreach (char c in substring)
			{
				char[] anyOf = new char[2]
				{
					c,
					char.ToUpper(c)
				};
				int num = target.IndexOfAny(anyOf, startIndex);
				if (num == -1)
				{
					return false;
				}
				startIndex = num + 1;
			}
			return true;
		}

		public static double Match(this string target, string substring)
		{
			return target.MatchPositions(substring, null);
		}

		public static double MatchPositions(this string haystack, string needle, int[] positions)
		{
			if (string.IsNullOrEmpty(needle))
			{
				return double.MinValue;
			}
			int length = needle.Length;
			int length2 = haystack.Length;
			if (length == length2)
			{
				if (positions != null)
				{
					for (int i = 0; i < length; i++)
					{
						positions[i] = i;
					}
				}
				return double.MaxValue;
			}
			if (length2 > 1024)
			{
				return double.MinValue;
			}
			string text = needle.ToLower();
			string text2 = haystack.ToLower();
			double[] array = new double[length2];
			double[,] array2 = new double[length, length2];
			double[,] array3 = new double[length, length2];
			PrecomputeBonus(haystack, array);
			for (int j = 0; j < length; j++)
			{
				double num = double.MinValue;
				double num2 = ((j == length - 1) ? (-0.005) : (-0.01));
				for (int k = 0; k < length2; k++)
				{
					if (text[j] == text2[k])
					{
						double num3 = double.MinValue;
						if (j == 0)
						{
							num3 = (double)k * -0.005 + array[k];
						}
						else if (k != 0)
						{
							num3 = Math.Max(array3[j - 1, k - 1] + array[k], array2[j - 1, k - 1] + 1.0);
						}
						array2[j, k] = num3;
						num = (array3[j, k] = Math.Max(num3, num + num2));
					}
					else
					{
						array2[j, k] = double.MinValue;
						num = (array3[j, k] = num + num2);
					}
				}
			}
			if (positions != null)
			{
				bool flag = false;
				int num4 = length - 1;
				int num5 = length2 - 1;
				while (num4 >= 0)
				{
					while (num5 >= 0)
					{
						if (array2[num4, num5] != double.MinValue && (flag || array2[num4, num5] == array3[num4, num5]))
						{
							flag = num4 > 0 && num5 > 0 && array3[num4, num5] == array2[num4 - 1, num5 - 1] + 1.0;
							positions[num4] = num5--;
							break;
						}
						num5--;
					}
					num4--;
				}
			}
			return array3[length - 1, length2 - 1];
		}

		private static void PrecomputeBonus(string haystack, double[] matchBonus)
		{
			int length = haystack.Length;
			char last_ch = '/';
			for (int i = 0; i < length; i++)
			{
				char c = haystack[i];
				matchBonus[i] = ComputeBonus(last_ch, c);
				last_ch = c;
			}
		}

		private static double ComputeBonus(char last_ch, char ch)
		{
			return BonusStates[BonusIndex[(byte)ch], (byte)last_ch];
		}

		private static int[] CreateBonusIndex()
		{
			int[] array = new int[256];
			for (char c = 'A'; c <= 'Z'; c = (char)(c + 1))
			{
				array[(uint)c] = 2;
			}
			for (char c2 = 'a'; c2 <= 'z'; c2 = (char)(c2 + 1))
			{
				array[(uint)c2] = 1;
			}
			for (char c3 = '0'; c3 <= '9'; c3 = (char)(c3 + 1))
			{
				array[(uint)c3] = 1;
			}
			return array;
		}

		private static double[,] CreateBonusStates()
		{
			double[,] array = new double[3, 256];
			array[1, 47] = 0.9;
			array[1, 45] = 0.8;
			array[1, 95] = 0.8;
			array[1, 32] = 0.8;
			array[1, 46] = 0.6;
			array[2, 47] = 0.9;
			array[2, 45] = 0.8;
			array[2, 95] = 0.8;
			array[2, 32] = 0.8;
			array[2, 46] = 0.6;
			for (char c = 'a'; c <= 'z'; c = (char)(c + 1))
			{
				array[2, (uint)c] = 0.7;
			}
			return array;
		}
	}
}
