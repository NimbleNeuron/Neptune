using System;

namespace Neptune.WebSocket
{
	public class WebSocketException : Exception
	{
		public WebSocketException() : this(WebSocketStatusCodes.ClientError) { }


		public WebSocketException(WebSocketStatusCodes closeCode) : base(closeCode.ToString())
		{
			CloseCode = closeCode;
		}


		public WebSocketException(string message) : this(WebSocketStatusCodes.ClientError, message) { }


		public WebSocketException(WebSocketStatusCodes closeCode, string message) : base(message) { }


		public WebSocketStatusCodes CloseCode { get; }
	}
}