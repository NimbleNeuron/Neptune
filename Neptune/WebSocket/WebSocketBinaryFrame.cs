using System;
using System.IO;

namespace Neptune.WebSocket
{
	public class WebSocketBinaryFrame : WebSocketFrame
	{
		public WebSocketBinaryFrame(byte[] data) : base(data.Length)
		{
			Data = data;
		}


		internal WebSocketBinaryFrame(WebSocketFrameHeader header, byte[] buffer, int offset) : base(header)
		{
			if (header.Final)
			{
				OnFinal(buffer, offset, true);
				return;
			}

			SetIncompletePayload(buffer, offset);
		}


		internal WebSocketBinaryFrame(WebSocketFrameHeader header, byte[] buffer) : base(header)
		{
			if (header.Final)
			{
				OnFinal(buffer, true);
				return;
			}

			SetIncompletePayload(buffer);
		}


		public override WebSocketOpcodes Opcode => WebSocketOpcodes.Binary;


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