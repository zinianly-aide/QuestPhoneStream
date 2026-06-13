using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace QuestPhoneStream
{
    public sealed class UnityMainThread : MonoBehaviour
    {
        private static readonly ConcurrentQueue<Action> Queue = new ConcurrentQueue<Action>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            var go = new GameObject("QuestPhoneStreamMainThread");
            DontDestroyOnLoad(go);
            go.AddComponent<UnityMainThread>();
        }

        public static void Enqueue(Action action)
        {
            Queue.Enqueue(action);
        }

        private void Update()
        {
            while (Queue.TryDequeue(out var action))
            {
                action.Invoke();
            }
        }
    }
}
