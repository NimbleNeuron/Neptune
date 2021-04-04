using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using Logger = Neptune.Log.Logger;

namespace Neptune.Http
{
	public class HttpResponseBody : IDisposable
	{
		private bool completed;
		private readonly bool computeHash;

		private string contentType;
		private bool disposed;
		private long downloadedLength;
		private string downloadToFile;
		private HashAlgorithm hash;
		private readonly Func<HashAlgorithm> hashAlgorithm;
		private readonly HttpResponseProgress progress;
		private object responseJson;
		private string responseText;
		private Texture2D responseTexture;

		private Stream stream;
		private string tempFilePath;

		internal HttpResponseBody(HttpResponseProgress progress, bool computeHash, Func<HashAlgorithm> hashAlgorithm)
		{
			this.progress = progress;
			this.computeHash = computeHash;
			this.hashAlgorithm = hashAlgorithm;
		}

		public string ResponseHash { get; private set; }

		public bool IsCompleted => completed;

		public string ResponseText {
			get
			{
				if (responseText != null)
				{
					return responseText;
				}

				if (!completed)
				{
					return null;
				}

				if (downloadedLength > 2147483647L)
				{
					throw new Exception("Too large content : length=" + downloadedLength);
				}

				if (downloadedLength == 0L)
				{
					responseText = string.Empty;
					return responseText;
				}

				if (!string.IsNullOrEmpty(downloadToFile))
				{
					using (StreamReader streamReader = new StreamReader(downloadToFile, Encoding.UTF8))
					{
						responseText = streamReader.ReadToEnd();
						goto IL_D1;
					}
				}

				if (stream != null && stream is MemoryStream)
				{
					MemoryStream memoryStream = stream as MemoryStream;
					responseText = Encoding.UTF8.GetString(memoryStream.GetBuffer(), 0, (int) memoryStream.Length);
				}

				IL_D1:
				return responseText;
			}
		}

		public virtual Texture2D ResponseTexture {
			get
			{
				if (responseTexture != null)
				{
					return responseTexture;
				}

				if (!completed)
				{
					return null;
				}

				if (downloadedLength == 0L)
				{
					return null;
				}

				if (downloadedLength > 2147483647L)
				{
					throw new Exception("Too large content : length=" + downloadedLength);
				}

				responseTexture = new Texture2D(0, 0, TextureFormat.ARGB32, false);
				if (!string.IsNullOrEmpty(downloadToFile))
				{
					using (FileStream fileStream = File.OpenRead(downloadToFile))
					{
						using (BinaryReader binaryReader = new BinaryReader(fileStream))
						{
							responseTexture.LoadImage(binaryReader.ReadBytes((int) downloadedLength));
							return responseTexture;
						}
					}
				}

				if (stream != null && stream is MemoryStream)
				{
					MemoryStream memoryStream = stream as MemoryStream;

					if (memoryStream.GetBuffer().Length == memoryStream.Length)
					{
						responseTexture.LoadImage(memoryStream.GetBuffer());
					}
					else
					{
						responseTexture.LoadImage(memoryStream.ToArray());
					}
				}

				return responseTexture;
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		public virtual byte[] GetBytes()
		{
			if (!string.IsNullOrEmpty(downloadToFile))
			{
				using (FileStream fileStream = File.OpenRead(downloadToFile))
				{
					using (BinaryReader binaryReader = new BinaryReader(fileStream))
					{
						return binaryReader.ReadBytes((int) downloadedLength);
					}
				}
			}

			return null;
		}

		public T ResponseJson<T>()
		{
			if (!completed)
			{
				return default;
			}

			if (downloadedLength == 0L)
			{
				return default;
			}

			if (downloadedLength > 2147483647L)
			{
				throw new Exception("Too large content : length=" + downloadedLength);
			}

			if (contentType != null && contentType.Contains("application/json"))
			{
				if (responseText != null)
				{
					responseJson = JsonConvert.DeserializeObject<T>(responseText, new JsonSerializerSettings
					{
						DateFormatString = "d MMMM, yyyy",
						Formatting = Formatting.Indented
					});
				}
				else
				{
					if (!string.IsNullOrEmpty(downloadToFile))
					{
						using (StreamReader streamReader = new StreamReader(downloadToFile, Encoding.UTF8))
						{
							responseText = streamReader.ReadToEnd();
							responseJson = JsonConvert.DeserializeObject<T>(responseText);

							return (T) responseJson;
						}
					}

					if (stream != null && stream is MemoryStream)
					{
						MemoryStream memoryStream = stream as MemoryStream;
						memoryStream.Seek(0L, SeekOrigin.Begin);
						using (StreamReader streamReader2 = new StreamReader(memoryStream, Encoding.UTF8))
						{
							responseJson = JsonConvert.DeserializeObject<T>(streamReader2.ReadToEnd());
						}
					}
				}

				return (T) responseJson;
			}

			return default;
		}

		~HttpResponseBody()
		{
			Dispose(false);
		}

		internal void DownloadToMemory(string contentType, long expectedContentLength)
		{
			if (expectedContentLength > 2147483647L)
			{
				throw new ArgumentOutOfRangeException("Too large expectedContentLength : " + expectedContentLength);
			}

			int num = (int) expectedContentLength;
			Create(contentType, new MemoryStream(num > 0 ? num : 0));
		}

		internal void DownloadToFile(string downloadTo, string tempFilePath, string contentType, bool createTempFile)
		{
			string directoryName = Path.GetDirectoryName(tempFilePath);
			if (!Directory.Exists(directoryName))
			{
				Directory.CreateDirectory(directoryName);
			}

			Create(contentType, !createTempFile ? null : File.OpenWrite(tempFilePath));
			this.tempFilePath = tempFilePath;
			downloadToFile = downloadTo;
		}

		private void Create(string contentType, Stream stream)
		{
			Cancel();
			this.contentType = contentType;
			this.stream = stream;
			if (computeHash && hashAlgorithm != null)
			{
				hash = hashAlgorithm();
			}
		}

		internal void Write(byte[] buffer, int length)
		{
			if (stream != null && length > 0)
			{
				stream.Write(buffer, 0, length);
				downloadedLength += length;
				if (hash != null)
				{
					hash.TransformBlock(buffer, 0, length, buffer, 0);
				}

				progress.OnWrite(length);
			}
		}

		internal void SetCompleted(string md5)
		{
			if (!completed)
			{
				if (hash != null)
				{
					if (downloadedLength > 0L)
					{
						hash.TransformFinalBlock(new byte[1], 0, 0);
						ResponseHash = BitConverter.ToString(hash.Hash).Replace("-", string.Empty).ToLower();
					}

					hash.Clear();
					hash = null;
				}
				else
				{
					ResponseHash = md5;
				}

				if (!string.IsNullOrEmpty(downloadToFile))
				{
					if (stream != null)
					{
						stream.Flush();
						stream.Dispose();
						stream = null;
					}
					else if (!File.Exists(tempFilePath))
					{
						throw new Exception("Temp file not found!");
					}

					if (File.Exists(downloadToFile))
					{
						File.Delete(downloadToFile);
					}

					Debug.Log("[HttpResponseBody] FileMove");
					File.Move(tempFilePath, downloadToFile);
				}

				completed = true;
			}
		}

		private void Cancel()
		{
			try
			{
				if (stream != null)
				{
					stream.Dispose();
				}

				if (hash != null)
				{
					hash.Clear();
				}

				if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
				{
					File.Delete(tempFilePath);
				}
			}
			catch (Exception ex)
			{
				Logger.Exception(ex);
			}
			finally
			{
				downloadedLength = 0L;
				contentType = null;
				tempFilePath = null;
				downloadToFile = null;
				stream = null;
				hash = null;
			}
		}

		private void Dispose(bool disposing)
		{
			if (disposed)
			{
				return;
			}

			try
			{
				if (!string.IsNullOrEmpty(tempFilePath) && File.Exists(tempFilePath))
				{
					if (stream != null)
					{
						stream.Dispose();
					}

					File.Delete(tempFilePath);
				}
			}
			catch (Exception ex)
			{
				Logger.Exception(ex);
			}

			if (disposing)
			{
				if (stream != null && !(stream is MemoryStream))
				{
					stream.Dispose();
				}

				if (hash != null)
				{
					hash.Clear();
				}
			}

			disposed = true;
		}
	}
}