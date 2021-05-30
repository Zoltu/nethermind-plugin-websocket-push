using System;
using System.Threading.Tasks;
using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.JsonRpc;

namespace Zoltu.Nethermind.Plugin.WebSocketPush
{
	public sealed class Plugin : DisposeAsyncOnce, INethermindPlugin
	{
		public String Name => "WebSocketPush";
		public String Description => "Pushes all new pending transactions to any connected WebSocket clients.";
		public String Author => "Micah";
		private INethermindApi? _nethermindApi;
		private PendingWebSocketModule? _pendingWebSocketModule;
		private BlockWebSocketModule? _blockWebSocketModule;
		private Boolean _initialized;

		public async Task Init(INethermindApi nethermindApi)
		{
			_nethermindApi = nethermindApi;
			await Task.CompletedTask;
		}

		public async Task InitNetworkProtocol()
		{
			if (_initialized) throw new Exception($"InitNetworkProtocol called twice on {Name} plugin.");
			_initialized = true;
			if (_nethermindApi == null) throw new Exception($"InitNetworkProtocol called on {Name} plugin before Init was called.");
			if (_nethermindApi.BlockTree == null) throw new Exception($"NethermindApi.BlockTree was null during {Name} plugin initialization.");
			if (_nethermindApi.TransactionProcessor == null) throw new Exception($"NethermindApi.TransactionProcessor was null during {Name} plugin initialization.");
			if (_nethermindApi.TxPool == null) throw new Exception($"NethermindApi.TxPool was null during {Name} plugin initialization.");
			if (_nethermindApi.BlockTree == null) throw new Exception($"NethermindApi.BlockTree was null during {Name} plugin initialization.");

			var logger = _nethermindApi.LogManager.GetClassLogger();
			var config = _nethermindApi.Config<IWebSocketPushConfig>();
			if (!config.PendingEnabled && !config.BlockEnabled)
			{
				logger.Warn($"{Name}.PendingEnabled and {Name}.BlockEnabled configuration variables set to false, halting initialization of {Name} plugin.");
				return;
			}
			var initConfig = _nethermindApi.Config<IInitConfig>();
			if (!initConfig.WebSocketsEnabled)
			{
				logger.Warn($"Init.WebSocketsEnabled configuration variable set to false, halting initialization of {Name} plugin.");
				return;
			}
			var jsonRpcConfig = _nethermindApi.Config<IJsonRpcConfig>();
			if (!jsonRpcConfig.Enabled)
			{
				logger.Warn($"JsonRpc.Enabled configuration variable set to false, halting initialization of {Name} plugin.");
				return;
			}
			var jsonSerializer = _nethermindApi.EthereumJsonSerializer;
			if (config.PendingEnabled)
			{
				_pendingWebSocketModule = new PendingWebSocketModule(logger, jsonSerializer, _nethermindApi.BlockTree, _nethermindApi.TransactionProcessor, config);
				_nethermindApi.WebSocketsManager.AddModule(_pendingWebSocketModule);
				_nethermindApi.TxPool.NewPending += (_, eventArgs) => _pendingWebSocketModule.Send(eventArgs.Transaction);
				logger.Info($"Subscribe to pending transactions by connecting to ws://{jsonRpcConfig.Host}:{jsonRpcConfig.WebSocketsPort}/{_pendingWebSocketModule.Name}");
			}
			if (config.BlockEnabled)
			{
				_blockWebSocketModule = new BlockWebSocketModule(logger, jsonSerializer, config);
				_nethermindApi.WebSocketsManager.AddModule(_blockWebSocketModule);
				// TODO: see BlockchainProcessor.cs:201 (do we need to do this?) and BlockchainProcessor.cs:269
				// TODO: use a readonly blockchain processor
				// TODO: be careful to not collide/race with actual block processor which may be building caches while processing this block in parallel
				// _nethermindApi.BlockTree.NewBestSuggestedBlock += (_, eventArgs) => _blockWebSocketModule.ProcessBlockWithTracer(eventArgs.Block);
				_nethermindApi.BlockTree.NewHeadBlock += (_, eventArgs) =>
				{
					// Nethermind gets into a bad state if the exception bubbles out of this handler, so we need to make sure to swallow it here
					try
					{
						_ = _blockWebSocketModule.Send(eventArgs.Block!);
					}
					catch (Exception exception)
					{
						logger.Error($"Plugin failed to process NewHeadBlock.", exception);
					}
				};
				logger.Info($"Subscribe to new blocks by connecting to ws://{jsonRpcConfig.Host}:{jsonRpcConfig.WebSocketsPort}/{_blockWebSocketModule.Name}");
			}
			await Task.CompletedTask;
		}

		public Task InitRpcModules() => Task.CompletedTask;

		protected override async ValueTask DisposeOnce()
		{
			if (_pendingWebSocketModule != null)
			{
				await _pendingWebSocketModule.DisposeAsync();
			}
		}
	}
}
