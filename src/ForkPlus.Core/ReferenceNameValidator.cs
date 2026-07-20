namespace ForkPlus
{
	public static class ReferenceNameValidator
	{
		[Null]
		public static string Validate(string referenceName)
		{
			string text = EndsWithDot(referenceName);
			if (text != null)
			{
				return text;
			}
			string text2 = HasTwoConsecutiveDots(referenceName);
			if (text2 != null)
			{
				return text2;
			}
			string text3 = HasControlASCIICharacters(referenceName);
			if (text3 != null)
			{
				return text3;
			}
			string text4 = HasIllegalCharacters(referenceName);
			if (text4 != null)
			{
				return text4;
			}
			string text5 = HasIllegalSequence(referenceName);
			if (text5 != null)
			{
				return text5;
			}
			string text6 = HasBackslash(referenceName);
			if (text6 != null)
			{
				return text6;
			}
			string text7 = IsAtSymbol(referenceName);
			if (text7 != null)
			{
				return text7;
			}
			string text8 = StartsWithSlash(referenceName);
			if (text8 != null)
			{
				return text8;
			}
			string text9 = HasSlashAndDot(referenceName);
			if (text9 != null)
			{
				return text9;
			}
			string text10 = HasSlashAndEndsWithDotLock(referenceName);
			if (text10 != null)
			{
				return text10;
			}
			string text11 = HasTwoConsecutiveSlashes(referenceName);
			if (text11 != null)
			{
				return text11;
			}
			return null;
		}

		[Null]
		public static string ValidateGitFlow(string referenceName)
		{
			string text = EndsWithDot(referenceName);
			if (text != null)
			{
				return text;
			}
			string text2 = HasTwoConsecutiveDots(referenceName);
			if (text2 != null)
			{
				return text2;
			}
			string text3 = HasControlASCIICharacters(referenceName);
			if (text3 != null)
			{
				return text3;
			}
			string text4 = HasIllegalCharacters(referenceName);
			if (text4 != null)
			{
				return text4;
			}
			string text5 = HasIllegalSequence(referenceName);
			if (text5 != null)
			{
				return text5;
			}
			string text6 = HasBackslash(referenceName);
			if (text6 != null)
			{
				return text6;
			}
			string text7 = IsAtSymbol(referenceName);
			if (text7 != null)
			{
				return text7;
			}
			string text8 = StartsWithSlash(referenceName);
			if (text8 != null)
			{
				return text8;
			}
			string text9 = HasSlashAndDot(referenceName);
			if (text9 != null)
			{
				return text9;
			}
			string text10 = StartsWithDot(referenceName);
			if (text10 != null)
			{
				return text10;
			}
			string text11 = EndsWithDotLock(referenceName);
			if (text11 != null)
			{
				return text11;
			}
			string text12 = HasTwoConsecutiveSlashes(referenceName);
			if (text12 != null)
			{
				return text12;
			}
			return null;
		}

		[Null]
		private static string HasTwoConsecutiveDots(string reference)
		{
			if (reference.Contains(".."))
			{
				return "Name cannot contain '..'";
			}
			return null;
		}

		[Null]
		private static string HasTwoConsecutiveSlashes(string reference)
		{
			if (reference.Contains("//"))
			{
				return "Name cannot contain multiple consecutive '/'";
			}
			return null;
		}

		[Null]
		private static string HasControlASCIICharacters(string reference)
		{
			if (reference.Contains("~") || reference.Contains("^") || reference.Contains(":"))
			{
				return "Name cannot contain ASCII control characters '~', '^' or ':'";
			}
			return null;
		}

		[Null]
		private static string HasIllegalCharacters(string reference)
		{
			if (reference.Contains("?") || reference.Contains("*") || reference.Contains("["))
			{
				return "Name cannot contain '?', '*' or '['";
			}
			return null;
		}

		[Null]
		private static string HasIllegalSequence(string reference)
		{
			if (reference.Contains("@{"))
			{
				return "Name cannot contain '@{'";
			}
			return null;
		}

		[Null]
		private static string HasBackslash(string reference)
		{
			if (reference.Contains("\\"))
			{
				return "Name cannot contain '\\'";
			}
			return null;
		}

		[Null]
		private static string IsAtSymbol(string reference)
		{
			if (reference == "@")
			{
				return "Name cannot be the single '@' character";
			}
			return null;
		}

		[Null]
		private static string StartsWithSlash(string reference)
		{
			if (reference.StartsWith("/"))
			{
				return "Name cannot begin with '/'";
			}
			return null;
		}

		[Null]
		private static string HasSlashAndDot(string reference)
		{
			if (reference.Contains("/."))
			{
				return "Name cannot contain '/.'";
			}
			return null;
		}

		[Null]
		private static string HasSlashAndEndsWithDotLock(string reference)
		{
			if (reference.Contains("/") && reference.EndsWith(".lock"))
			{
				return "Name cannot contain '/' and end with '.lock'";
			}
			return null;
		}

		[Null]
		private static string EndsWithDot(string reference)
		{
			if (reference.EndsWith("."))
			{
				return "Name cannot end with '.'";
			}
			return null;
		}

		[Null]
		private static string EndsWithDotLock(string reference)
		{
			if (reference.EndsWith(".lock"))
			{
				return "Name cannot end with '.lock'";
			}
			return null;
		}

		[Null]
		private static string StartsWithDot(string reference)
		{
			if (reference.StartsWith("."))
			{
				return "Name cannot start with '.'";
			}
			return null;
		}
	}
}
