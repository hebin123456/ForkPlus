using System;

namespace ForkPlus.Git.Commands
{
	internal struct AnnotatedTagDetails
	{
		public string Name { get; }

		public UserIdentity Tagger { get; }

		public DateTime TaggerDate { get; }

		public string Message { get; }

		public AnnotatedTagDetails(string name, UserIdentity tagger, DateTime taggerDate, string message)
		{
			Name = name;
			Tagger = tagger;
			TaggerDate = taggerDate;
			Message = message;
		}
	}
}
