using System.Collections;
using Blis.Common;

namespace Neptune.WebSocket
{
	public class WebSocketUpdater : SingletonMonoBehaviour<WebSocketUpdater>
	{
		internal long PauseCount;


		private void OnApplicationPause(bool pauseStatus)
		{
			PauseCount += 1L;
		}

		protected override void OnAwakeSingleton()
		{
			DontDestroyOnLoad(this);
		}


		public void UpdateState(WebSocket ws, IEnumerator enumerator)
		{
			StartCoroutine(enumerator);
		}
	}
}