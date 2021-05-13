using System;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Nethermind.Core;
using Nethermind.JsonRpc.Modules.Eth;
using Nethermind.Logging;
using Nethermind.Serialization.Json;

namespace Zoltu.Nethermind.Plugin.WebSocketPush
{
	public sealed class BlockWebSocketClient : WebSocketClient
	{
		private readonly IJsonSerializer _jsonSerializer;

		public BlockWebSocketClient(ILogger logger, IJsonSerializer jsonSerializer, IWebSocketPushConfig config, WebSocket webSocket, String id, String client) : base(logger, config, webSocket, id, client)
		{
			_jsonSerializer = jsonSerializer;
		}

		public async Task Send(Block block)
		{
			var transactionAsString = _jsonSerializer.Serialize(new BlockForRpc(block, true));
			await SendRawAsync(transactionAsString);
		}
	}
}
