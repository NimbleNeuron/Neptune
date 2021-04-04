using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

namespace Neptune.Http
{
	public class KeyValueList : List<KeyValuePair<string, object>>
	{
		public KeyValueList() { }


		public KeyValueList(List<KeyValuePair<string, object>> list) : base(list) { }


		public KeyValueList(string query)
		{
			foreach (string text in query.Split('&'))
			{
				int num = text.IndexOf('=');
				if (num >= 0)
				{
					Add(UnityWebRequest.UnEscapeURL(text.Substring(0, num)),
						UnityWebRequest.UnEscapeURL(text.Substring(num + 1)));
				}
				else
				{
					Add(UnityWebRequest.UnEscapeURL(text), null);
				}
			}
		}

		public KeyValueList(IDictionary dict)
		{
			foreach (object obj in dict.Keys)
			{
				Add(obj as string, dict[obj]);
			}
		}

		public KeyValueList(params object[] args)
		{
			int num = args.Length;
			for (int i = 0; i < num; i += 2)
			{
				Add(args[i] as string, i + 1 >= num ? null : args[i + 1]);
			}
		}

		public List<KeyValuePair<string, string>> ToStringValueList()
		{
			List<KeyValuePair<string, string>> keyValuePairList = new List<KeyValuePair<string, string>>();
			foreach (KeyValuePair<string, object> keyValuePair in this)
			{
				object obj1 = keyValuePair.Value;
				if (obj1 != null && obj1 is IList)
				{
					foreach (object obj2 in obj1 as IList)
					{
						keyValuePairList.Add(new KeyValuePair<string, string>(keyValuePair.Key,
							obj2 == null ? null : obj2.ToString()));
					}
				}
				else
				{
					keyValuePairList.Add(new KeyValuePair<string, string>(keyValuePair.Key,
						obj1 == null ? null : obj1.ToString()));
				}
			}

			return keyValuePairList;
		}

		public WWWForm ToForm()
		{
			WWWForm wwwform = new WWWForm();
			foreach (KeyValuePair<string, string> keyValuePair in ToStringValueList())
			{
				wwwform.AddField(keyValuePair.Key, keyValuePair.Value);
			}

			return wwwform;
		}

		public string ToQuery()
		{
			return string.Join("&",
				ToStringValueList().ConvertAll<string>(p => string.Format("{0}={1}", UnityWebRequest.EscapeURL(p.Key),
					p.Value == null ? string.Empty : UnityWebRequest.EscapeURL(p.Value))).ToArray());
		}

		public Hashtable ToHashtable()
		{
			Hashtable hashtable = new Hashtable();
			foreach (KeyValuePair<string, object> keyValuePair in this)
			{
				if (hashtable.ContainsKey(keyValuePair.Key))
				{
					object obj = hashtable[keyValuePair.Key];
					if (obj != null && obj is IList)
					{
						((IList) obj).Add(keyValuePair.Value);
					}
					else
					{
						List<object> list = new List<object>();
						list.Add(obj);
						list.Add(keyValuePair.Value);
						hashtable[keyValuePair.Key] = list;
					}
				}
				else
				{
					hashtable.Add(keyValuePair.Key, keyValuePair.Value);
				}
			}

			return hashtable;
		}

		public void Add(string key, object value)
		{
			base.Add(new KeyValuePair<string, object>(key, value));
		}

		public void Set(string key, string value)
		{
			int num = FindLastIndex(m => m.Key == key);
			if (num < 0)
			{
				Add(key, value);
				return;
			}

			base[num] = new KeyValuePair<string, object>(key, value);
		}

		public object Get(string key)
		{
			int num = FindIndex(m => m.Key == key);
			if (num >= 0)
			{
				return base[num].Value;
			}

			return null;
		}

		public object GetLast(string key)
		{
			int num = FindLastIndex(m => m.Key == key);
			if (num >= 0)
			{
				return base[num].Value;
			}

			return null;
		}

		public List<object> GetAll(string key)
		{
			return FindAll(m => m.Key == key).ConvertAll<object>(p => p.Value);
		}
	}
}