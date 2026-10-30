using System.IO;
using Cosmos.Core;
using Cosmos.View;
using UnityEditor;
using UnityEngine;

namespace Cosmos.EditorTools
{
    /// <summary>
    /// 验证用：在编辑器里直接搭出某一拍的画面并渲染成 PNG，不进入 Play 模式
    /// （批处理下 Play 模式会挂住）。不参与玩法。
    /// 命令行：Unity.exe -batchmode -projectPath ... -executeMethod Cosmos.EditorTools.PreviewCapture.Run
    /// </summary>
    public static class PreviewCapture
    {
        private const int Width = 1280;
        private const int Height = 720;

        public static void Run()
        {
            CaptureOpening();
            CaptureSyncMoment();
            EditorApplication.Exit(0);
        }

        private static void CaptureOpening()
        {
            var sim = new ShadowSyncSim(ShadowRoom.Milestone1());
            Shoot(sim, "C:/Users/duyv/AppData/Local/Temp/cosmos_preview_opening.png");
        }

        private static void CaptureSyncMoment()
        {
            var sim = new ShadowSyncSim(ShadowRoom.Milestone1());
            sim.TryMove(GridPos.Right);
            sim.TryMove(GridPos.Right);
            sim.TryMove(GridPos.Up);
            sim.TryMove(GridPos.Right); // 拍 4：玩家踩 A，影子落 B
            Shoot(sim, "C:/Users/duyv/AppData/Local/Temp/cosmos_preview_sync.png");
        }

        private static void Shoot(ShadowSyncSim sim, string path)
        {
            var view = ShadowRoomView.SpawnForPreview(sim);
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[Cosmos] 没找到主相机，截图跳过");
                return;
            }

            var rt = new RenderTexture(Width, Height, 24);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();

            RenderTexture.active = null;
            cam.targetTexture = null;

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(view.gameObject);

            Debug.Log("[Cosmos] 截图已保存 " + path);
        }
    }
}
