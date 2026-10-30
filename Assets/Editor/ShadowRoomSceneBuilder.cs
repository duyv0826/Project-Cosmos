using System.IO;
using Cosmos.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Cosmos.EditorTools
{
    /// <summary>一键生成可打开的关卡场景。菜单：Cosmos / 生成影子之室场景。</summary>
    public static class ShadowRoomSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/ShadowRoom.unity";

        [MenuItem("Cosmos/生成影子之室场景")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var go = new GameObject("ShadowRoom");
            go.AddComponent<ShadowRoomView>();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();

            Debug.Log("[Cosmos] 场景已生成：" + ScenePath);
        }
    }
}
