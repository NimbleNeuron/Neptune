using System;
using System.IO;

namespace Neptune.WebSocket
{
	public class WebSocketContinuationFrame : WebSocketFrame
	{
		public WebSocketContinuationFrame(byte[] data) : base(data.Length)
		{
			Data = data;
		}


		internal WebSocketContinuationFrame(WebSocketFrameHeader header, byte[] buffer, int offset) : base(header)
		{
			OnFinal(buffer, offset, true);
		}


		internal WebSocketContinuationFrame(WebSocketFrameHeader header, byte[] buffer) : base(header)
		{
			OnFinal(buffer, true);
		}


		public override WebSocketOpcodes Opcode => WebSocketOpcodes.Continuation;


		public byte[] Data { get; private set; }


		private void OnFinal(byte[] buffer, int offset, bool unmask)
		{
			if (Header.PayloadLength > 2147483647L)
			{
				throw new WebSocketException(WebSocketStatusCodes.TooLarge);
			}

			int num = (int) Header.PayloadLength;
			Data = new byte[num];
			Buffer.BlockCopy(buffer, offset, Data, 0, num);
			if (unmask)
			{
				Unmask(Data, 0, num);
			}
		}


		protected override void OnFinal(byte[] buffer, bool unmask)
		{
			if (!unmask)
			{
				throw new WebSocketException(WebSocketStatusCodes.ProtocolError);
			}

			Unmask(buffer, 0, buffer.Length);
			Data = buffer;
		}


		public override void WriteTo(Stream stream)
		{
			base.WriteTo(stream, new[]
			{
				Data
			});
		}
	}
}