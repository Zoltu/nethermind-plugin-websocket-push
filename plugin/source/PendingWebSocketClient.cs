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
using Nethermind.Logging;
using Nethermind.Mev.Execution;
using Nethermind.Serialization.Json;

namespace Zoltu.Nethermind.Plugin.WebSocketPush
{
	public sealed class PendingWebSocketClient : WebSocketClient
	{
		private readonly IJsonSerializer nethermindJsonSerializer;
		private readonly ITracerFactory tracerFactory;
		private readonly IBlockTree blockTree;

		private ImmutableArray<FilteredExecutionRequest> filters = ImmutableArray<FilteredExecutionRequest>.Empty;

		private Int64 eventTracingGasLimit = Int64.MaxValue;

		public PendingWebSocketClient(ILogger logger, IWebSocketPushConfig config, WebSocket webSocket, String id, String client, IJsonSerializer nethermindJsonSerializer, ITracerFactory tracerFactory, IBlockTree blockTree) : base(logger, config, webSocket, id, client)
		{
			this.nethermindJsonSerializer = nethermindJsonSerializer;
			this.tracerFactory = tracerFactory;
			this.blockTree = blockTree;
		}

		public override async Task ReceiveAsync(Memory<Byte> data)
		{
			try
			{
				var dataString = System.Text.Encoding.UTF8.GetString(data.Span);
				var filterOptions = this.nethermindJsonSerializer.Deserialize<FilteredExecutionRequest>(dataString);
				if (filterOptions.Contract == null && filterOptions.Signature == null && filterOptions.CalldataFilters.Length == 0)
				{
					this.eventTracingGasLimit = filterOptions.GasLimit;
					return;
				}
				this.filters = this.filters.Add(filterOptions);
				this.logger.Info($"New Pending Filter added: {filterOptions.Contract}.{filterOptions.Signature}");
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

		public async Task OnNewPending(Transaction transaction)
		{
			// get a local copy of the list we can work with for the duration of this method
			var filters = this.filters;
			if (transaction.GasLimit < this.eventTracingGasLimit)
			{
				// if there are no filters, just send the pending transactions as they are (no execution details)
				if (filters.IsEmpty)
				{
					await this.SendSimpleTransaction(transaction);
					return;
				}
				// if the gas limit for the transaction is less than the gas limit for all of the filters then ignore this transaction
				if (filters.All(filter => transaction.GasLimit < filter.GasLimit))
				{
					await this.SendSimpleTransaction(transaction);
					return;
				}
			}

			var head = this.blockTree.Head;
			if (head == null) throw new Exception($"BlockTree Head was null.");
			var blockHeader = new BlockHeader(head.Hash!, Keccak.EmptyTreeHash, head.Beneficiary!, head.Difficulty, head.Number + 1, head.GasLimit, head.Timestamp + 1, Array.Empty<Byte>());
			blockHeader.TotalDifficulty = 2 * blockHeader.Difficulty;
			var block = new Block(blockHeader, new[] { transaction }, Enumerable.Empty<BlockHeader>());
			var cancellationToken = new CancellationTokenSource(2000).Token;
			var blockTracer = new MyBlockTracer(this.filters, cancellationToken);
			var tracer = this.tracerFactory.Create();
			var postTraceStateRoot = tracer.Trace(block, blockTracer);
			var txTracer = blockTracer.TxTracer;
			var filterMatches = txTracer?.FilterMatches ?? ImmutableArray<FilterMatch>.Empty;
			var logs = txTracer?.Logs ?? ImmutableArray<LogEntry>.Empty;
			if (filterMatches.IsEmpty && transaction.GasLimit < this.eventTracingGasLimit)
			{
				await this.SendSimpleTransaction(transaction);
				return;
			}
			await this.SendFilterMatchTransaction(transaction, filterMatches.ToArray(), logs.ToArray());
		}

		private async Task SendSimpleTransaction(Transaction transaction)
		{
			var transactionAsString = this.nethermindJsonSerializer.Serialize(transaction);
			await this.SendRawAsync(transactionAsString);
		}

		private async Task SendFilterMatchTransaction(Transaction transaction, FilterMatch[] matches, LogEntry[] logs)
		{
			var transactionAsString = this.nethermindJsonSerializer.Serialize(new FilterMatchMessage(transaction, matches, logs));
			await this.SendRawAsync(transactionAsString);
		}

		public readonly struct FilteredExecutionRequest
		{
			public struct CalldataFilter
			{
				public readonly UInt32 Offset;
				public readonly Byte[] SearchBytes;
				public CalldataFilter(UInt32 offset, Byte[] searchBytes) => (this.Offset, this.SearchBytes) = (offset, searchBytes);
			}

			public readonly Address? Contract;
			public readonly UInt32? Signature;
			public readonly Int64 GasLimit;
			public readonly CalldataFilter[] CalldataFilters;
			public FilteredExecutionRequest(Address? contract, UInt32? signature, Int64 gasLimit, CalldataFilter[] calldataFilters) => (this.Contract, this.Signature, this.GasLimit, this.CalldataFilters) = (contract, signature, gasLimit, calldataFilters);
		}

		public readonly struct FilterMatchMessage
		{
			public Transaction Transaction { get; }
			public FilterMatch[] FilterMatches { get; }
			public LogEntry[] Logs { get; }
			public FilterMatchMessage(Transaction transaction, FilterMatch[] filterMatches, LogEntry[] logs) => (this.Transaction, this.FilterMatches, this.Logs) = (transaction, filterMatches, logs);
		}

		public readonly struct FilterMatch
		{
			public readonly Int64 Gas;
			public readonly UInt256 Value;
			public readonly Address From;
			public readonly Address? To;
			public readonly Byte[] Input;
			public readonly ExecutionType CallType;
			public FilterMatch(Int64 gas, UInt256 value, Address from, Address? to, Byte[] input, ExecutionType callType)
			{
				this.Gas = gas;
				this.Value = value;
				this.From = from;
				this.To = to;
				this.Input = input;
				this.CallType = callType;
			}
		}

		private class MyBlockTracer : IBlockTracer
		{
			public Boolean IsTracingRewards => false;

			private readonly ImmutableArray<FilteredExecutionRequest> filters;
			private readonly CancellationToken cancellationToken;
			public MyTxTracer? TxTracer { get; private set; }
			public MyBlockTracer(ImmutableArray<FilteredExecutionRequest> filters, CancellationToken cancellationToken) => (this.filters, this.cancellationToken) = (filters, cancellationToken);
			public void ReportReward(Address author, String rewardType, UInt256 rewardValue) { }
			public void StartNewBlockTrace(Block block) => this.TxTracer = null;
			public ITxTracer StartNewTxTrace(Transaction? tx) => new CancellationTxTracer(this.TxTracer = new MyTxTracer(this.filters), this.cancellationToken);
			public void EndTxTrace() { }
		}

		public sealed class MyTxTracer : ITxTracer
		{
			private readonly ImmutableArray<FilteredExecutionRequest> filters;
			public ImmutableArray<FilterMatch> FilterMatches { get; private set; } = ImmutableArray<FilterMatch>.Empty;
			public ImmutableArray<LogEntry> Logs { get; private set; } = ImmutableArray<LogEntry>.Empty;
			public MyTxTracer(ImmutableArray<FilteredExecutionRequest> filters) => (this.filters, this.IsTracingActions) = (filters, !filters.IsEmpty);

			public Boolean IsTracingReceipt => true;
			public Boolean IsTracingAccess => false;
			public Boolean IsTracingActions { get; private set; }
			public Boolean IsTracingOpLevelStorage => false;
			public Boolean IsTracingMemory => false;
			public Boolean IsTracingInstructions => false;
			public Boolean IsTracingRefunds => false;
			public Boolean IsTracingCode => false;
			public Boolean IsTracingStack => false;
			public Boolean IsTracingState => false;
			public Boolean IsTracingBlockHash => false;
			public Boolean IsTracingStorage => false;

			public void MarkAsFailed(Address recipient, Int64 gasSpent, Byte[] output, String error, Keccak? stateRoot) { }
			public void MarkAsSuccess(Address recipient, Int64 gasSpent, Byte[] output, LogEntry[] logs, Keccak? stateRoot) => this.Logs = logs.ToImmutableArray();
			public void ReportAccess(IReadOnlySet<Address> accessedAddresses, IReadOnlySet<StorageCell> accessedStorageCells) {}
			public void ReportAccountRead(Address address) { }
			public void ReportAction(Int64 gas, UInt256 value, Address from, Address? to, ReadOnlyMemory<Byte> input, ExecutionType callType, Boolean isPrecompileCall = false)
			{
				if (input.Length < 4) return;
				var callSignature = BitConverter.ToUInt32(BitConverter.IsLittleEndian ? input.Slice(0, 4).ToArray().Reverse().ToArray() : input.Slice(0, 4).ToArray(), 0);

				if (this.filters.Any(MatchesFilter))
				{
					this.FilterMatches = this.FilterMatches.Add(new FilterMatch(gas, value, from, to, input.ToArray(), callType));
				}

				Boolean MatchesFilter(FilteredExecutionRequest filter)
				{
					return MatchesDestination(filter) && MatchesSignature(filter) && filter.CalldataFilters.All(MatchesCalldata);
				}

				Boolean MatchesDestination(FilteredExecutionRequest filter)
				{
					return filter.Contract == null || filter.Contract == to;
				}

				Boolean MatchesSignature(FilteredExecutionRequest filter)
				{
					return filter.Signature == null || filter.Signature == callSignature;
				}

				Boolean MatchesCalldata(FilteredExecutionRequest.CalldataFilter calldataFilter)
				{
					if (input.Length < calldataFilter.Offset + calldataFilter.SearchBytes.Length) return false;
					var calldataSlice = input.Slice((Int32)calldataFilter.Offset, calldataFilter.SearchBytes.Length);
					return calldataFilter.SearchBytes.SequenceEqual(calldataSlice.ToArray());
				}
			}
			public void ReportActionEnd(Int64 gas, ReadOnlyMemory<Byte> output) {}
			public void ReportActionEnd(Int64 gas, Address deploymentAddress, ReadOnlyMemory<Byte> deployedCode) {}
			public void ReportActionError(EvmExceptionType evmExceptionType) { }
			public void ReportBalanceChange(Address address, UInt256? before, UInt256? after) { }
			public void ReportBlockHash(Keccak blockHash) { }
			public void ReportByteCode(Byte[] byteCode) { }
			public void ReportCodeChange(Address address, Byte[]? before, Byte[]? after) { }
			public void ReportExtraGasPressure(Int64 extraGasPressure) { }
			public void ReportGasUpdateForVmTrace(Int64 refund, Int64 gasAvailable) { }
			public void ReportMemoryChange(Int64 offset, in ReadOnlySpan<Byte> data) { }
			public void ReportNonceChange(Address address, UInt256? before, UInt256? after) { }
			public void ReportOperationError(EvmExceptionType error) { }
			public void ReportOperationRemainingGas(Int64 gas) { }
			public void ReportRefund(Int64 refund) { }
			public void ReportSelfDestruct(Address address, UInt256 balance, Address refundAddress) { }
			public void ReportStackPush(in ReadOnlySpan<Byte> stackItem) { }
			public void ReportStorageChange(in ReadOnlySpan<Byte> key, in ReadOnlySpan<Byte> value) { }
			public void ReportStorageChange(StorageCell storageCell, Byte[] before, Byte[] after) { }
			public void ReportStorageRead(StorageCell storageCell) => throw new NotImplementedException();
			public void SetOperationMemory(List<String> memoryTrace) { }
			public void SetOperationMemorySize(UInt64 newSize) { }
			public void SetOperationStack(List<String> stackTrace) { }
			public void SetOperationStorage(Address address, UInt256 storageIndex, ReadOnlySpan<Byte> newValue, ReadOnlySpan<Byte> currentValue) => throw new NotImplementedException();
			public void StartOperation(Int32 depth, Int64 gas, Instruction opcode, Int32 pc) { }
		}
	}
}
