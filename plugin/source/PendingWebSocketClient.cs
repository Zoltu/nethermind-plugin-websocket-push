using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Nethermind.Blockchain;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Evm;
using Nethermind.Evm.Tracing;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Serialization.Json;

namespace Zoltu.Nethermind.Plugin.WebSocketPush
{
	public sealed class PendingWebSocketClient : WebSocketClient
	{
		private readonly IJsonSerializer _nethermindJsonSerializer;
		private readonly IBlockTree _blockTree;
		private readonly ITransactionProcessor _transactionProcessor;

		private ImmutableArray<FilteredExecutionRequest> _filters = ImmutableArray<FilteredExecutionRequest>.Empty;

		private Int64 _eventTracingGasLimit = Int64.MaxValue;

		public PendingWebSocketClient(ILogger logger, IJsonSerializer nethermindJsonSerializer, IBlockTree blockTree, ITransactionProcessor transactionProcessor, IWebSocketPushConfig config, WebSocket webSocket, String id, String client) : base(logger, config, webSocket, id, client)
		{
			_nethermindJsonSerializer = nethermindJsonSerializer;
			_blockTree = blockTree;
			_transactionProcessor = transactionProcessor;
		}

		public override async Task ReceiveAsync(Memory<Byte> data)
		{
			try
			{
				var dataString = System.Text.Encoding.UTF8.GetString(data.Span);
				var filterOptions = _nethermindJsonSerializer.Deserialize<FilteredExecutionRequest>(dataString);
				if (filterOptions.Contract == Address.Zero && filterOptions.Signature == 0)
				{
					_eventTracingGasLimit = filterOptions.GasLimit;
					return;
				}
				_filters = _filters.Add(filterOptions);
				_logger.Info($"New Pending Filter added: {filterOptions.Contract}.{filterOptions.Signature}");
				await Task.CompletedTask;
			}
			catch (Exception exception)
			{
				await SendRawAsync($"Exception occurred while processing request: {exception.Message}");
				_logger.Info($"Exception occurred while processing Pending WebSocket request:");
				// optimistically try to log the failing incoming message as a string, if it fails move on
				try { _logger.Info(System.Text.Encoding.UTF8.GetString(data.Span)); } catch { }
				_logger.Info(exception.Message);
				_logger.Info(exception.StackTrace);
			}
		}

		public async Task Send(Transaction transaction)
		{
			// get a local copy of the list we can work with for the duration of this method
			var filters = _filters;
			if (transaction.GasLimit < _eventTracingGasLimit)
			{
				// if there are no filters, just send the pending transactions as they are (no execution details)
				if (filters.IsEmpty)
				{
					await SendSimpleTransaction(transaction);
					return;
				}
				// if the gas limit for the transaction is less than the gas limit for all of the filters then ignore this transaction
				if (filters.All(filter => transaction.GasLimit < filter.GasLimit))
				{
					await SendSimpleTransaction(transaction);
					return;
				}
			}
			var tracer = new Tracer(filters);
			await Task.Run(() =>
			{
				var head = _blockTree.Head;
				if (head == null) throw new Exception($"BlockTree Head was null.");
				var blockHeader = new BlockHeader(head.Hash!, Keccak.EmptyTreeHash, Address.Zero, head.Difficulty, head.Number + 1, head.GasLimit, head.Timestamp + 1, Array.Empty<Byte>());
				_transactionProcessor.CallAndRestore(transaction, blockHeader, tracer);
			});
			if (tracer.FilterMatches.IsEmpty && transaction.GasLimit < _eventTracingGasLimit)
			{
				await SendSimpleTransaction(transaction);
				return;
			}
			await SendFilterMatchTransaction(transaction, tracer.FilterMatches.ToArray(), tracer.Logs.ToArray());
		}

		private async Task SendSimpleTransaction(Transaction transaction)
		{
			var transactionAsString = _nethermindJsonSerializer.Serialize(transaction);
			await SendRawAsync(transactionAsString);
		}

		private async Task SendFilterMatchTransaction(Transaction transaction, FilterMatch[] matches, LogEntry[] logs)
		{
			var transactionAsString = _nethermindJsonSerializer.Serialize(new FilterMatchMessage(transaction, matches, logs));
			await SendRawAsync(transactionAsString);
		}

		public readonly struct FilteredExecutionRequest
		{
			public struct CalldataFilter
			{
				public readonly UInt32 Offset;
				public readonly Byte[] SearchBytes;
				public CalldataFilter(UInt32 offset, Byte[] searchBytes) => (Offset, SearchBytes) = (offset, searchBytes);
			}

			public readonly Address Contract;
			public readonly UInt32 Signature;
			public readonly Int64 GasLimit;
			public readonly CalldataFilter[] CalldataFilters;
			public FilteredExecutionRequest(Address contract, UInt32 signature, Int64 gasLimit, CalldataFilter[] calldataFilters) => (Contract, Signature, GasLimit, CalldataFilters) = (contract, signature, gasLimit, calldataFilters);
		}

		public readonly struct FilterMatchMessage
		{
			public readonly Transaction Transaction;
			public readonly FilterMatch[] FilterMatches;
			public readonly LogEntry[] Logs;
			public FilterMatchMessage(Transaction transaction, FilterMatch[] filterMatches, LogEntry[] logs) => (Transaction, FilterMatches, Logs) = (transaction, filterMatches, logs);
		}

		public readonly struct FilterMatch
		{
			public readonly Int64 Gas;
			public readonly UInt256 Value;
			public readonly Address From;
			public readonly Address To;
			public readonly Byte[] Input;
			public readonly ExecutionType CallType;
			public FilterMatch(Int64 gas, UInt256 value, Address from, Address to, Byte[] input, ExecutionType callType)
			{
				Gas = gas;
				Value = value;
				From = from;
				To = to;
				Input = input;
				CallType = callType;
			}
		}

		public sealed class Tracer : ITxTracer
		{
			private readonly ImmutableArray<FilteredExecutionRequest> _filters;
			public ImmutableArray<FilterMatch> FilterMatches { get; private set; } = ImmutableArray<FilterMatch>.Empty;
			public ImmutableArray<LogEntry> Logs { get; private set; } = ImmutableArray<LogEntry>.Empty;
			public Tracer(ImmutableArray<FilteredExecutionRequest> filters) => _filters = filters;

			public Boolean IsTracingReceipt => true;
			public Boolean IsTracingAccess => false;
			public Boolean IsTracingActions => true;
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
			public void MarkAsSuccess(Address recipient, Int64 gasSpent, Byte[] output, LogEntry[] logs, Keccak? stateRoot)
			{
				Logs = logs.ToImmutableArray();
			}
			public void ReportAccess(IReadOnlySet<Address> accessedAddresses, IReadOnlySet<StorageCell> accessedStorageCells) {}
			public void ReportAccountRead(Address address) { }
			public void ReportAction(Int64 gas, UInt256 value, Address from, Address to, ReadOnlyMemory<Byte> input, ExecutionType callType, Boolean isPrecompileCall = false)
			{
				if (input.Length < 4) return;
				var callSignature = BitConverter.ToUInt32(BitConverter.IsLittleEndian ? input.Slice(0, 4).ToArray().Reverse().ToArray() : input.Slice(0, 4).ToArray(), 0);

				if (_filters.Any(MatchesDestinationAndSignature) || _filters.Any(MatchesSignatureAndCalldata))
				{
					FilterMatches = FilterMatches.Add(new FilterMatch(gas, value, from, to, input.ToArray(), callType));
				}

				Boolean MatchesDestinationAndSignature(FilteredExecutionRequest filter)
				{
					return filter.Contract == to && filter.Signature == callSignature;
				}

				Boolean MatchesSignatureAndCalldata(FilteredExecutionRequest filter)
				{
					return filter.Contract == Address.Zero && filter.Signature == callSignature && filter.CalldataFilters.ToArray().All(MatchesCalldata);
				}

				Boolean MatchesCalldata(FilteredExecutionRequest.CalldataFilter calldataFilter)
				{
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
