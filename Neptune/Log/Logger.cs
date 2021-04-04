using System;
using UnityEngine;

namespace Neptune.Log
{
	public static class Logger
	{
		public delegate void ExceptionFunc(Exception ex);

		public delegate void LogFunc(object message, params object[] args);

		private static LogLevel logLevel;
		public static LogFunc Log;
		public static LogFunc Error;
		public static LogFunc Warning;
		public static ExceptionFunc Exception;

		static Logger()
		{
			if (Debug.isDebugBuild)
			{
				LogLevel = LogLevel.All;
			}
		}

		public static LogLevel LogLevel {
			get => logLevel;
			set
			{
				logLevel = value;

				if (IsEnabled(LogLevel.Log))
				{
					Log = LogLog;
				}
				else
				{
					Log = LogNone;
				}

				if (IsEnabled(LogLevel.Warning))
				{
					Warning = LogWarning;
				}
				else
				{
					Warning = LogNone;
				}

				if (IsEnabled(LogLevel.Error))
				{
					Error = LogError;
				}
				else
				{
					Error = LogNone;
				}

				if (IsEnabled(LogLevel.Exception))
				{
					Exception = LogException;
					return;
				}

				Exception = LogNoneEx;
			}
		}

		private static bool IsEnabled(LogLevel level)
		{
			return (logLevel & level) == level;
		}

		private static object Message(object message, params object[] args)
		{
			if (args.Length != 0 && message is string)
			{
				return string.Format(message as string, args);
			}

			return message;
		}

		private static void LogNone(object message, params object[] args) { }

		private static void LogNoneEx(Exception ex) { }

		private static void LogLog(object message, params object[] args)
		{
			Debug.Log(Message(message, args));
		}

		private static void LogError(object message, params object[] args)
		{
			Debug.LogError(Message(message, args));
		}

		private static void LogWarning(object message, params object[] args)
		{
			Debug.LogWarning(Message(message, args));
		}

		private static void LogException(Exception ex)
		{
			Debug.LogException(ex);
		}

		private static string GetLogTextColor(string _Text, byte _ColorR, byte _ColorG, byte _ColorB)
		{
			return GetLogTextColor(_Text, new Color32(_ColorR, _ColorG, _ColorB, byte.MaxValue));
		}

		private static string GetLogTextColor(string _Text, Color32 _Color)
		{
			_Text = string.Format("<color=#{1:x}{2:x}{3:x}>{0}</color>", _Text, _Color.r, _Color.g, _Color.b);
			return _Text;
		}
	}
}