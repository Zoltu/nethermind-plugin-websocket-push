using System;
using System.Threading.Tasks;
using Nethermind.Api;
using Nethermind.Api.Extensions;
using Nethermind.Blockchain.Processing;
using Nethermind.JsonRpc;
using Nethermind.Mev.Execution;

namespace Zoltu.Nethermind.Plugin.WebSocketPush
{
	public sealed class Plugin : DisposeAsyncOnce, INethermindPlugin
	{
		public String Name => "WebSocketPush";
		public String Description => "Pushes all new pending transactions to any connected WebSocket clients.";
		public String Author => "Micah";
		private INethermindApi? nethermindApi;
		private PendingWebSocketModule? pendingWebSocketModule;
		private BlockWebSocketModule? blockWebSocketModule;
		private Boolean initialized;

		public async Task Init(INethermindApi nethermindApi)
		{
			this.nethermindApi = nethermindApi;
			await Task.CompletedTask;
		}

		public async Task InitNetworkProtocol()
		{
			if (this.initialized) throw new Exception($"InitNetworkProtocol called twice on {this.Name} plugin.");
			this.initialized = true;
			if (this.nethermindApi == null) throw new Exception($"InitNetworkProtocol called on {this.Name} plugin before Init was called.");
			var transactionProcessor = this.nethermindApi.TransactionProcessor ?? throw new Exception($"NethermindApi.TransactionProcessor was null during {this.Name} plugin initialization.");
			var txPool = this.nethermindApi.TxPool ?? throw new Exception($"NethermindApi.TxPool was null during {this.Name} plugin initialization.");
			var dbProvider = this.nethermindApi.DbProvider ?? throw new Exception($"NethermindApi.DbProvider was null during {this.Name} plugin initialization.");
			var blockTree = this.nethermindApi.BlockTree ?? throw new Exception($"NethermindApi.BlockTree was null during {this.Name} plugin initialization.");
			var readOnlyTrieStore = this.nethermindApi.ReadOnlyTrieStore ?? throw new Exception($"NethermindApi.ReadOnlyTrieStore was null during {this.Name} plugin initialization.");
			var blockPreprocessor = this.nethermindApi.BlockPreprocessor ?? throw new Exception($"NethermindApi.BlockPreprocessor was null during {this.Name} plugin initialization.");
			var specProvider = this.nethermindApi.SpecProvider ?? throw new Exception($"NethermindApi.SpecProvider was null during {this.Name} plugin initialization.");
			var logManager = this.nethermindApi.LogManager ?? throw new Exception($"NethermindApi.LogManager was null during {this.Name} plugin initialization.");

			var logger = this.nethermindApi.LogManager.GetClassLogger();
			var config = this.nethermindApi.Config<IWebSocketPushConfig>();
			if (!config.PendingEnabled && !config.BlockEnabled)
			{
				logger.Warn($"{this.Name}.PendingEnabled and {this.Name}.BlockEnabled configuration variables set to false, halting initialization of {this.Name} plugin.");
				return;
			}
			var initConfig = this.nethermindApi.Config<IInitConfig>();
			if (!initConfig.WebSocketsEnabled)
			{
				logger.Warn($"Init.WebSocketsEnabled configuration variable set to false, halting initialization of {this.Name} plugin.");
				return;
			}
			var jsonRpcConfig = this.nethermindApi.Config<IJsonRpcConfig>();
			if (!jsonRpcConfig.Enabled)
			{
				logger.Warn($"JsonRpc.Enabled configuration variable set to false, halting initialization of {this.Name} plugin.");
				return;
			}
			var jsonSerializer = this.nethermindApi.EthereumJsonSerializer;
			if (config.PendingEnabled)
			{
				var tracerFactory = new TracerFactory(dbProvider, blockTree, readOnlyTrieStore, blockPreprocessor, specProvider, logManager, ProcessingOptions.ProducingBlock | ProcessingOptions.IgnoreParentNotOnMainChain);
				this.pendingWebSocketModule = new PendingWebSocketModule(logger, jsonSerializer, config, tracerFactory, this.nethermindApi.BlockTree);
				this.nethermindApi.WebSocketsManager.AddModule(this.pendingWebSocketModule);
				this.nethermindApi.TxPool.NewPending += (_, eventArgs) => Task.Run(async () =>
				{
					try
					{
						await this.pendingWebSocketModule.OnNewPending(eventArgs.Transaction);
					}
					catch (Exception exception)
					{
						logger.Error($"Plugin failed to process NewPending.", exception);
					}
				});
				logger.Info($"Subscribe to pending transactions by connecting to ws://{jsonRpcConfig.Host}:{jsonRpcConfig.WebSocketsPort}/{this.pendingWebSocketModule.Name}");
			}
			if (config.BlockEnabled)
			{
				this.blockWebSocketModule = new BlockWebSocketModule(logger, jsonSerializer, specProvider, config);
				this.nethermindApi.WebSocketsManager.AddModule(this.blockWebSocketModule);
				// TODO: see BlockchainProcessor.cs:201 (do we need to do this?) and BlockchainProcessor.cs:269
				// TODO: use a readonly blockchain processor
				// TODO: be careful to not collide/race with actual block processor which may be building caches while processing this block in parallel
				// _nethermindApi.BlockTree.NewBestSuggestedBlock += (_, eventArgs) => _blockWebSocketModule.ProcessBlockWithTracer(eventArgs.Block);
				this.nethermindApi.BlockTree.NewHeadBlock += (_, eventArgs) => Task.Run(() =>
				{
					try
					{
						_ = this.blockWebSocketModule.Send(eventArgs.Block!);
					}
					catch (Exception exception)
					{
						logger.Error($"Plugin failed to process NewHeadBlock.", exception);
					}
				});
				logger.Info($"Subscribe to new blocks by connecting to ws://{jsonRpcConfig.Host}:{jsonRpcConfig.WebSocketsPort}/{this.blockWebSocketModule.Name}");
			}
			await Task.CompletedTask;
		}

		public Task InitRpcModules() => Task.CompletedTask;

		protected override async ValueTask DisposeOnce()
		{
			if (this.pendingWebSocketModule != null)
			{
				await this.pendingWebSocketModule.DisposeAsync();
			}
		}
	}
}
