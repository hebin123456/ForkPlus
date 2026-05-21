using System;
using ForkPlus.Git;

namespace ForkPlus.UI
{
	public class StashReferenceViewModel : ReferenceViewModel
	{
		public StashRevision _stash;

		public override Reference Reference
		{
			get
			{
				throw new NotImplementedException();
			}
		}

		public string ReflogName => _stash.ReflogName;

		public StashReferenceViewModel(int graphColumn, StashRevision stash)
			: base(graphColumn)
		{
			_stash = stash;
		}
	}
}
