using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;
using Logger = Neptune.Log.Logger;

namespace Neptune.Http
{
	public static class HttpRequestFactory
	{
		private static readonly List<Header> headers = new List<Header>();


		public static bool KeepAlive = true;


		public static bool UseCaches = false;


		public static int Timeout = 30000;


		public static int MaxRetry = 1;

		public static HttpRequest Open(string method, string url, object tag = null)
		{
			ServicePointManager.ServerCertificateValidationCallback = Validator;
			HttpRequest request = null;
			request = new NetHttpRequest(method, url);
			Uri uri = new Uri(url);

			Logger.Log("<color=#86E57F>[API: SEND][{1}] {0}</color>", url, method.ToUpper());

			headers.FindAll(header => header.Matches(uri)).ForEach(delegate(Header header)
			{
				request.SetRequestHeader(header.headerName, header.headerValue);
			});

			request.KeepAlive = KeepAlive;
			request.UseCaches = UseCaches;
			request.Timeout = Timeout;
			request.MaxRetry = MaxRetry;
			request.Tag = tag;
			return request;
		}


		private static HttpRequest GetInternal(string url, object tag = null)
		{
			HttpRequest httpRequest = Open("GET", url, tag);
			httpRequest.Send();
			return httpRequest;
		}


		public static Func<HttpRequest> Get(string url, object tag = null)
		{
			return () => GetInternal(url, tag);
		}


		private static HttpRequest Delete(string url, object tag = null)
		{
			HttpRequest httpRequest = Open("DELETE", url, tag);
			httpRequest.Send();
			return httpRequest;
		}


		private static HttpRequest Put(string url, object data, object tag = null)
		{
			HttpRequest httpRequest = Open("PUT", url, tag);
			httpRequest.SendObject(data);
			return httpRequest;
		}


		private static HttpRequest PostInternal(string url, object data, object tag = null)
		{
			HttpRequest httpRequest = Open("POST", url, tag);
			httpRequest.SendObject(data);
			return httpRequest;
		}


		public static Func<HttpRequest> Post(string url, object data, object tag = null)
		{
			return () => PostInternal(url, data, tag);
		}


		public static void GetHeaders(string url, IDictionary<string, string> headers)
		{
			Uri uri = new Uri(url);
			HttpRequestFactory.headers.FindAll(header => header.Matches(uri)).ForEach(delegate(Header header)
			{
				headers[header.headerName] = header.headerValue;
			});
		}


		private static void SetHeader(Header header)
		{
			Header header2 = headers.Find(h => h.Matches(header));
			if (header2 == null)
			{
				if (header.headerValue != null)
				{
					headers.Add(header);
				}

				return;
			}

			if (header.headerValue != null)
			{
				header2.headerValue = header.headerValue;
				return;
			}

			headers.Remove(header2);
		}


		public static void SetHeader(string hostName, int port, string headerName, string headerValue)
		{
			SetHeader(new Header
			{
				hostName = hostName,
				port = port,
				headerName = headerName,
				headerValue = headerValue
			});
		}


		public static void SetHeader(string hostName, string headerName, string headerValue)
		{
			SetHeader(new Header
			{
				hostName = hostName,
				headerName = headerName,
				headerValue = headerValue
			});
		}


		public static void SetHeader(string headerName, string headerValue)
		{
			SetHeader(new Header
			{
				headerName = headerName,
				headerValue = headerValue
			});
		}


		public static bool Validator(object sender, X509Certificate certificate, X509Chain chain,
			SslPolicyErrors sslPolicyErrors)
		{
			return true;
		}


		public static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain,
			SslPolicyErrors sslPolicyErrors)
		{
			if (sslPolicyErrors == SslPolicyErrors.None)
			{
				return true;
			}

			Debug.Log("Certificate error: " + sslPolicyErrors);
			return false;
		}


		public static bool CheckValidationResult(ServicePoint sp, X509Certificate certificate, WebRequest request,
			int error)
		{
			return error == 0;
		}


		public static void CheckValidate() { }


		private class Header
		{
			internal string headerName;


			internal string headerValue;


			internal string hostName;


			internal int? port;

			internal bool Matches(Uri uri)
			{
				return (hostName == null || !(hostName != uri.Host)) && (port == null || port.Value == uri.Port);
			}


			internal bool Matches(Header header)
			{
				if (hostName != header.hostName)
				{
					return false;
				}

				int? num = port;
				int? num2 = header.port;
				return (num.GetValueOrDefault() == num2.GetValueOrDefault()) & (num != null == (num2 != null)) &&
				       !(headerName != header.headerName);
			}
		}
	}
}