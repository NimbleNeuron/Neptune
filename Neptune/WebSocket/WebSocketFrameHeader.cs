using System;

namespace Neptune.WebSocket
{
	public class WebSocketFrameHeader
	{
		private const byte ByteZero = 0;


		private const byte FinalBit = 128;


		private const byte OpcodeMask = 15;


		private const byte MaskBit = 128;


		private const byte TwoBytesPayloadLength = 126;


		private const byte EightBytesPayloadLength = 127;


		private const int MaskLength = 4;


		private const byte PayloadLenthMask = 127;


		private WebSocketFrameHeader(bool final, WebSocketOpcodes opcode, long payloadLength, byte[] mask)
		{
			Final = final;
			Opcode = opcode;
			PayloadLength = payloadLength;
			Mask = mask;
		}


		public WebSocketFrameHeader(bool final, WebSocketOpcodes opcode, long payloadLength) : this(final, opcode,
			payloadLength, null)
		{
			Mask = BitConverter.GetBytes(GetHashCode());
		}


		public WebSocketFrameHeader(WebSocketOpcodes opcode, long payloadLength) : this(true, opcode, payloadLength) { }


		public bool Final { get; private set; }


		public WebSocketOpcodes Opcode { get; }


		public long PayloadLength { get; private set; }


		public byte[] Mask { get; }


		public int Length => CalcHeaderLength(PayloadLength, Mask != null);


		public byte[] RawHeader {
			get
			{
				byte[] numArray = new byte[Length];
				numArray[0] = Final ? (byte) 128 : (byte) 0;
				ref byte local = ref numArray[0];
				local = (byte) ((WebSocketOpcodes) local | Opcode);
				numArray[1] = Mask != null ? (byte) 128 : (byte) 0;
				if (PayloadLength >= ushort.MaxValue)
				{
					numArray[1] |= 127;
					byte[] bytes = BitConverter.GetBytes((ulong) PayloadLength);
					if (BitConverter.IsLittleEndian)
					{
						Array.Reverse(bytes, 0, bytes.Length);
					}

					Buffer.BlockCopy(bytes, 0, numArray, 2, 8);
				}
				else if (PayloadLength >= 126L)
				{
					numArray[1] |= 126;
					byte[] bytes = BitConverter.GetBytes((ushort) PayloadLength);
					if (BitConverter.IsLittleEndian)
					{
						Array.Reverse(bytes, 0, bytes.Length);
					}

					Buffer.BlockCopy(bytes, 0, numArray, 2, 2);
				}
				else
				{
					numArray[1] |= (byte) PayloadLength;
				}

				if (Mask != null)
				{
					Buffer.BlockCopy(Mask, 0, numArray, numArray.Length - 4, 4);
				}

				return numArray;
			}

			// co: dotPeek
			// get
			// {
			// 	byte[] array = new byte[this.Length];
			// 	array[0] = (this.Final ? 128 : 0);
			// 	byte[] array2 = array;
			// 	int num = 0;
			// 	array2[num] |= (byte)this.Opcode;
			// 	array[1] = ((this.Mask != null) ? 128 : 0);
			// 	if (this.PayloadLength >= 65535L)
			// 	{
			// 		byte[] array3 = array;
			// 		int num2 = 1;
			// 		array3[num2] |= 127;
			// 		byte[] bytes = BitConverter.GetBytes((ulong)this.PayloadLength);
			// 		if (BitConverter.IsLittleEndian)
			// 		{
			// 			Array.Reverse(bytes, 0, bytes.Length);
			// 		}
			// 		Buffer.BlockCopy(bytes, 0, array, 2, 8);
			// 	}
			// 	else if (this.PayloadLength >= 126L)
			// 	{
			// 		byte[] array4 = array;
			// 		int num3 = 1;
			// 		array4[num3] |= 126;
			// 		byte[] bytes2 = BitConverter.GetBytes((ushort)this.PayloadLength);
			// 		if (BitConverter.IsLittleEndian)
			// 		{
			// 			Array.Reverse(bytes2, 0, bytes2.Length);
			// 		}
			// 		Buffer.BlockCopy(bytes2, 0, array, 2, 2);
			// 	}
			// 	else
			// 	{
			// 		byte[] array5 = array;
			// 		int num4 = 1;
			// 		array5[num4] |= (byte)this.PayloadLength;
			// 	}
			// 	if (this.Mask != null)
			// 	{
			// 		Buffer.BlockCopy(this.Mask, 0, array, array.Length - 4, 4);
			// 	}
			// 	return array;
			// }
		}


		public override string ToString()
		{
			return string.Format("WebSocketFrameHeader: final={0}, opcode={1}, payload-length={2}, mask={3}", Final,
				Opcode, PayloadLength, Mask == null ? "null" : BitConverter.ToString(Mask));
		}


		internal void Add(WebSocketFrameHeader header)
		{
			if (Final)
			{
				throw new Exception("Final frame");
			}

			if (header.Opcode != WebSocketOpcodes.Continuation)
			{
				throw new Exception("Not continuation frame");
			}

			Final = header.Final;
			PayloadLength += header.PayloadLength;
		}


		public static int CalcHeaderLength(long payloadLength, bool hasMask)
		{
			int num = 2;
			if (payloadLength >= 65535L)
			{
				num += 8;
			}
			else if (payloadLength >= 126L)
			{
				num += 2;
			}

			if (hasMask)
			{
				num += 4;
			}

			return num;
		}


		private static byte[] BlockCopyReverse(byte[] src, int offset, int count)
		{
			byte[] array = new byte[count];
			Buffer.BlockCopy(src, offset, array, 0, count);
			Array.Reverse(array, 0, count);
			return array;
		}


		public static WebSocketFrameHeader TryGet(byte[] buffer, int offset, int count)
		{
			if (count < 2)
			{
				return null;
			}

			int num = 2;
			bool final = (buffer[offset] & 128) == 128;
			WebSocketOpcodes opcode = (WebSocketOpcodes) (buffer[offset] & 15);
			bool flag = (buffer[offset + 1] & 128) == 128;
			byte[] array = null;
			long num2 = buffer[offset + 1] & 127;
			if (count < CalcHeaderLength(num2, flag))
			{
				return null;
			}

			if (num2 != 126L)
			{
				if (num2 == 127L)
				{
					if (BitConverter.IsLittleEndian)
					{
						num2 = BitConverter.ToInt64(BlockCopyReverse(buffer, offset + num, 8), 0);
					}
					else
					{
						num2 = BitConverter.ToInt64(buffer, offset + num);
					}

					num += 8;
				}
			}
			else
			{
				if (BitConverter.IsLittleEndian)
				{
					num2 = BitConverter.ToUInt16(BlockCopyReverse(buffer, offset + num, 2), 0);
				}
				else
				{
					num2 = BitConverter.ToUInt16(buffer, offset + num);
				}

				num += 2;
			}

			if (flag)
			{
				array = new byte[4];
				Buffer.BlockCopy(buffer, offset + num, array, 0, 4);
				num += 4;
			}

			return new WebSocketFrameHeader(final, opcode, num2, array);
		}
	}
}