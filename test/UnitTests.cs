using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Nethermind.Core;
using Nethermind.Evm;
using Nethermind.Serialization.Json;
using static Zoltu.Nethermind.Plugin.WebSocketPush.PendingWebSocketClient;
using static Zoltu.Nethermind.Plugin.WebSocketPush.PendingWebSocketClient.FilteredExecutionRequest;

namespace Zoltu.Nethermind.Plugin.WebSocket.Push.Test
{
	public class UnitTests
	{
		[Xunit.Fact]
		public async Task target_and_signature()
		{
			var filteredExecutionRequest = new FilteredExecutionRequest(new Address("0xcafebeefcafebeefcafebeefcafebeefcafebeef"), 0x022c0d9f, 40000, Array.Empty<CalldataFilter>());
			var filters = ImmutableArray<FilteredExecutionRequest>.Empty.Add(filteredExecutionRequest);
			var tracer = new MyTxTracer(filters);
			tracer.ReportAction(123456, 0, new Address("0xdeadbabedeadbabedeadbabedeadbabedeadbabe"), new Address("0xcafebeefcafebeefcafebeefcafebeefcafebeef"), new Byte[] { 0x02, 0x2c, 0x0d, 0x9f }, ExecutionType.Call, false);
			Xunit.Assert.NotEmpty(tracer.FilterMatches);
			await Task.CompletedTask;
		}

		[Xunit.Fact]
		public async Task signature_only_filter()
		{
			var filteredExecutionRequest = new FilteredExecutionRequest(Address.Zero, 0x022c0d9f, 40000, Array.Empty<CalldataFilter>());
			var filters = ImmutableArray<FilteredExecutionRequest>.Empty.Add(filteredExecutionRequest);
			var tracer = new MyTxTracer(filters);
			tracer.ReportAction(123456, 0, new Address("0xdeadbabedeadbabedeadbabedeadbabedeadbabe"), new Address("0xcafebeefcafebeefcafebeefcafebeefcafebeef"), new Byte[] { 0x02, 0x2c, 0x0d, 0x9f }, ExecutionType.Call, false);
			Xunit.Assert.NotEmpty(tracer.FilterMatches);
			await Task.CompletedTask;
		}

		[Xunit.Fact]
		public async Task no_signature_match()
		{
			var filteredExecutionRequest = new FilteredExecutionRequest(Address.Zero, 0x022c0d9f, 40000, Array.Empty<CalldataFilter>());
			var filters = ImmutableArray<FilteredExecutionRequest>.Empty.Add(filteredExecutionRequest);
			var tracer = new MyTxTracer(filters);
			tracer.ReportAction(123456, 0, new Address("0xdeadbabedeadbabedeadbabedeadbabedeadbabe"), new Address("0xcafebeefcafebeefcafebeefcafebeefcafebeef"), new Byte[] { 0x02, 0x2d, 0x0d, 0x9f }, ExecutionType.Call, false);
			Xunit.Assert.Empty(tracer.FilterMatches);
			await Task.CompletedTask;
		}

		[Xunit.Fact]
		public void serialization()
		{
			var serializer = new EthereumJsonSerializer();
			var serialized = serializer.Serialize(new FilterMatchMessage(new Transaction(), Array.Empty<FilterMatch>(), Array.Empty<LogEntry>()));
			Xunit.Assert.Equal("{\"transaction\":{\"type\":\"0x0\",\"nonce\":\"0x0\",\"gasPrice\":\"0x0\",\"gasBottleneck\":\"0x0\",\"maxPriorityFeePerGas\":\"0x0\",\"decodedMaxFeePerGas\":\"0x0\",\"maxFeePerGas\":\"0x0\",\"isEip1559\":false,\"gasLimit\":\"0x0\",\"value\":\"0x0\",\"isSigned\":false,\"isContractCreation\":true,\"isMessageCall\":false,\"timestamp\":\"0x0\",\"isServiceTransaction\":false,\"poolIndex\":\"0x0\"},\"filterMatches\":[],\"logs\":[]}", serialized);
		}

		[Xunit.Fact]
		public void endianness()
		{
			var input = new ReadOnlyMemory<Byte>(new Byte[] { 0xa9, 0x05, 0x9c, 0xbb });
			var callSignature = BitConverter.ToUInt32(BitConverter.IsLittleEndian ? input.Slice(0, 4).ToArray().Reverse().ToArray() : input.Slice(0, 4).ToArray(), 0);
			Xunit.Assert.Equal(0xa9059cbb, callSignature);
		}
	}
}
