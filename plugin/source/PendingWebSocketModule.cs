using System;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Logging;
using Nethermind.Mev.Execution;
using Nethermind.Serialization.Json;

namespace Zoltu.Nethermind.Plugin.WebSocketPush
{
	public sealed class PendingWebSocketModule : WebSocketModule<PendingWebSocketClient>
	{
		public override String Name => "pending";
		private readonly ITracerFactory tracerFactory;
		private readonly IBlockTree blockTree;

		public PendingWebSocketModule(ILogger logger, IJsonSerializer jsonSerializer, IWebSocketPushConfig config, ITracerFactory tracerFactory, IBlockTree blockTree) : base(logger, jsonSerializer, config) => (this.tracerFactory, this.blockTree) = (tracerFactory, blockTree);

		protected override PendingWebSocketClient Create(ILogger logger, IJsonSerializer jsonSerializer, IWebSocketPushConfig config, WebSocket webSocket, String id, String client) => new(logger, config, webSocket, id, client, jsonSerializer, this.tracerFactory, this.blockTree);

		public Task OnNewPending(Transaction transaction) => Task.WhenAll(this.clients.Values.Select(client => client.OnNewPending(transaction)));
	}
}
