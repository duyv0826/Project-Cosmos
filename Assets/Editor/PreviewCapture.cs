using System.IO;
using Cosmos.Core;
using Cosmos.View;
using UnityEditor;
using UnityEngine;

namespace Cosmos.EditorTools
{
    /// <summary>
    /// 验证用：在编辑器里直接搭出画面并渲染成 PNG，不进入 Play 模式
    /// （批处理下 Play 模式会挂住）。不参与玩法。
    /// 命令行：Unity.exe -batchmode -projectPath ... -executeMethod Cosmos.EditorTools.PreviewCapture.Run
    /// </summary>
    public static class PreviewCapture
    {
        private const int Width = 1280;
        private const int Height = 720;
        private const string OutDir = "C:/Users/duyv/AppData/Local/Temp/";

        public static void Run()
        {
            Shoot("cosmos_preview_opening.png", new ShadowSyncSim(ShadowRoom.Milestone1()), -1f);
            Shoot("cosmos_preview_sync_peak.png", SyncSim(), 0.02f);
            Shoot("cosmos_preview_sync_after.png", SyncSim(), 0.40f);
            Shoot("cosmos_preview_end.png", EndSim(), 0.12f);
            EditorApplication.Exit(0);
        }

        private static ShadowSyncSim SyncSim()
        {
            var sim = new ShadowSyncSim(ShadowRoom.Milestone1());
            sim.TryMove(GridPos.Right);
            sim.TryMove(GridPos.Right);
            sim.TryMove(GridPos.Up);
            sim.TryMove(GridPos.Right); // 拍 4：玩家踩 A，影子落 B
            return sim;
        }

        private static ShadowSyncSim EndSim()
        {
            var sim = SyncSim();
            sim.TryMove(GridPos.Left);
            sim.TryMove(GridPos.Up);   // 门
            sim.TryMove(GridPos.Up);   // 光点
            return sim;
        }

        private static void Shoot(string fileName, ShadowSyncSim sim, float burstAt)
        {
            var view = ShadowRoomView.SpawnForPreview(sim);
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[Cosmos] 没找到主相机，截图跳过");
                return;
            }

            if (burstAt >= 0f)
            {
                var at = sim.ReachedLight ? sim.Room.Light : sim.Room.Door;
                view.PreviewBurstAt(new Vector3(at.X, at.Y, 0f), burstAt);
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

            File.WriteAllBytes(OutDir + fileName, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(view.gameObject);

            Debug.Log("[Cosmos] 截图已保存 " + OutDir + fileName);
        }
    }
}
