using System;
using System.IO;
using System.Threading.Tasks;
using ForkPlus.Jobs;

namespace ForkPlus.Git.Interaction
{
	internal class TailReader
	{
		private string _filePath;

		public TailReader(string filePath)
		{
			_filePath = filePath;
		}

		public void Tail(Action<string> callback, CancelHandler cancel)
		{
			Task.Run(async delegate
			{
				using StreamReader reader = new StreamReader(new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
				long lastMaxOffset = reader.BaseStream.Length;
				while (!cancel.IsCanceled)
				{
					if (reader.BaseStream.Length == lastMaxOffset)
					{
						await Task.Delay(200);
					}
					else
					{
						reader.BaseStream.Seek(lastMaxOffset, SeekOrigin.Begin);
						string obj;
						while ((obj = reader.ReadLine()) != null)
						{
							callback(obj);
						}
						lastMaxOffset = reader.BaseStream.Position;
						await Task.Delay(200);
					}
				}
			});
		}
	}
}
