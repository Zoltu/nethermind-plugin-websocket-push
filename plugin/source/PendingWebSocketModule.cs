using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Int256;
using Nethermind.JsonRpc.Data;
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

		public PendingWebSocketModule(ILogManager logManager, ILogger logger, IJsonSerializer jsonSerializer, IWebSocketPushConfig config, ITracerFactory tracerFactory, IBlockTree blockTree) : base(logManager, logger, jsonSerializer, config) => (this.tracerFactory, this.blockTree) = (tracerFactory, blockTree);

		protected override PendingWebSocketClient Create(ILogManager logManager, ILogger logger, IJsonSerializer jsonSerializer, IWebSocketPushConfig config, WebSocket webSocket, String id, String client) => new(logManager, jsonSerializer, logger, config, webSocket, id, client);

		public async Task OnNewPending(Transaction transaction)
		{
			if (this.Clients.IsEmpty) return;

			if (transaction.GasLimit <= 21000)
			{
				await this.SendSimpleTransactionToAllClients(transaction);
				return;
			}

			if (this.Clients.All(pair => pair.Value.TraceLevel == PendingWebSocketClient.TraceLevels.None))
			{
				await this.SendSimpleTransactionToAllClients(transaction);
				return;
			}

			var tracer = this.TraceTransaction(transaction, this.Clients.Any(pair => pair.Value.TraceLevel == PendingWebSocketClient.TraceLevels.Actions));
			if (tracer == null)
			{
				this.Logger.Error($"Unreachable code reached.  Pending transaction was traced as part of a block, but no tracer was created.");
				await this.SendSimpleTransactionToAllClients(transaction);
				return;
			}

			String? noTracing = null;
			String? eventTracing = null;
			String? actionTracing = null;

			await Task.WhenAll(this.Clients.Values.Select(client => client.TraceLevel switch
			{
				PendingWebSocketClient.TraceLevels.None => client.SendRawAsync(noTracing ??= this.JsonSerializer.Serialize(new TracedTransactionMessage(transaction, null, null))),
				PendingWebSocketClient.TraceLevels.Events => client.SendRawAsync(eventTracing ??= this.JsonSerializer.Serialize(new TracedTransactionMessage(transaction, tracer.Events, null))),
				PendingWebSocketClient.TraceLevels.Actions => client.SendRawAsync(actionTracing ??= this.JsonSerializer.Serialize(new TracedTransactionMessage(transaction, tracer.Events, tracer.Actions))),
				_ => throw new Exception($"Unreachable code.  Unexpected {client.TraceLevel}"),
			}));
		}

		private MyTxTracer? TraceTransaction(Transaction transaction, Boolean traceActions)
		{
			var head = this.blockTree.Head;
			if (head == null) throw new Exception($"BlockTree Head was null.");
			var blockHeader = new BlockHeader(head.Hash!, Keccak.EmptyTreeHash, head.Beneficiary!, head.Difficulty, head.Number + 1, head.GasLimit, head.Timestamp + 1, Array.Empty<Byte>());
			blockHeader.TotalDifficulty = 2 * blockHeader.Difficulty;
			var block = new Block(blockHeader, new[] { transaction }, Enumerable.Empty<BlockHeader>());
			var cancellationToken = new CancellationTokenSource(2000).Token;
			var blockTracer = new SingleTransactionBlockTracer(traceActions, cancellationToken);
			var tracer = this.tracerFactory.Create();
			tracer.Trace(block, blockTracer);
			return blockTracer.TxTracer;
		}

		private async Task SendSimpleTransactionToAllClients(Transaction transaction)
		{
			var transactionAsString = this.JsonSerializer.Serialize(new TracedTransactionMessage(transaction, null, null));
			await Task.WhenAll(this.Clients.Values.Select(client => client.SendRawAsync(transactionAsString)));
		}

		private sealed class SingleTransactionBlockTracer : IBlockTracer
		{
			public Boolean IsTracingRewards => false;

			private readonly CancellationToken cancellationToken;
			public Boolean IsTracingActions { get; }
			public MyTxTracer? TxTracer { get; private set; }
			public SingleTransactionBlockTracer(Boolean traceActions, CancellationToken cancellationToken) => (this.IsTracingActions, this.cancellationToken) = (traceActions, cancellationToken);
			public void ReportReward(Address author, String rewardType, UInt256 rewardValue) { }
			public void StartNewBlockTrace(Block block) => this.TxTracer = null;
			public ITxTracer StartNewTxTrace(Transaction? tx) => new CancellationTxTracer(this.TxTracer = new MyTxTracer(this.IsTracingActions), this.cancellationToken);
			public void EndTxTrace() { }

			public void EndBlockTrace() { }
		}

		public sealed class TransactionAction
		{
			public Int64 Gas { get; }
			public UInt256 Value { get; }
			public Address From { get; }
			public Address? To { get; }
			public IEnumerable<Byte> Input { get; }
			public ExecutionType CallType { get; }
			public Boolean IsPrecompileCall { get; }
			public TransactionAction(Int64 gas, UInt256 value, Address from, Address? to, ReadOnlyMemory<Byte> input, ExecutionType callType, Boolean isPrecompileCall) => (this.Gas, this.Value, this.From, this.To, this.Input, this.CallType, this.IsPrecompileCall) = (gas, value, from, to, input.ToArray(), callType, isPrecompileCall);
		}

		public sealed class MyTxTracer : ITxTracer
		{
			public ImmutableQueue<TransactionAction> Actions { get; private set; } = ImmutableQueue<TransactionAction>.Empty;
			public ImmutableArray<LogEntry> Events { get; private set; } = ImmutableArray<LogEntry>.Empty;
			public MyTxTracer(Boolean traceActions) => this.IsTracingActions = traceActions;

			public Boolean IsTracing => true;
			public Boolean IsTracingReceipt => true;
			public Boolean IsTracingAccess => false;
			public Boolean IsTracingActions { get; }
			public Boolean IsTracingOpLevelStorage => false;
			public Boolean IsTracingMemory => false;
			public Boolean IsTracingInstructions => false;
			public Boolean IsTracingRefunds => false;
			public Boolean IsTracingCode => false;
			public Boolean IsTracingStack => false;
			public Boolean IsTracingState => false;
			public Boolean IsTracingBlockHash => false;
			public Boolean IsTracingStorage => false;
			public Boolean IsTracingFees => false;

			public void LoadOperationStorage(Address address, UInt256 storageIndex, ReadOnlySpan<byte> value) { }
			public void MarkAsFailed(Address recipient, Int64 gasSpent, Byte[] output, String error, Keccak? stateRoot) { }
			public void MarkAsSuccess(Address recipient, Int64 gasSpent, Byte[] output, LogEntry[] logs, Keccak? stateRoot) => this.Events = logs.ToImmutableArray();
			public void StartOperation(Int32 depth, Int64 gas, Instruction opcode, Int32 pc, Boolean isPostMerge = false) { }
			public void ReportAccess(IReadOnlySet<Address> accessedAddresses, IReadOnlySet<StorageCell> accessedStorageCells) { }
			public void ReportAccountRead(Address address) { }
			public void ReportAction(Int64 gas, UInt256 value, Address from, Address? to, ReadOnlyMemory<Byte> input, ExecutionType callType, Boolean isPrecompileCall = false) => this.Actions = this.Actions.Enqueue(new TransactionAction(gas, value, from, to, input, callType, isPrecompileCall));
			public void ReportActionEnd(Int64 gas, ReadOnlyMemory<Byte> output) {}
			public void ReportActionEnd(Int64 gas, Address deploymentAddress, ReadOnlyMemory<Byte> deployedCode) {}
			public void ReportActionError(EvmExceptionType evmExceptionType) { }
			public void ReportBalanceChange(Address address, UInt256? before, UInt256? after) { }
			public void ReportBlockHash(Keccak blockHash) { }
			public void ReportByteCode(Byte[] byteCode) { }
			public void ReportCodeChange(Address address, Byte[]? before, Byte[]? after) { }
			public void ReportExtraGasPressure(Int64 extraGasPressure) { }
			public void ReportFees(UInt256 fees, UInt256 burntFees) { }
			public void ReportGasUpdateForVmTrace(Int64 refund, Int64 gasAvailable) { }
			public void ReportMemoryChange(Int64 offset, in ReadOnlySpan<Byte> data) { }
			public void ReportNonceChange(Address address, UInt256? before, UInt256? after) { }
			public void ReportOperationError(EvmExceptionType error) { }
			public void ReportOperationRemainingGas(Int64 gas) { }
			public void ReportRefund(Int64 refund) { }
			public void ReportSelfDestruct(Address address, UInt256 balance, Address refundAddress) { }
			public void ReportStackPush(in ReadOnlySpan<Byte> stackItem) { }
			public void ReportStorageChange(in ReadOnlySpan<Byte> key, in ReadOnlySpan<Byte> value) { }
			public void ReportStorageChange(in StorageCell storageCell, Byte[] before, Byte[] after) { }
			public void ReportStorageRead(in StorageCell storageCell) { }
			public void SetOperationMemory(List<String> memoryTrace) { }
			public void SetOperationMemorySize(UInt64 newSize) { }
			public void SetOperationStack(List<String> stackTrace) { }
			public void SetOperationStorage(Address address, UInt256 storageIndex, ReadOnlySpan<Byte> newValue, ReadOnlySpan<Byte> currentValue) { }
			public void StartOperation(Int32 depth, Int64 gas, Instruction opcode, Int32 pc) { }
		}

		public readonly struct TracedTransactionMessage
		{
			public TransactionForRpc Transaction { get; }
			public IEnumerable<LogEntry>? Events { get; }
			public IEnumerable<TransactionAction>? Actions { get; }
			public TracedTransactionMessage(Transaction transaction, IEnumerable<LogEntry>? events, IEnumerable<TransactionAction>? actions) => (this.Transaction, this.Events, this.Actions) = (new TransactionForRpc(transaction), events, actions);
		}
	}
}
