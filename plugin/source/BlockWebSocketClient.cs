using System;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.JsonRpc.Modules.Eth;
using Nethermind.Logging;
using Nethermind.Serialization.Json;

namespace Zoltu.Nethermind.Plugin.WebSocketPush
{
	public sealed class BlockWebSocketClient : WebSocketClient
	{
		private readonly IJsonSerializer jsonSerializer;
		private readonly ISpecProvider specProvider;

		public BlockWebSocketClient(ILogManager logManager, ILogger logger, IJsonSerializer jsonSerializer, ISpecProvider specProvider, IWebSocketPushConfig config, WebSocket webSocket, String id, String client) : base(logManager, jsonSerializer, logger, config, webSocket, id, client) => (this.jsonSerializer, this.specProvider) = (jsonSerializer, specProvider);

		public async Task Send(Block block)
		{
			var transactionAsString = this.jsonSerializer.Serialize(new BlockForRpc(block, true, this.specProvider));
			await this.SendRawAsync(transactionAsString);
		}
	}
}
