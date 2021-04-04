using System;
using System.IO;
using System.Text;

namespace Neptune.WebSocket
{
	public class WebSocketCloseFrame : WebSocketFrame
	{
		public WebSocketCloseFrame() : this(WebSocketStatusCodes.Normal, "Normal Close") { }


		public WebSocketCloseFrame(WebSocketException ex) : this(ex.CloseCode, ex.Message) { }


		public WebSocketCloseFrame(WebSocketStatusCodes statusCode, string reason) : base(
			Encoding.UTF8.GetByteCount(reason) + 2)
		{
			StatusCode = statusCode;
			Reason = reason;
		}


		internal WebSocketCloseFrame(WebSocketFrameHeader header, byte[] buffer, int offset) : base(header)
		{
			if (header.Final)
			{
				OnFinal(buffer, offset, true);
				return;
			}

			SetIncompletePayload(buffer, offset);
		}


		internal WebSocketCloseFrame(WebSocketFrameHeader header, byte[] buffer) : base(header)
		{
			if (header.Final)
			{
				OnFinal(buffer, 0, true);
				return;
			}

			SetIncompletePayload(buffer);
		}


		public override WebSocketOpcodes Opcode => WebSocketOpcodes.ConnectionClose;


		public WebSocketStatusCodes StatusCode { get; private set; }


		public string Reason { get; private set; }


		public override void WriteTo(Stream stream)
		{
			byte[] bytes = BitConverter.GetBytes((ushort) StatusCode);
			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(bytes, 0, bytes.Length);
			}

			base.WriteTo(stream, bytes, Encoding.UTF8.GetBytes(Reason));
		}


		protected override void OnFinal(byte[] buffer, bool unmask)
		{
			OnFinal(buffer, 0, unmask);
		}


		private void OnFinal(byte[] buffer, int offset, bool unmask)
		{
			if (Header.PayloadLength > 2147483647L)
			{
				throw new WebSocketException(WebSocketStatusCodes.TooLarge);
			}

			int num = (int) Header.PayloadLength;
			if (unmask)
			{
				Unmask(buffer, offset, num);
			}

			byte[] array =
			{
				buffer[offset],
				buffer[offset + 1]
			};
			if (BitConverter.IsLittleEndian)
			{
				Array.Reverse(array, 0, 2);
			}

			StatusCode = (WebSocketStatusCodes) BitConverter.ToUInt16(array, 0);
			if (num > 2)
			{
				Reason = Encoding.UTF8.GetString(buffer, offset + 2, num - 2);
				return;
			}

			Reason = string.Empty;
		}
	}
}