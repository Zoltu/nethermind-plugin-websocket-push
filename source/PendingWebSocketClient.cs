using System;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Nethermind.Core;
using Nethermind.Logging;
using Nethermind.Serialization.Json;

namespace Zoltu.Nethermind.Plugin.WebSocketPush
{
	public sealed class PendingWebSocketClient : WebSocketClient
	{
		private readonly IJsonSerializer _jsonSerializer;

		public PendingWebSocketClient(ILogger logger, IJsonSerializer jsonSerializer, IWebSocketPushConfig config, WebSocket webSocket, String id, String client) : base(logger, config, webSocket, id, client)
		{
			_jsonSerializer = jsonSerializer;
		}

		public async Task Send(Transaction transaction)
		{
			var transactionAsString = _jsonSerializer.Serialize(transaction);
			await SendRawAsync(transactionAsString);
		}
	}
}
