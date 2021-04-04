﻿using System;
using System.Collections.Generic;
using System.IO;

namespace Neptune.WebSocket
{
	public abstract class WebSocketFrame
	{
		private List<WebSocketContinuationFrame> continuationFrames;


		private byte[] incompletePayload;


		protected WebSocketFrame(WebSocketFrameHeader header)
		{
			if (Opcode != header.Opcode)
			{
				throw new WebSocketException("Invalid opcode");
			}

			Header = header;
		}


		protected WebSocketFrame(long payloadLength)
		{
			Header = new WebSocketFrameHeader(Opcode, payloadLength);
		}


		public abstract WebSocketOpcodes Opcode { get; }


		public WebSocketFrameHeader Header { get; }


		public void Append(WebSocketContinuationFrame frame)
		{
			Header.Add(frame.Header);
			if (continuationFrames == null)
			{
				continuationFrames = new List<WebSocketContinuationFrame>();
			}

			continuationFrames.Add(frame);
			if (Header.Final)
			{
				long length = incompletePayload.Length;
				continuationFrames.ForEach(delegate(WebSocketContinuationFrame frm)
				{
					length += frm.Header.PayloadLength;
				});
				if (length > 2147483647L)
				{
					throw new WebSocketException(WebSocketStatusCodes.TooLarge);
				}

				byte[] array = new byte[(int) length];
				MemoryStream stream = new MemoryStream(array);
				stream.Write(incompletePayload, 0, incompletePayload.Length);
				continuationFrames.ForEach(delegate(WebSocketContinuationFrame frm)
				{
					stream.Write(frm.Data, 0, frm.Data.Length);
				});
				OnFinal(array, false);
				incompletePayload = null;
				continuationFrames.Clear();
				continuationFrames = null;
			}
		}


		public abstract void WriteTo(Stream stream);


		protected abstract void OnFinal(byte[] payload, bool unmask);


		protected void SetIncompletePayload(byte[] payload)
		{
			if (payload.Length > 2147483647)
			{
				throw new WebSocketException(WebSocketStatusCodes.TooLarge);
			}

			incompletePayload = payload;
			Unmask(payload, 0, payload.Length);
		}


		protected void SetIncompletePayload(byte[] buffer, int offset)
		{
			if (Header.PayloadLength > 2147483647L)
			{
				throw new WebSocketException(WebSocketStatusCodes.TooLarge);
			}

			int num = (int) Header.PayloadLength;
			byte[] dst = new byte[num];
			Buffer.BlockCopy(buffer, offset, dst, 0, num);
			SetIncompletePayload(dst);
		}


		protected void Unmask(byte[] buffer, int offset, int count)
		{
			byte[] mask = Header.Mask;
			if (mask != null)
			{
				for (int i = 0; i < count; i++)
				{
					int num = offset + i;
					buffer[num] ^= mask[i % 4];
				}
			}
		}


		protected void WriteTo(Stream stream, params byte[][] payloads)
		{
			byte[] rawHeader = Header.RawHeader;
			stream.Write(rawHeader, 0, rawHeader.Length);
			byte[] mask = Header.Mask;
			if (mask != null)
			{
				int num1 = 0;
				int length = mask.Length;
				foreach (byte[] payload in payloads)
				{
					foreach (byte num2 in payload)
					{
						stream.WriteByte((byte) (num2 ^ mask[num1++]));
						if (num1 >= length)
						{
							num1 = 0;
						}
					}
				}
			}
			else
			{
				foreach (byte[] payload in payloads)
				{
					stream.Write(payload, 0, payload.Length);
				}
			}

			// co: dotPeek
			// byte[] rawHeader = this.Header.RawHeader;
			// stream.Write(rawHeader, 0, rawHeader.Length);
			// byte[] mask = this.Header.Mask;
			// if (mask != null)
			// {
			// 	int num = 0;
			// 	int num2 = mask.Length;
			// 	foreach (byte[] array in payloads)
			// 	{
			// 		foreach (byte b in array)
			// 		{
			// 			stream.WriteByte(b ^ mask[num++]);
			// 			if (num >= num2)
			// 			{
			// 				num = 0;
			// 			}
			// 		}
			// 	}
			// 	return;
			// }
			// foreach (byte[] array2 in payloads)
			// {
			// 	stream.Write(array2, 0, array2.Length);
			// }
		}


		public static WebSocketFrame Create(WebSocketFrameHeader header, byte[] buffer)
		{
			switch (header.Opcode)
			{
				case WebSocketOpcodes.Continuation:
					return new WebSocketContinuationFrame(header, buffer);
				case WebSocketOpcodes.Text:
					return new WebSocketTextFrame(header, buffer);
				case WebSocketOpcodes.Binary:
					return new WebSocketBinaryFrame(header, buffer);
				case WebSocketOpcodes.ConnectionClose:
					return new WebSocketCloseFrame(header, buffer);
				case WebSocketOpcodes.Ping:
					return new WebSocketPingFrame(header, buffer);
				case WebSocketOpcodes.Pong:
					return new WebSocketPongFrame(header, buffer);
			}

			throw new WebSocketException(WebSocketStatusCodes.Unsupported);
		}


		public static WebSocketFrame Create(WebSocketFrameHeader header, byte[] buffer, int offset)
		{
			switch (header.Opcode)
			{
				case WebSocketOpcodes.Continuation:
					return new WebSocketContinuationFrame(header, buffer, offset);
				case WebSocketOpcodes.Text:
					return new WebSocketTextFrame(header, buffer, offset);
				case WebSocketOpcodes.Binary:
					return new WebSocketBinaryFrame(header, buffer, offset);
				case WebSocketOpcodes.ConnectionClose:
					return new WebSocketCloseFrame(header, buffer, offset);
				case WebSocketOpcodes.Ping:
					return new WebSocketPingFrame(header, buffer, offset);
				case WebSocketOpcodes.Pong:
					return new WebSocketPongFrame(header, buffer, offset);
			}

			throw new WebSocketException(WebSocketStatusCodes.Unsupported);
		}
	}
}