using System;
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
		private readonly IJsonSerializer jsonSerializer;

		public BlockWebSocketClient(ILogger logger, IJsonSerializer jsonSerializer, IWebSocketPushConfig config, WebSocket webSocket, String id, String client) : base(logger, config, webSocket, id, client)
		{
			this.jsonSerializer = jsonSerializer;
		}

		public async Task Send(Block block)
		{
			var transactionAsString = this.jsonSerializer.Serialize(new BlockForRpc(block, true, null));
			await this.SendRawAsync(transactionAsString);
		}
	}
}
