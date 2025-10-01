using System.Collections;
using UnityEngine;

namespace MapEditorExtension
{
    public class CoroutineRunner : MonoBehaviour
    {
        private static CoroutineRunner _instance;
        public static CoroutineRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject runnerObject = new("CoroutineRunner");
                    _instance = runnerObject.AddComponent<CoroutineRunner>();
                    DontDestroyOnLoad(runnerObject);
                }
                return _instance;
            }
        }
        public static void Run(IEnumerator coroutine)
        {
            Instance.StartCoroutine(coroutine);
        }
    }
}
