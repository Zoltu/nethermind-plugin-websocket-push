using System;
using System.Linq;

namespace Zoltu.Nethermind.Plugin.WebSocket.Push.Test
{
	public class UnitTests
	{
		[Xunit.Fact]
		public void endianness()
		{
			var input = new ReadOnlyMemory<Byte>(new Byte[] { 0xa9, 0x05, 0x9c, 0xbb });
			var callSignature = BitConverter.ToUInt32(BitConverter.IsLittleEndian ? input.Slice(0, 4).ToArray().Reverse().ToArray() : input.Slice(0, 4).ToArray(), 0);
			Xunit.Assert.Equal(0xa9059cbb, callSignature);
		}
	}
}
