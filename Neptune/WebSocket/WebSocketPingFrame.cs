using System.Text;

namespace Neptune.WebSocket
{
	public class WebSocketPingFrame : WebSocketBinaryFrame
	{
		private string text;


		public WebSocketPingFrame(byte[] data) : base(data) { }


		public WebSocketPingFrame(string text) : base(Encoding.UTF8.GetBytes(text))
		{
			this.text = text;
		}


		internal WebSocketPingFrame(WebSocketFrameHeader header, byte[] buffer, int offset) : base(header, buffer,
			offset) { }


		internal WebSocketPingFrame(WebSocketFrameHeader header, byte[] buffer) : base(header, buffer) { }


		public override WebSocketOpcodes Opcode => WebSocketOpcodes.Ping;


		public string Text {
			get
			{
				if (text != null)
				{
					return text;
				}

				if (!Header.Final)
				{
					return null;
				}

				text = Encoding.UTF8.GetString(Data, 0, (int) Header.PayloadLength);
				return text;
			}
		}
	}
}