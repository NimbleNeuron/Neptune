using System.Text;

namespace Neptune.WebSocket
{
	public class WebSocketPongFrame : WebSocketBinaryFrame
	{
		private string text;


		public WebSocketPongFrame(WebSocketPingFrame ping) : base(ping.Data) { }


		internal WebSocketPongFrame(WebSocketFrameHeader header, byte[] buffer, int offset) : base(header, buffer,
			offset) { }


		internal WebSocketPongFrame(WebSocketFrameHeader header, byte[] buffer) : base(header, buffer) { }


		public override WebSocketOpcodes Opcode => WebSocketOpcodes.Pong;


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