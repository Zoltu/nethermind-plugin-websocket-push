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
using Nethermind.State;

namespace Zoltu.Nethermind.Plugin.WebSocketPush
{
	public sealed class PendingWebSocketClient : WebSocketClient
	{
		private readonly IJsonSerializer _nethermindJsonSerializer;
		private readonly IBlockTree _blockTree;
		private readonly ITransactionProcessor _transactionProcessor;

		private ImmutableArray<FilteredExecutionRequest> _filters = ImmutableArray<FilteredExecutionRequest>.Empty;

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
			var tracer = new Tracer(filters);
			await Task.Run(() =>
			{
				var head = _blockTree.Head;
				if (head == null) throw new Exception($"BlockTree Head was null.");
				var blockHeader = new BlockHeader(head.Hash!, Keccak.EmptyTreeHash, Address.Zero, head.Difficulty, head.Number + 1, head.GasLimit, head.Timestamp + 1, Array.Empty<Byte>());
				_transactionProcessor.CallAndRestore(transaction, blockHeader, tracer);
			});
			if (tracer.FilterMatches.IsEmpty)
			{
				await SendSimpleTransaction(transaction);
				return;
			}
			await SendFilterMatchTransaction(transaction, tracer.FilterMatches);
		}

		private async Task SendSimpleTransaction(Transaction transaction)
		{
			var transactionAsString = _nethermindJsonSerializer.Serialize(transaction);
			await SendRawAsync(transactionAsString);
		}

		private async Task SendFilterMatchTransaction(Transaction transaction, ImmutableArray<FilterMatch> matches)
		{
			var transactionAsString = _nethermindJsonSerializer.Serialize(new FilterMatchMessage(transaction, matches));
			await SendRawAsync(transactionAsString);
		}

		public readonly struct FilteredExecutionRequest
		{
			public readonly Address Contract { get; }

			public readonly UInt32 Signature { get; }

			public readonly UInt256 GasLimit { get; }

			public FilteredExecutionRequest(Address contract, UInt32 signature, UInt256 gasLimit) => (Contract, Signature, GasLimit) = (contract, signature, gasLimit);
		}

		public readonly struct FilterMatchMessage
		{
			public readonly Transaction Transaction;
			public readonly ImmutableArray<FilterMatch> FilterMatches;
			public FilterMatchMessage(Transaction transaction, ImmutableArray<FilterMatch> filterMatches) => (Transaction, FilterMatches) = (transaction, filterMatches);
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
				Input = input.ToArray();
				CallType = callType;
			}
		}

		public sealed class Tracer : ITxTracer
		{
			private readonly ImmutableArray<FilteredExecutionRequest> _filters;
			public ImmutableArray<FilterMatch> FilterMatches { get; private set; } = ImmutableArray<FilterMatch>.Empty;

			public Tracer(ImmutableArray<FilteredExecutionRequest> filters) => _filters = filters;

			public Boolean IsTracingReceipt => false;

			public Boolean IsTracingActions => true;

			public Boolean IsTracingOpLevelStorage => false;

			public Boolean IsTracingMemory => false;

			public Boolean IsTracingInstructions => false;

			public Boolean IsTracingRefunds => false;

			public Boolean IsTracingCode => false;

			public Boolean IsTracingStack => false;

			public Boolean IsTracingState => false;

			public Boolean IsTracingBlockHash => false;

			public Boolean IsTracingStorage => throw new NotImplementedException();

			public void MarkAsFailed(Address recipient, Int64 gasSpent, Byte[] output, String error, Keccak? stateRoot) { }
			public void MarkAsSuccess(Address recipient, Int64 gasSpent, Byte[] output, LogEntry[] logs, Keccak? stateRoot) { }
			public void ReportAccountRead(Address address) { }
			public void ReportAction(Int64 gas, UInt256 value, Address from, Address to, Byte[] input, ExecutionType callType, Boolean isPrecompileCall)
			{
				if (input.Length < 4) return;
				var callSignature = BitConverter.ToUInt32(BitConverter.IsLittleEndian ? input.Take(4).Reverse().ToArray() : input, 0);
				if (!_filters.Any(filter => filter.Contract == to && filter.Signature == callSignature)) return;
				FilterMatches = FilterMatches.Add(new FilterMatch(gas, value, from, to, input, callType));
			}
			public void ReportActionEnd(Int64 gas, Byte[] output) { }
			public void ReportActionEnd(Int64 gas, Address deploymentAddress, Byte[] deployedCode) { }
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
