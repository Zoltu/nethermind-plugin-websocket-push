using System;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Nethermind.Core;
using Nethermind.Core.Specs;
using Nethermind.Logging;
using Nethermind.Serialization.Json;

namespace Zoltu.Nethermind.Plugin.WebSocketPush
{
	public sealed class BlockWebSocketModule : WebSocketModule<BlockWebSocketClient>
	{
		public override String Name => "block";

		private readonly ISpecProvider specProvider;

		public BlockWebSocketModule(ILogger logger, IJsonSerializer jsonSerializer, ISpecProvider specProvider, IWebSocketPushConfig config) : base(logger, jsonSerializer, config) => this.specProvider = specProvider;

		protected override BlockWebSocketClient Create(ILogger logger, IJsonSerializer jsonSerializer, IWebSocketPushConfig config, WebSocket webSocket, String id, String client) => new(logger, jsonSerializer, this.specProvider, config, webSocket, id, client);

		public Task Send(Block block) => Task.WhenAll(this.Clients.Values.Select(client => client.Send(block)));
	}
}
