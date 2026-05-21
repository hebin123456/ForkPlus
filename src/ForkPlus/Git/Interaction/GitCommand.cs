using System.Collections.Generic;
using System.Text;

namespace ForkPlus.Git.Interaction
{
	public class GitCommand
	{
		private List<string> _args = new List<string>();

		private readonly StringBuilder _buffer = new StringBuilder(512);

		private bool _isDirty = true;

		private string _argumentsString;

		public string ArgumentsString
		{
			get
			{
				if (_isDirty)
				{
					_argumentsString = _buffer.ToString();
					_isDirty = false;
				}
				return _argumentsString;
			}
		}

		public bool IsEmpty => _buffer.Length == 0;

		public GitCommand()
		{
		}

		public GitCommand(params string[] arguments)
		{
			AddRange(arguments);
		}

		public GitCommand(string[] configParameters, params string[] arguments)
		{
			AddRange(configParameters);
			AddRange(arguments);
		}

		public GitCommand(string arg1)
		{
			Add(arg1);
		}

		public GitCommand(string arg1, string arg2)
		{
			Add(arg1);
			Add(arg2);
		}

		public GitCommand(string arg1, string arg2, string arg3)
		{
			Add(arg1);
			Add(arg2);
			Add(arg3);
		}

		public GitCommand(string arg1, string arg2, string arg3, string arg4)
		{
			Add(arg1);
			Add(arg2);
			Add(arg3);
			Add(arg4);
		}

		public GitCommand(string arg1, string arg2, string arg3, string arg4, string arg5)
		{
			Add(arg1);
			Add(arg2);
			Add(arg3);
			Add(arg4);
			Add(arg5);
		}

		public void AddRange(params string[] arguments)
		{
			for (int i = 0; i < arguments.Length; i++)
			{
				Add(arguments[i]);
			}
		}

		public void Add(string arg1, string arg2)
		{
			Add(arg1);
			Add(arg2);
		}

		public void Add(string argument)
		{
			_args.Add(argument);
			if (_buffer.Length > 0)
			{
				_buffer.Append(" ");
			}
			_buffer.Append(argument);
			_isDirty = true;
		}

		public bool CheckLimit(string argument)
		{
			return _buffer.Length + 1 + argument.Length < Consts.Env.ArgumentLengthLimit;
		}

		public string[] ToArray()
		{
			return _args.ToArray();
		}
	}
}
