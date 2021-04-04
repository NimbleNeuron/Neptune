using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Blis.Common;
using Neptune.Http;
using UnityEngine;
using Logger = Neptune.Log.Logger;

namespace Neptune.WebSocket
{
	public class WebSocket
	{
		public delegate void BinaryHandler(WebSocket sender, byte[] binary);

		public delegate void CloseHandler(WebSocket sender, bool wasClean, WebSocketStatusCodes code, string reason);

		public delegate void ErrorHandler(WebSocket sender, Exception exception);

		public delegate void MessageHandler(WebSocket sender, string message);

		public delegate void OpenHandler(WebSocket sender);

		public delegate void StateChangeHandler(WebSocket sender);

		private const int DefaultConnectTimeout = 5000;
		private const int DefaultResponseTimeout = 5000;
		private const int DefaultPingTimeout = 3000;
		private const int DefaultCloseTimeout = 5000;
		private const int BufferSize = 8192;
		private const byte Cr = 13;
		private const byte Lf = 10;

		private static readonly byte[] CrLf =
		{
			13,
			10
		};

		private IAsyncResult asyncResult;
		private byte[] buffer;
		private int bufferPos;
		private TcpClient client;
		private WebSocketCloseFrame closeFrame;
		private readonly byte[] defaultBuffer = new byte[8192];

		private WebSocketFrameHeader header;
		private WebSocketFrame incompleteFrame;
		private Dictionary<string, string> requestHeaders;

		private WebSocketStates state = WebSocketStates.Closed;
		private DateTime stateTimeout = DateTime.MaxValue;
		private Stream stream;


		public WebSocket()
		{
			ConnectTimeout = 5000;
			ResponseTimeout = 5000;
			PingTimeout = 3000;
			CloseTimeout = 5000;
		}

		public string Url { get; private set; }
		public int ConnectTimeout { get; set; }
		public int ResponseTimeout { get; set; }
		public int PingTimeout { get; set; }
		public int CloseTimeout { get; set; }

		public WebSocketStates State {
			get => state;
			private set
			{
				if (state != value)
				{
					state = value;
					if (OnStateChange != null)
					{
						try
						{
							OnStateChange(this);
						}
						catch (Exception ex)
						{
							Logger.Exception(ex);
						}
					}
				}
			}
		}

		public event StateChangeHandler OnStateChange;
		public event OpenHandler OnOpen;
		public event MessageHandler OnMessage;
		public event BinaryHandler OnBinary;
		public event ErrorHandler OnError;
		public event CloseHandler OnClose;


		private bool CheckIfIpv6OnlyNetwork()
		{
			IPHostEntry iphostEntry = null;
			try
			{
				iphostEntry = Dns.GetHostEntry("www.apple.com");
			}
			catch (Exception e)
			{
				Logger.Exception(e);
				return false;
			}

			if (iphostEntry == null)
			{
				return false;
			}

			foreach (IPAddress ipaddress in iphostEntry.AddressList)
			{
				Logger.Warning("IP LOOKUP: {0}", ipaddress);
				if (ipaddress.AddressFamily != AddressFamily.InterNetworkV6)
				{
					return false;
				}
			}

			return true;
		}


		private string GetMappedIpv6Address(string url)
		{
			Uri uri = new Uri(url);
			if (uri == null || uri.Host.Length == 0)
			{
				Logger.Error("Invalid URL");
				return url;
			}

			IPAddress ipaddress = null;
			try
			{
				ipaddress = IPAddress.Parse(uri.Host);
			}
			catch (Exception e)
			{
				Logger.Exception(e);
				return url;
			}

			if (ipaddress.AddressFamily == AddressFamily.InterNetworkV6)
			{
				return url;
			}

			string arg = "bsurvival.com";
			if (uri.Host == "133.186.134.234")
			{
				arg = "www.bsurvival.com";
			}

			return string.Format("ws://{0}:{1}", arg, uri.Port);
		}


		public bool Open(string url, IDictionary<string, string> requestHeaders = null)
		{
			bool result;
			try
			{
				Url = url;
				Uri uri = new Uri(url);
				this.requestHeaders = new Dictionary<string, string>();
				this.requestHeaders["User-Agent"] = "Neptune.WebSocket";
				HttpRequestFactory.GetHeaders(url, this.requestHeaders);
				if (requestHeaders != null)
				{
					foreach (KeyValuePair<string, string> keyValuePair in requestHeaders)
					{
						this.requestHeaders[keyValuePair.Key] = keyValuePair.Value;
					}
				}

				this.requestHeaders["Host"] = uri.Authority;
				this.requestHeaders["Upgrade"] = "websocket";
				this.requestHeaders["Connection"] = "Upgrade";
				this.requestHeaders["Sec-WebSocket-Key"] = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
				this.requestHeaders["Sec-WebSocket-Version"] = "13";
				this.requestHeaders["Origin"] = "null";
				BeginConnect(uri);
				result = true;
			}
			catch (Exception e)
			{
				HandleException(e);
				result = false;
			}

			return result;
		}


		public bool Send(WebSocketFrame frame)
		{
			bool result;
			try
			{
				if (State != WebSocketStates.Open)
				{
					result = false;
				}
				else
				{
					SendFrame(frame);
					result = true;
				}
			}
			catch (Exception e)
			{
				HandleException(e);
				result = false;
			}

			return result;
		}


		public bool Send(string data)
		{
			return Send(new WebSocketTextFrame(data));
		}


		public bool Send(byte[] data)
		{
			return Send(new WebSocketBinaryFrame(data));
		}


		public void Close()
		{
			try
			{
				WebSocketStates webSocketStates = State;
				if (webSocketStates > WebSocketStates.Handshaking)
				{
					if (webSocketStates == WebSocketStates.Open)
					{
						SendFrame(new WebSocketCloseFrame());
					}
				}
				else
				{
					Close(null);
				}
			}
			catch (Exception e)
			{
				HandleException(e);
			}
		}


		private IEnumerator UpdateState()
		{
			long pauseCount = SingletonMonoBehaviour<WebSocketUpdater>.Instance.PauseCount;
			while (asyncResult != null)
			{
				bool flag = false;
				try
				{
					if (asyncResult.IsCompleted)
					{
						switch (State)
						{
							case WebSocketStates.Connecting:
								EndConnect();
								break;
							case WebSocketStates.Handshaking:
								EndHandshake();
								break;
							case WebSocketStates.Open:
							case WebSocketStates.Closing:
								EndReadFrame();
								break;
						}
					}
					else
					{
						if (DateTime.UtcNow >= stateTimeout)
						{
							throw new WebSocketException(string.Format("{0} timed out", State));
						}

						if (pauseCount != SingletonMonoBehaviour<WebSocketUpdater>.Instance.PauseCount &&
						    State == WebSocketStates.Open)
						{
							pauseCount = SingletonMonoBehaviour<WebSocketUpdater>.Instance.PauseCount;
							SendFrame(new WebSocketPingFrame(""));
						}
						else
						{
							flag = true;
						}
					}
				}
				catch (Exception e)
				{
					HandleException(e);
				}

				if (flag)
				{
					yield return null;
				}
			}
		}


		private void HandleException(Exception e)
		{
			if (!(e is WebSocketException))
			{
				Close(e);
				return;
			}

			switch (State)
			{
				case WebSocketStates.Connecting:
				case WebSocketStates.Handshaking:
				case WebSocketStates.Closing:
					Close(e);
					return;
				case WebSocketStates.Open:
					SendFrame(new WebSocketCloseFrame(e as WebSocketException));
					return;
				default:
					return;
			}
		}


		private void SetState(WebSocketStates state, int timeout = 0)
		{
			State = state;
			stateTimeout = timeout == 0 ? DateTime.MaxValue : DateTime.UtcNow + TimeSpan.FromMilliseconds(timeout);
			Debug.Log(string.Format("WebSocket | SetState: {0} | Timeout: {1}", state, timeout));
		}


		private void SendFrame(WebSocketFrame frame)
		{
			if (State == WebSocketStates.Open)
			{
				if (frame is WebSocketCloseFrame)
				{
					if (closeFrame == null)
					{
						closeFrame = frame as WebSocketCloseFrame;
					}

					SetState(WebSocketStates.Closing, CloseTimeout);
				}
				else if (frame is WebSocketPingFrame && state == WebSocketStates.Open)
				{
					stateTimeout = DateTime.UtcNow + TimeSpan.FromMilliseconds(PingTimeout);
				}

				using (MemoryStream memoryStream =
					new MemoryStream(frame.Header.Length + (int) frame.Header.PayloadLength))
				{
					frame.WriteTo(memoryStream);
					memoryStream.WriteTo(stream);
				}
			}
		}


		private void BeginRead()
		{
			if (buffer == null)
			{
				ResetBuffer();
			}

			asyncResult = stream.BeginRead(buffer, bufferPos, buffer.Length - bufferPos, null, null);
		}


		private int EndRead()
		{
			int num = stream.EndRead(asyncResult);
			asyncResult = null;
			bufferPos += num;
			if (num == 0)
			{
				Close(null);
			}

			return num;
		}


		private void ResetBuffer()
		{
			buffer = defaultBuffer;
			bufferPos = 0;
		}


		private void SetBuffer(byte[] dst, byte[] src, int offset, int count)
		{
			buffer = dst;
			Buffer.BlockCopy(src, offset, dst, 0, count);
			bufferPos = count;
		}


		private void Close(Exception e)
		{
			if (State != WebSocketStates.Closed)
			{
				try
				{
					WebSocketStates webSocketStates = State;
					if (webSocketStates - WebSocketStates.Handshaking <= 2 && client != null && client.Connected &&
					    asyncResult == null)
					{
						int num;
						for (int i = client.Available; i > 0; i -= num)
						{
							int count = Mathf.Min(i, defaultBuffer.Length);
							num = stream.Read(buffer, 0, count);
						}
					}

					if (stream != null)
					{
						stream.Close();
					}

					if (client != null)
					{
						client.Close();
					}
				}
				catch (Exception ex)
				{
					Logger.Exception(ex);
				}
				finally
				{
					client = null;
					stream = null;
					asyncResult = null;
				}

				ResetBuffer();
				header = null;
				bool wasClean = e == null && closeFrame != null;
				WebSocketStatusCodes code;
				string reason;
				if (closeFrame == null)
				{
					code = WebSocketStatusCodes.UncleanClose;
					reason = e == null ? string.Empty : e.Message;
				}
				else
				{
					code = closeFrame.StatusCode;
					reason = closeFrame.Reason;
					closeFrame = null;
				}

				SetState(WebSocketStates.Closed);
				try
				{
					if (e != null && OnError != null)
					{
						OnError(this, e);
					}
				}
				catch (Exception ex2)
				{
					Logger.Exception(ex2);
				}

				try
				{
					if (OnClose != null)
					{
						OnClose(this, wasClean, code, reason);
					}
				}
				catch (Exception ex3)
				{
					Logger.Exception(ex3);
				}
			}
		}


		private void BeginConnect(Uri uri, bool ipv6 = false)
		{
			SetState(WebSocketStates.Connecting, ConnectTimeout);
			if (ipv6)
			{
				client = new TcpClient(AddressFamily.InterNetworkV6);
			}
			else
			{
				client = new TcpClient();
			}

			client.NoDelay = true;
			asyncResult = client.BeginConnect(uri.Host, uri.Port, null, null);
			SingletonMonoBehaviour<WebSocketUpdater>.Instance.UpdateState(this, UpdateState());
		}


		private void EndConnect()
		{
			client.EndConnect(asyncResult);
			asyncResult = null;
			BeginHandshake();
		}


		private void BeginHandshake()
		{
			SetState(WebSocketStates.Handshaking, ResponseTimeout);
			stream = client.GetStream();
			using (MemoryStream memoryStream = new MemoryStream())
			{
				using (BinaryWriter binaryWriter = new BinaryWriter(memoryStream))
				{
					binaryWriter.Write(
						Encoding.ASCII.GetBytes(string.Format("GET {0} HTTP/1.1", new Uri(Url).PathAndQuery)));
					binaryWriter.Write(CrLf);
					foreach (KeyValuePair<string, string> keyValuePair in requestHeaders)
					{
						binaryWriter.Write(Encoding.ASCII.GetBytes(keyValuePair.Key));
						binaryWriter.Write(':');
						binaryWriter.Write(' ');
						binaryWriter.Write(Encoding.ASCII.GetBytes(keyValuePair.Value));
						binaryWriter.Write(CrLf);
					}

					binaryWriter.Write(CrLf);
					memoryStream.WriteTo(stream);
				}
			}

			BeginRead();
		}


		private void EndHandshake()
		{
			if (EndRead() == 0)
			{
				return;
			}

			int num = 0;
			int num2 = 0;
			while (num2 < bufferPos && num != 4)
			{
				byte b = buffer[num2++];
				switch (num)
				{
					case 0:
						num = b == 13 ? 1 : 0;
						break;
					case 1:
						num = b == 10 ? 2 : b == 13 ? 1 : 0;
						break;
					case 2:
						num = b == 13 ? 3 : 0;
						break;
					case 3:
						num = b == 10 ? 4 : b == 13 ? 1 : 0;
						break;
				}
			}

			if (num == 4)
			{
				using (MemoryStream memoryStream = new MemoryStream(buffer, 0, num2))
				{
					using (StreamReader streamReader = new StreamReader(memoryStream, Encoding.ASCII))
					{
						int num3 = 0;
						string text = streamReader.ReadLine();
						string[] array = text.Split(' ');
						if (array.Length < 3 || !int.TryParse(array[1], out num3))
						{
							throw new WebSocketException("Invalid HTTP status-line : " + text);
						}

						Dictionary<string, string> dictionary = new Dictionary<string, string>();
						dictionary["upgrade"] = string.Empty;
						dictionary["connection"] = string.Empty;
						dictionary["sec-websocket-accept"] = string.Empty;
						string text2;
						while ((text2 = streamReader.ReadLine()).Length > 0)
						{
							int num4 = text2.IndexOf(':');
							if (num4 >= 0)
							{
								string key = text2.Substring(0, num4).Trim().ToLower();
								string value = text2.Substring(num4 + 1).Trim();
								dictionary[key] = value;
							}
						}

						if (num3 != 101 || dictionary["upgrade"].ToLower() != "websocket" ||
						    dictionary["connection"].ToLower() != "upgrade")
						{
							throw new WebSocketException(string.Format(
								"Handshake failed : status-code={0},upgrade={1},connection={2}", num3,
								dictionary["upgrade"], dictionary["connection"]));
						}

						string str = requestHeaders["Sec-WebSocket-Key"];
						string b2 = dictionary["sec-websocket-accept"].ToLower();
						if (Convert.ToBase64String(
							new SHA1CryptoServiceProvider().ComputeHash(
								Encoding.ASCII.GetBytes(str + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11"))).ToLower() != b2)
						{
							throw new WebSocketException("Handshake failed : invalid Sec-WebSocket-Accept");
						}
					}
				}

				SetState(WebSocketStates.Open);
				try
				{
					if (OnOpen != null)
					{
						OnOpen(this);
					}
				}
				catch (Exception ex)
				{
					Logger.Exception(ex);
				}

				if (num2 < bufferPos)
				{
					ProcessFrames(num2);
				}
				else
				{
					ResetBuffer();
				}

				BeginRead();
				return;
			}

			if (bufferPos == buffer.Length)
			{
				throw new WebSocketException("Too large response header");
			}

			BeginRead();
		}


		private void EndReadFrame()
		{
			if (EndRead() == 0)
			{
				return;
			}

			if (header == null)
			{
				ProcessFrames(0);
			}
			else if (bufferPos == buffer.Length)
			{
				OnReceiveFrame(WebSocketFrame.Create(header, buffer));
				header = null;
				ResetBuffer();
			}

			BeginRead();
		}


		private void ProcessFrames(int offset)
		{
			while (offset < bufferPos)
			{
				int num = bufferPos - offset;
				WebSocketFrameHeader webSocketFrameHeader = WebSocketFrameHeader.TryGet(buffer, offset, num);
				if (webSocketFrameHeader == null)
				{
					SetBuffer(defaultBuffer, buffer, offset, num);
					return;
				}

				if (webSocketFrameHeader.PayloadLength > 2147483647L)
				{
					throw new WebSocketException(WebSocketStatusCodes.TooLarge);
				}

				offset += webSocketFrameHeader.Length;
				num -= webSocketFrameHeader.Length;
				int num2 = (int) webSocketFrameHeader.PayloadLength;
				if (num < num2)
				{
					header = webSocketFrameHeader;
					SetBuffer(new byte[num2], buffer, offset, num);
					return;
				}

				OnReceiveFrame(WebSocketFrame.Create(webSocketFrameHeader, buffer, offset));
				offset += num2;
				if (offset >= bufferPos)
				{
					ResetBuffer();
				}
			}
		}


		private void OnReceiveFrame(WebSocketFrame frame)
		{
			if (frame.Header.Final)
			{
				switch (frame.Opcode)
				{
					case WebSocketOpcodes.Continuation:
						OnContinuationFrame(frame as WebSocketContinuationFrame);
						return;
					case WebSocketOpcodes.Text:
						OnTextFrame(frame as WebSocketTextFrame);
						return;
					case WebSocketOpcodes.Binary:
						OnBinaryFrame(frame as WebSocketBinaryFrame);
						return;
					case (WebSocketOpcodes) 3:
					case (WebSocketOpcodes) 4:
					case (WebSocketOpcodes) 5:
					case (WebSocketOpcodes) 6:
					case (WebSocketOpcodes) 7:
						break;
					case WebSocketOpcodes.ConnectionClose:
						OnCloseFrame(frame as WebSocketCloseFrame);
						return;
					case WebSocketOpcodes.Ping:
						OnPingFrame(frame as WebSocketPingFrame);
						return;
					case WebSocketOpcodes.Pong:
						OnPongFrame(frame as WebSocketPongFrame);
						break;
					default:
						return;
				}

				return;
			}

			if (incompleteFrame != null)
			{
				throw new WebSocketException(WebSocketStatusCodes.ProtocolError);
			}

			incompleteFrame = frame;
		}


		private void OnBinaryFrame(WebSocketBinaryFrame frame)
		{
			try
			{
				if (OnBinary != null)
				{
					OnBinary(this, frame.Data);
				}
			}
			catch (Exception ex)
			{
				Logger.Exception(ex);
			}
		}


		private void OnTextFrame(WebSocketTextFrame frame)
		{
			try
			{
				if (OnMessage != null)
				{
					OnMessage(this, frame.Text);
				}
			}
			catch (Exception ex)
			{
				Logger.Exception(ex);
			}
		}


		private void OnCloseFrame(WebSocketCloseFrame frame)
		{
			if (State == WebSocketStates.Open)
			{
				closeFrame = frame;
				SendFrame(new WebSocketCloseFrame());
			}
		}


		private void OnContinuationFrame(WebSocketContinuationFrame frame)
		{
			if (incompleteFrame == null)
			{
				throw new WebSocketException(WebSocketStatusCodes.ProtocolError);
			}

			incompleteFrame.Append(frame);
			if (incompleteFrame.Header.Final)
			{
				OnReceiveFrame(incompleteFrame);
				incompleteFrame = null;
			}
		}


		private void OnPingFrame(WebSocketPingFrame frame)
		{
			if (State == WebSocketStates.Open)
			{
				SendFrame(new WebSocketPongFrame(frame));
			}
		}


		private void OnPongFrame(WebSocketPongFrame frame)
		{
			if (state == WebSocketStates.Open)
			{
				stateTimeout = DateTime.MaxValue;
			}
		}
	}
}