using System.IO;
using System.Text;

namespace Neptune.WebSocket
{
	public class WebSocketTextFrame : WebSocketFrame
	{
		public WebSocketTextFrame(string text) : base(Encoding.UTF8.GetByteCount(text))
		{
			Text = text;
		}


		internal WebSocketTextFrame(WebSocketFrameHeader header, byte[] buffer, int offset) : base(header)
		{
			if (header.Final)
			{
				OnFinal(buffer, offset, true);
				return;
			}

			SetIncompletePayload(buffer, offset);
		}


		internal WebSocketTextFrame(WebSocketFrameHeader header, byte[] buffer) : base(header)
		{
			if (header.Final)
			{
				OnFinal(buffer, 0, true);
				return;
			}

			SetIncompletePayload(buffer);
		}


		public override WebSocketOpcodes Opcode => WebSocketOpcodes.Text;


		public string Text { get; private set; }


		public override void WriteTo(Stream stream)
		{
			base.WriteTo(stream, new[]
			{
				Encoding.UTF8.GetBytes(Text)
			});
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

			int count = (int) Header.PayloadLength;
			if (unmask)
			{
				Unmask(buffer, offset, count);
			}

			Text = Encoding.UTF8.GetString(buffer, offset, count);
		}
	}
}