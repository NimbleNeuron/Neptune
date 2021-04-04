using System;

namespace Neptune.Http
{
	public class HttpResponseProgress
	{
		private DateTime lastDateTime = DateTime.UtcNow;
		private int timeout;

		internal HttpResponseProgress()
		{
			Loading = false;
			Loaded = false;
			ContentLength = -1L;
			ExpectedLength = -1L;
			TotalRead = 0L;
			TotalWrite = 0L;
		}

		public bool Loading { get; private set; }
		public bool Loaded { get; }
		public long ContentLength { get; private set; }
		public long ExpectedLength { get; private set; }
		public long TotalRead { get; private set; }
		public long TotalWrite { get; private set; }

		public bool IsTimedOut => timeout > 0 && Loading &&
		                          !(DateTime.UtcNow - lastDateTime < TimeSpan.FromMilliseconds(timeout));

		internal void OnBegin(int timeout)
		{
			this.timeout = timeout;
			Loading = true;
			UpdateDateTime();
		}

		internal void OnSent()
		{
			UpdateDateTime();
		}

		internal void OnHeader(long contentLength, long expectedLength, string contentEncoding)
		{
			contentEncoding = (contentEncoding ?? "identity").ToLower();
			if ("gzip" == contentEncoding || "deflate" == contentEncoding)
			{
				if (contentLength > 0L && expectedLength <= contentLength)
				{
					expectedLength = -1L;
				}
			}
			else
			{
				expectedLength = contentLength;
			}

			ContentLength = contentLength;
			ExpectedLength = expectedLength;
			TotalRead = 0L;
			TotalWrite = 0L;
			UpdateDateTime();
		}

		internal void OnRead(int length)
		{
			TotalRead += length;
			UpdateDateTime();
		}

		internal void OnWrite(int length)
		{
			TotalWrite += length;
			UpdateDateTime();
		}

		internal void OnReadTotal(long length)
		{
			if (length > TotalRead)
			{
				TotalRead = length;
				UpdateDateTime();
			}
		}

		internal void OnWriteTotal(long length)
		{
			if (length > TotalWrite)
			{
				TotalWrite = length;
				UpdateDateTime();
			}
		}

		internal void OnComplete()
		{
			Loading = false;
			UpdateDateTime();
		}

		internal void UpdateDateTime()
		{
			lastDateTime = DateTime.UtcNow;
		}
	}
}