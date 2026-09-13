using System;
using ForkPlus.AutoUpdater;
using Xunit;

namespace ForkPlus.AutoUpdaterTests
{
	/// <summary>
	/// 进度消息协议单元测试：锁定 download/phase/error 三类消息的构造与解析
	/// 语义。主程序侧（ForkPlus.AutoUpdateRunner 的 AutoUpdateProgress.Parse）
	/// 各持一份格式副本，本套用例与主程序侧用例共同锁定字段名/格式不漂移。
	/// </summary>
	public class UpdateMessageProtocolTests
	{
		[Fact]
		public void Download_ConstructsAndParses_RoundTrip()
		{
			string message = UpdateMessageProtocol.Download(1024L, 4096L);
			Assert.Equal("download:1024:4096", message);
			Assert.True(UpdateMessageProtocol.TryParseDownload(message, out long received, out long total));
			Assert.Equal(1024L, received);
			Assert.Equal(4096L, total);
		}

		[Theory]
		[InlineData("download:abc:100")]      // 非数字 received
		[InlineData("download:100:xyz")]      // 非数字 total
		[InlineData("download:100")]          // 段数不足
		[InlineData("download:100:200:300")]  // 段数过多
		[InlineData("phase:downloading")]     // 前缀不匹配
		[InlineData("")]                      // 空串
		[InlineData("download:-5:100")]       // 负数可解析（语义由调用方判断），仅锁格式合法路径外溢
		public void TryParseDownload_InvalidInput_ReturnsFalseOrParsed(string input)
		{
			bool parsed = UpdateMessageProtocol.TryParseDownload(input, out long received, out long total);
			if (input == "download:-5:100")
			{
				// 负数字节长度格式上合法（long.TryParse 通过）——解析层不做业务校验
				Assert.True(parsed);
				Assert.Equal(-5L, received);
			}
			else
			{
				Assert.False(parsed);
				Assert.Equal(0L, received);
				Assert.Equal(0L, total);
			}
		}

		[Fact]
		public void TryParseDownload_Null_ReturnsFalse()
		{
			Assert.False(UpdateMessageProtocol.TryParseDownload(null, out _, out _));
		}

		[Fact]
		public void Phase_ConstructsAndParses_RoundTrip()
		{
			string message = UpdateMessageProtocol.Phase(UpdateMessageProtocol.PhaseExtracting);
			Assert.Equal("phase:extracting", message);
			Assert.Equal("extracting", UpdateMessageProtocol.TryParsePhase(message));
		}

		[Theory]
		[InlineData("download:1:2")]
		[InlineData("error:boom")]
		[InlineData("phase:")]           // 空阶段名：前缀匹配即返回空串（调用方按未知阶段兜底）
		[InlineData("")]
		public void TryParsePhase_NonPhaseInput_ReturnsNullOrEmpty(string input)
		{
			string phase = UpdateMessageProtocol.TryParsePhase(input);
			if (input == "phase:")
			{
				Assert.Equal("", phase);
			}
			else
			{
				Assert.Null(phase);
			}
		}

		[Fact]
		public void TryParsePhase_Null_ReturnsNull()
		{
			Assert.Null(UpdateMessageProtocol.TryParsePhase(null));
		}

		[Fact]
		public void Error_ConstructsAndParses_RoundTrip()
		{
			string message = UpdateMessageProtocol.Error("network reset");
			Assert.Equal("error:network reset", message);
			Assert.Equal("network reset", UpdateMessageProtocol.TryParseError(message));
		}

		[Fact]
		public void Error_NewlinesAndNulls_AreSanitized()
		{
			Assert.Equal("error:a b c", UpdateMessageProtocol.Error("a\r\nb\nc"));
			Assert.Equal("error:", UpdateMessageProtocol.Error(null));
		}

		[Theory]
		[InlineData("phase:downloading")]
		[InlineData("download:1:2")]
		[InlineData("")]
		public void TryParseError_NonErrorInput_ReturnsNull(string input)
		{
			Assert.Null(UpdateMessageProtocol.TryParseError(input));
		}

		/// <summary>协议字段名/阶段名常量锁定：改名 = 破坏与主程序的管道兼容。</summary>
		[Fact]
		public void ProtocolConstants_AreLocked()
		{
			Assert.Equal("download:", UpdateMessageProtocol.DownloadPrefix);
			Assert.Equal("phase:", UpdateMessageProtocol.PhasePrefix);
			Assert.Equal("error:", UpdateMessageProtocol.ErrorPrefix);
			Assert.Equal("downloading", UpdateMessageProtocol.PhaseDownloading);
			Assert.Equal("extracting", UpdateMessageProtocol.PhaseExtracting);
			Assert.Equal("waiting-exit", UpdateMessageProtocol.PhaseWaitingExit);
			Assert.Equal("replacing", UpdateMessageProtocol.PhaseReplacing);
			Assert.Equal("restarting", UpdateMessageProtocol.PhaseRestarting);
			Assert.Equal("done", UpdateMessageProtocol.PhaseDone);
		}
	}
}
