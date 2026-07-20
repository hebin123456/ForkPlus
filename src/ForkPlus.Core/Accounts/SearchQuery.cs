namespace ForkPlus.Accounts
{
	public class SearchQuery
	{
		public abstract class Parameter
		{
			public abstract string Value { get; }
		}

		public class Assignee : Parameter
		{
			public override string Value { get; }

			[Null]
			public static Parameter TryCreate(string parameterName, string parameterValue)
			{
				if (parameterName == "assignee")
				{
					return new Assignee(parameterValue);
				}
				return null;
			}

			public Assignee(string value)
			{
				Value = value;
			}
		}

		public class Author : Parameter
		{
			public override string Value { get; }

			[Null]
			public static Parameter TryCreate(string parameterName, string parameterValue)
			{
				if (parameterName == "author")
				{
					return new Author(parameterValue);
				}
				return null;
			}

			public Author(string value)
			{
				Value = value;
			}
		}

		public class Milestone : Parameter
		{
			public override string Value { get; }

			[Null]
			public static Parameter TryCreate(string parameterName, string parameterValue)
			{
				if (parameterName == "milestone")
				{
					return new Milestone(parameterValue);
				}
				return null;
			}

			public Milestone(string value)
			{
				Value = value;
			}
		}

		public class SearchString : Parameter
		{
			public override string Value { get; }

			public SearchString(string value)
			{
				Value = value;
			}
		}

		public Parameter[] Parameters { get; }

		public SearchQuery(Parameter[] parameters)
		{
			Parameters = parameters;
		}
	}
}
