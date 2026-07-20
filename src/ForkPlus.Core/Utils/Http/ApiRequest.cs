using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Utils.Http
{
	[DebuggerDisplay("{_result}")]
	public class ApiRequest
	{
		private readonly StringBuilder _path = new StringBuilder(256);

		private readonly List<KeyValuePair<string, string>> _parameters = new List<KeyValuePair<string, string>>();

		public KeyValuePair<string, string>[] Parameters => _parameters.ToArray();

		[Null]
		public JToken Json { get; private set; }

		public HttpMethod HttpMethod { get; }

		public string Slug
		{
			get
			{
				StringBuilder stringBuilder = new StringBuilder(256);
				stringBuilder.Append(_path);
				for (int i = 0; i < _parameters.Count; i++)
				{
					if (i == 0)
					{
						stringBuilder.Append("?");
					}
					else
					{
						stringBuilder.Append("&");
					}
					stringBuilder.Append(_parameters[i].Key);
					stringBuilder.Append("=");
					stringBuilder.Append(_parameters[i].Value);
				}
				return stringBuilder.ToString();
			}
		}

		public ApiRequest(string path)
			: this(HttpMethod.Get, path)
		{
		}

		public ApiRequest(HttpMethod httpMethod, string path)
		{
			HttpMethod = httpMethod;
			_path.Append(path);
		}

		public ApiRequest(string path, string path1)
			: this(HttpMethod.Get, path, path1)
		{
		}

		public ApiRequest(HttpMethod httpMethod, string path, string path1)
		{
			HttpMethod = httpMethod;
			_path.Append(path);
			_path.Append("/");
			_path.Append(path1);
		}

		public ApiRequest(string path, string path1, string path2)
			: this(HttpMethod.Get, path, path1, path2)
		{
		}

		public ApiRequest(HttpMethod httpMethod, string path, string path1, string path2)
		{
			HttpMethod = httpMethod;
			_path.Append(path);
			_path.Append("/");
			_path.Append(path1);
			_path.Append("/");
			_path.Append(path2);
		}

		public void AddParameter(string parameter, string value)
		{
			_parameters.Add(new KeyValuePair<string, string>(parameter, value));
		}

		public void AddParameter(string parameter, int value)
		{
			_parameters.Add(new KeyValuePair<string, string>(parameter, value.ToString()));
		}

		public void SetJson(JToken json)
		{
			Json = json;
		}
	}
}
