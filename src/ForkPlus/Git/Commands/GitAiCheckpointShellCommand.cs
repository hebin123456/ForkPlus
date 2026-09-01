using System;
using System.Collections.Generic;
using System.IO;
using ForkPlus.Git.Interaction;
using ForkPlus.Shell.Interaction;
using Newtonsoft.Json.Linq;

namespace ForkPlus.Git.Commands
{
	/// <summary>
	/// 把 ForkPlus 内置 AI 功能（AI 开发 / AI 代码审查）的修改上报给 git-ai，
	/// 让这些 AI 生成的代码进入 git-ai 的作者归属体系（refs/notes/ai），
	/// 之后可在 Blame 视图与统计页看到 ForkPlus AI 的行级归属。
	/// <para>
	/// 遵循 git-ai 官方 agent-v1 preset 协议（https://usegitai.com/docs/cli/add-your-agent）：
	/// </para>
	/// <list type="number">
	/// <item>AI 编辑文件<b>前</b>：上报 human 检查点，把上次 AI 插入之后的全部工作区改动标记为人类</item>
	/// <item>AI 编辑文件<b>后</b>：上报 ai_agent 检查点，携带对话 transcript、agent 名、模型与编辑的文件</item>
	/// </list>
	/// <para>
	/// 所有上报都是"尽力而为"：git-ai 未安装 / 版本过旧 / 上报失败时仅记日志并静默跳过，
	/// 绝不影响 AI 功能本身的文件修改流程。
	/// </para>
	/// </summary>
	public class GitAiCheckpointShellCommand
	{
		/// <summary>agent-v1 协议约定的 agent 名。统计页按智能体维度聚合时会显示为 ForkPlus。</summary>
		public const string AgentName = "ForkPlus";

		/// <summary>checkpoint 上报超时（毫秒）。checkpoint 是本地操作，正常亚秒完成；超时按失败处理。</summary>
		private const int TimeoutMilliseconds = 15000;

		/// <summary>单条 transcript 消息文本的最大长度，超出截断（防御性：避免超大对话把 JSON 撑爆）。</summary>
		private const int MaxTranscriptMessageLength = 64 * 1024;

		/// <summary>transcript 消息（user / assistant / tool_use 的统一载体）。</summary>
		public sealed class TranscriptMessage
		{
			/// <summary>user（用户输入）或 assistant（AI 回复）。</summary>
			public string Type { get; }

			/// <summary>消息文本。</summary>
			public string Text { get; }

			public TranscriptMessage(string type, string text)
			{
				Type = type;
				Text = text ?? "";
			}
		}

		/// <summary>
		/// AI 编辑文件前上报 human 检查点：把上次 AI 插入之后到当前时刻的工作区改动标记为人类编写。
		/// will_edit_filepaths 让 git-ai 把 diff 收窄到即将编辑的文件，大仓库下可提速 50-100 倍。
		/// </summary>
		/// <param name="gitModule">仓库模块。</param>
		/// <param name="gitAiPath">git-ai 可执行文件路径。</param>
		/// <param name="willEditFilepaths">即将被 AI 编辑的仓库相对路径（正斜杠分隔），空数组也可（全量 diff）。</param>
		/// <returns>true = 上报成功。</returns>
		public bool ReportHumanCheckpoint(GitModule gitModule, string gitAiPath, IEnumerable<string> willEditFilepaths)
		{
			JObject payload = new JObject();
			payload["type"] = "human";
			payload["repo_working_dir"] = gitModule.Path;
			JArray filepaths = new JArray();
			if (willEditFilepaths != null)
			{
				foreach (string filepath in willEditFilepaths)
				{
					if (!string.IsNullOrWhiteSpace(filepath))
					{
						filepaths.Add(NormalizeRepoPath(filepath));
					}
				}
			}
			payload["will_edit_filepaths"] = filepaths;
			return ExecuteCheckpoint(gitModule, gitAiPath, payload, "human");
		}

		/// <summary>
		/// AI 编辑文件后上报 ai_agent 检查点：把本次 AI 修改标记为 AI 生成，
		/// 携带对话 transcript（完整多轮历史）、agent 名、模型、会话 id 与实际编辑的文件。
		/// </summary>
		/// <param name="gitModule">仓库模块。</param>
		/// <param name="gitAiPath">git-ai 可执行文件路径。</param>
		/// <param name="model">本次使用的模型（如 gpt-4o）。</param>
		/// <param name="conversationId">会话 id（同一对话线程内保持不变，建议用 GUID）。</param>
		/// <param name="editedFilepaths">实际被 AI 修改的仓库相对路径（正斜杠分隔）。</param>
		/// <param name="transcript">对话消息（user/assistant），按时间顺序。</param>
		/// <returns>true = 上报成功。</returns>
		public bool ReportAiCheckpoint(GitModule gitModule, string gitAiPath, string model, string conversationId, IEnumerable<string> editedFilepaths, IEnumerable<TranscriptMessage> transcript)
		{
			JObject payload = new JObject();
			payload["type"] = "ai_agent";
			payload["repo_working_dir"] = gitModule.Path;
			payload["agent_name"] = AgentName;
			payload["model"] = string.IsNullOrWhiteSpace(model) ? "unknown" : model;
			payload["conversation_id"] = string.IsNullOrWhiteSpace(conversationId) ? Guid.NewGuid().ToString() : conversationId;
			JArray filepaths = new JArray();
			if (editedFilepaths != null)
			{
				foreach (string filepath in editedFilepaths)
				{
					if (!string.IsNullOrWhiteSpace(filepath))
					{
						filepaths.Add(NormalizeRepoPath(filepath));
					}
				}
			}
			payload["edited_filepaths"] = filepaths;
			JArray messages = new JArray();
			if (transcript != null)
			{
				string timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
				foreach (TranscriptMessage message in transcript)
				{
					if (message == null || string.IsNullOrWhiteSpace(message.Text))
					{
						continue;
					}
					JObject entry = new JObject();
					entry["type"] = message.Type == "assistant" ? "assistant" : "user";
					string text = message.Text;
					if (text.Length > MaxTranscriptMessageLength)
					{
						text = text.Substring(0, MaxTranscriptMessageLength);
					}
					entry["text"] = text;
					entry["timestamp"] = timestamp;
					messages.Add(entry);
				}
			}
			JObject transcriptObject = new JObject();
			transcriptObject["messages"] = messages;
			payload["transcript"] = transcriptObject;
			return ExecuteCheckpoint(gitModule, gitAiPath, payload, "ai_agent");
		}

		/// <summary>执行 checkpoint：JSON 走 stdin，命令行固定为 checkpoint agent-v1 --hook-input stdin。</summary>
		private static bool ExecuteCheckpoint(GitModule gitModule, string gitAiPath, JObject payload, string checkpointType)
		{
			if (string.IsNullOrWhiteSpace(gitAiPath) || !File.Exists(gitAiPath))
			{
				return false;
			}
			try
			{
				ShellRequest request = new ShellRequest(gitModule.Path, gitAiPath, new string[4] { "checkpoint", "agent-v1", "--hook-input", "stdin" })
				{
					StandardInput = payload.ToString(Newtonsoft.Json.Formatting.None)
				};
				GitRequestResult result = request.Execute(TimeoutMilliseconds);
				if (!result.Success)
				{
					// 常见失败：git-ai 版本过旧不认识 agent-v1 preset。静默降级，不打扰用户。
					Log.Info("git-ai " + checkpointType + " checkpoint failed (ignored): " + result.Stderr.Trim());
					return false;
				}
				return true;
			}
			catch (Exception ex)
			{
				Log.Error("Failed to report git-ai " + checkpointType + " checkpoint", ex);
				return false;
			}
		}

		/// <summary>把仓库相对路径规范成正斜杠（git-ai 侧按 POSIX 风格路径匹配）。</summary>
		private static string NormalizeRepoPath(string path)
		{
			return (path ?? "").Replace('\\', '/');
		}
	}
}
