using UnityEngine;

namespace Cosmos.View
{
    /// <summary>
    /// 兜底启动：即使打开的是空白场景，按 Play 也能直接玩。
    /// 这样"只是一次尝试"的工程不需要维护场景资产。
    /// </summary>
    public static class CosmosBoot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureBootstrap()
        {
            if (Object.FindFirstObjectByType<ShadowRoomView>() != null) return;

            var go = new GameObject("ShadowRoom");
            go.AddComponent<ShadowRoomView>();
        }
    }
}
