using System;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Nethermind.Logging;

namespace Zoltu.Nethermind.Plugin.WebSocketPush
{
	public sealed class PendingWebSocketClient : WebSocketClient
	{
		public TraceLevels TraceLevel { get; private set; } = TraceLevels.None;

		public PendingWebSocketClient(ILogger logger, IWebSocketPushConfig config, WebSocket webSocket, String id, String client) : base(logger, config, webSocket, id, client) { }

		public override async Task ReceiveAsync(Memory<Byte> data)
		{
			try
			{
				var dataString = System.Text.Encoding.UTF8.GetString(data.Span);
				this.TraceLevel = Enum.Parse<TraceLevels>(dataString, true);
				this.logger.Info($"Pending transaction tracing level set to {this.TraceLevel}");
				await Task.CompletedTask;
			}
			catch (Exception exception)
			{
				await this.SendRawAsync($"Exception occurred while processing request: {exception.Message}");
				this.logger.Info($"Exception occurred while processing Pending WebSocket request:");
				// optimistically try to log the failing incoming message as a string, if it fails move on
				try { this.logger.Info(System.Text.Encoding.UTF8.GetString(data.Span)); } catch { }
				this.logger.Info(exception.Message);
				this.logger.Info(exception.StackTrace);
			}
		}

		public enum TraceLevels { None, Events, Actions };
	}
}
