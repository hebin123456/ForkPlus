using System;
using System.Collections.Generic;
using System.Text;

namespace ForkPlus.Accounts
{
	public static class SearchQueryParser
	{
		private class Token
		{
			public string Value { get; }

			public Token(string value)
			{
				Value = value;
			}
		}

		private class ParameterToken : Token
		{
			public string Name { get; }

			public ParameterToken(string name, string value)
				: base(value)
			{
				Name = name;
			}
		}

		public static SearchQuery Parse(string input, Func<string, string, SearchQuery.Parameter>[] allowedQueryParameters)
		{
			List<SearchQuery.Parameter> list = new List<SearchQuery.Parameter>();
			for (int i = 0; i < input.Length; i++)
			{
				Token token = ParseToken(input, ref i);
				if (token is ParameterToken parameterToken)
				{
					SearchQuery.Parameter parameter = CreateParameter(parameterToken.Name, parameterToken.Value, allowedQueryParameters);
					if (parameter != null)
					{
						list.Add(parameter);
						continue;
					}
					Log.Warn("Unsupported search parameter '" + parameterToken.Name + "'");
					list.Add(new SearchQuery.SearchString(parameterToken.Name + ":" + parameterToken.Value));
				}
				else if (token != null)
				{
					Token token2 = token;
					list.Add(new SearchQuery.SearchString(token2.Value));
				}
			}
			StringBuilder stringBuilder = new StringBuilder();
			for (int j = 0; j < list.Count; j++)
			{
				if (list[j] is SearchQuery.SearchString searchString)
				{
					if (stringBuilder.Length > 0)
					{
						stringBuilder.Append(" ");
					}
					stringBuilder.Append(searchString.Value);
					list.RemoveAt(j);
					j--;
				}
			}
			if (stringBuilder.Length > 0)
			{
				list.Add(new SearchQuery.SearchString(stringBuilder.ToString()));
			}
			return new SearchQuery(list.ToArray());
		}

		[Null]
		private static Token ParseToken(string input, ref int cursor)
		{
			string text = null;
			int num = cursor;
			while (cursor < input.Length)
			{
				if (input[cursor] == ' ')
				{
					if (text == null)
					{
						return new Token(input.Substring(num, cursor - num));
					}
					return new ParameterToken(text, input.Substring(num, cursor - num));
				}
				if (input[cursor] == ':' && text == null)
				{
					text = input.Substring(num, cursor - num);
					num = cursor + 1;
				}
				cursor++;
			}
			if (num < cursor)
			{
				if (text == null)
				{
					return new Token(input.Substring(num, cursor - num));
				}
				return new ParameterToken(text, input.Substring(num, cursor - num));
			}
			return null;
		}

		[Null]
		private static SearchQuery.Parameter CreateParameter(string parameterName, string parameterValue, Func<string, string, SearchQuery.Parameter>[] allowedParameters)
		{
			for (int i = 0; i < allowedParameters.Length; i++)
			{
				SearchQuery.Parameter parameter = allowedParameters[i](parameterName, parameterValue);
				if (parameter != null)
				{
					return parameter;
				}
			}
			return null;
		}
	}
}
