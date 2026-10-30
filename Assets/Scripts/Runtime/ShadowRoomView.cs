using System.Collections.Generic;
using Cosmos.Core;
using UnityEngine;

namespace Cosmos.View
{
    /// <summary>
    /// 表现层：把 <see cref="ShadowSyncSim"/> 的权威状态画成画面，并读取键盘。
    /// 只读模拟、不反向写——所有规则都在 Core 里，这里只负责"看起来对不对"。
    /// 刻意不接美术资源：全部用 1×1 白图缩放成方块，方便随时推翻重做。
    /// </summary>
    public sealed class ShadowRoomView : MonoBehaviour
    {
        [Header("移动")]
        [Tooltip("一拍走一格，找手感时改这里")]
        public float stepSeconds = 0.09f;

        private static readonly Color BgColor = new Color32(0x0B, 0x0C, 0x10, 0xFF);
        private static readonly Color FloorColor = new Color32(0x0F, 0x11, 0x17, 0xFF);
        private static readonly Color WallColor = new Color32(0x2A, 0x2F, 0x3C, 0xFF);
        private static readonly Color PlayerColor = new Color32(0xE8, 0xEC, 0xF2, 0xFF);
        private static readonly Color ShadowColor = new Color32(0x6E, 0x8F, 0xC4, 0x7A);
        private static readonly Color SwitchADim = new Color32(0x4A, 0x3A, 0x22, 0xFF);
        private static readonly Color SwitchALit = new Color32(0xD9, 0xA4, 0x41, 0xFF);
        private static readonly Color SwitchBDim = new Color32(0x22, 0x33, 0x4A, 0xFF);
        private static readonly Color SwitchBLit = new Color32(0xB9, 0xCD, 0xE6, 0xFF);
        private static readonly Color DoorLocked = new Color32(0x2A, 0x2F, 0x3A, 0xFF);
        private static readonly Color DoorOpen = new Color32(0xC8, 0x8B, 0x2E, 0xFF);
        private static readonly Color LightColor = new Color32(0xFF, 0xE3, 0xA8, 0xFF);

        private ShadowSyncSim _sim;
        private Sprite _sprite;
        private Sprite _glowSprite;

        private Transform _player;
        private Transform _shadow;
        private Transform _switchA;
        private Transform _switchB;
        private Transform _door;
        private Transform _light;
        private Transform _burstCore;
        private Transform _burstRing;
        private float _burstElapsed = -1f;
        private float _burstDuration = 1.2f;
        private float _freeze;
        private float _stepPulse;

        private Vector3 _playerTarget;
        private Vector3 _shadowTarget;

        private Font _font;
        private GUIStyle _hud;
        private GUIStyle _hint;
        private GUIStyle _big;
        private bool _debug;

        private void Awake()
        {
            Initialize(null);
        }

        /// <summary>表现层初始化。传入 sim 可让编辑器预览直接落在某一拍的状态上。</summary>
        public void Initialize(ShadowSyncSim sim)
        {
            _sprite = MakeUnitSprite();
            _glowSprite = MakeRadialSprite();
            _sim = sim ?? new ShadowSyncSim(ShadowRoom.Milestone1());
            _sim.Emitted += OnSimEvent;

            BuildCamera();
            BuildRoom();
            BuildActors();

            _playerTarget = ToWorld(_sim.PlayerPos);
            _shadowTarget = ToWorld(_sim.ShadowPos);
            _player.position = _playerTarget;
            _shadow.position = _shadowTarget;
            SyncColors();
        }

#if UNITY_EDITOR
        /// <summary>仅供预览渲染：在编辑器里直接搭出画面，不进入 Play。</summary>
        public static ShadowRoomView SpawnForPreview(ShadowSyncSim sim)
        {
            var go = new GameObject("CosmosPreview");
            var view = go.AddComponent<ShadowRoomView>();
            view.Initialize(sim);
            return view;
        }
#endif

        private void OnDestroy()
        {
            if (_sim != null) _sim.Emitted -= OnSimEvent;
        }

        private void Update()
        {
            HandleInput();
            Animate();
            SyncColors();
        }

        // ---------- 输入 ----------

        private void HandleInput()
        {
            if (Down(KeyCode.R))
            {
                _sim.Reset();
                _playerTarget = ToWorld(_sim.PlayerPos);
                _shadowTarget = _playerTarget;
                return;
            }

            // 同步那一瞬间把输入掐住 0.2 秒：让这一拍有重量，而不是走过去的路上顺手开了个门
            if (Down(KeyCode.F1)) _debug = !_debug;
            if (_freeze > 0f) return;

            if (Down(KeyCode.W) || Down(KeyCode.UpArrow)) TryStep(GridPos.Up);
            else if (Down(KeyCode.S) || Down(KeyCode.DownArrow)) TryStep(GridPos.Down);
            else if (Down(KeyCode.A) || Down(KeyCode.LeftArrow)) TryStep(GridPos.Left);
            else if (Down(KeyCode.D) || Down(KeyCode.RightArrow)) TryStep(GridPos.Right);
        }

        private void TryStep(GridPos dir)
        {
            // TryMove 返回 false 时这一拍不消耗：位置不动，视觉自然不动
            _sim.TryMove(dir);
        }

        private static bool Down(KeyCode key)
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key);
#elif ENABLE_INPUT_SYSTEM
            var k = key switch
            {
                KeyCode.W => UnityEngine.InputSystem.Key.W,
                KeyCode.A => UnityEngine.InputSystem.Key.A,
                KeyCode.S => UnityEngine.InputSystem.Key.S,
                KeyCode.D => UnityEngine.InputSystem.Key.D,
                KeyCode.R => UnityEngine.InputSystem.Key.R,
                KeyCode.UpArrow => UnityEngine.InputSystem.Key.UpArrow,
                KeyCode.DownArrow => UnityEngine.InputSystem.Key.DownArrow,
                KeyCode.LeftArrow => UnityEngine.InputSystem.Key.LeftArrow,
                KeyCode.RightArrow => UnityEngine.InputSystem.Key.RightArrow,
                _ => UnityEngine.InputSystem.Key.None,
            };
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return k != UnityEngine.InputSystem.Key.None && kb != null && kb[k].wasPressedThisFrame;
#else
            return false;
#endif
        }

        // ---------- 事件 ----------

        private void OnSimEvent(SimEvent e)
        {
            switch (e)
            {
                case SimEvent.Step:
                    _playerTarget = ToWorld(_sim.PlayerPos);
                    _shadowTarget = ToWorld(_sim.ShadowPos);
                    _stepPulse = 1f;
                    break;

                case SimEvent.Sync:
                    StartBurst(ToWorld(_sim.Room.Door), 1.25f);
                    _freeze = 0.2f;
                    break;

                case SimEvent.ReachedLight:
                    StartBurst(ToWorld(_sim.Room.Light), 1.8f);
                    _freeze = 0.5f;
                    break;

                case SimEvent.Reset:
                    _playerTarget = ToWorld(_sim.PlayerPos);
                    _shadowTarget = _playerTarget;
                    _burstElapsed = -1f;
                    _burstCore.gameObject.SetActive(false);
                    _burstRing.gameObject.SetActive(false);
                    _freeze = 0f;
                    break;
            }
        }

        // ---------- 每帧表现 ----------

        private void Animate()
        {
            float dt = Time.deltaTime;

            if (_freeze > 0f) _freeze -= dt;

            // 逐格移动也要有"落格"的顿感，不要瞬移
            float t = stepSeconds <= 0f ? 1f : Mathf.Clamp01(dt / stepSeconds);
            _player.position = Vector3.Lerp(_player.position, _playerTarget, t);
            _shadow.position = Vector3.Lerp(_shadow.position, _shadowTarget, t);

            // 每落一格弹一下：让"拍"变成可数的事件，而不是瞬移
            _stepPulse = Mathf.Max(0f, _stepPulse - dt / 0.14f);
            float bump = 1f + 0.10f * _stepPulse * _stepPulse;
            _player.localScale = new Vector3(0.6f * bump, 0.6f * bump, 1f);
            _shadow.localScale = new Vector3(0.52f * bump, 0.52f * bump, 1f);

            // 光点呼吸；通关后放大一次
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.2f);
            float lightScale = 0.85f + 0.08f * pulse + (_sim.ReachedLight ? 0.6f : 0f);
            _light.localScale = new Vector3(lightScale, lightScale, 1f);

            TickBurst(dt);
        }

        // ---------- 同步光晕 ----------

        private void StartBurst(Vector3 worldPos, float duration)
        {
            _burstDuration = duration;
            _burstElapsed = 0f;
            _burstCore.position = worldPos;
            _burstRing.position = worldPos;
            RenderBurst(0f);
        }

        private void TickBurst(float dt)
        {
            if (_burstElapsed < 0f) return;

            _burstElapsed += dt;
            RenderBurst(_burstElapsed);

            if (_burstElapsed >= _burstDuration)
            {
                _burstElapsed = -1f;
                _burstCore.gameObject.SetActive(false);
                _burstRing.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 两段式：0~0.18 秒亮芯炸开（先给一个"被击中"的感觉），
        /// 0.08~1.1 秒大环慢慢散掉（余温，不是烟花）。
        /// </summary>
        private void RenderBurst(float time)
        {
            float coreK = Mathf.Clamp01(time / 0.18f);
            _burstCore.gameObject.SetActive(coreK < 1f);
            float coreScale = Mathf.Lerp(1.2f, 3.6f, coreK);
            _burstCore.localScale = new Vector3(coreScale, coreScale, 1f);
            var coreColor = LightColor;
            coreColor.a = 0.95f * (1f - coreK) * (1f - coreK);
            _burstCore.GetComponent<SpriteRenderer>().color = coreColor;

            float ringK = Mathf.Clamp01((time - 0.08f) / 1.02f);
            _burstRing.gameObject.SetActive(ringK > 0f && ringK < 1f);
            float ringScale = Mathf.Lerp(1.4f, 8.0f, ringK);
            _burstRing.localScale = new Vector3(ringScale, ringScale, 1f);
            var ringColor = LightColor;
            ringColor.a = 0.38f * (1f - ringK) * (1f - ringK);
            _burstRing.GetComponent<SpriteRenderer>().color = ringColor;
        }

#if UNITY_EDITOR
        /// <summary>预览用：把光晕定格在某一时刻，供批处理渲图。</summary>
        public void PreviewBurstAt(Vector3 worldPos, float time)
        {
            _burstCore.position = worldPos;
            _burstRing.position = worldPos;
            RenderBurst(time);
        }
#endif

        private void SyncColors()
        {
            bool aPressed = _sim.PlayerPos == _sim.Room.PlayerSwitch;
            bool bPressed = _sim.ShadowPos == _sim.Room.ShadowSwitch;

            _switchA.GetComponent<SpriteRenderer>().color = aPressed ? SwitchALit : SwitchADim;
            _switchB.GetComponent<SpriteRenderer>().color = bPressed ? SwitchBLit : SwitchBDim;
            _door.GetComponent<SpriteRenderer>().color = _sim.DoorLatched ? DoorOpen : DoorLocked;
        }

        // ---------- 搭建 ----------

        private void BuildCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera");
                go.tag = "MainCamera";
                cam = go.AddComponent<Camera>();
            }

            cam.orthographic = true;
            cam.orthographicSize = 2.45f;
            cam.transform.position = new Vector3(1.5f, 1.5f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = BgColor;
            cam.transform.rotation = Quaternion.identity;
        }

        private void BuildRoom()
        {
            var root = new GameObject("Room").transform;
            root.SetParent(transform, false);

            for (int y = 0; y < _sim.Room.Height; y++)
            {
                for (int x = 0; x < _sim.Room.Width; x++)
                {
                    var p = new GridPos(x, y);
                    var go = MakeQuad("Cell" + p, root, _sim.Room.IsWall(p) ? WallColor : FloorColor, 0);
                    go.transform.position = ToWorld(p);
                    go.transform.localScale = new Vector3(0.96f, 0.96f, 1f);
                }
            }

            // 格线：让"一拍走一格"这件事在画面上成立，否则玩家无从计算
            var gridColor = new Color32(0x1B, 0x20, 0x29, 0xFF);
            for (int i = 0; i <= _sim.Room.Width; i++)
            {
                float x = -0.5f + i;
                MakeEdge("GridV" + i, root, gridColor, new Vector3(x, 1.5f, 0f), new Vector3(0.012f, 4.0f, 1f));
            }
            for (int i = 0; i <= _sim.Room.Height; i++)
            {
                float y = -0.5f + i;
                MakeEdge("GridH" + i, root, gridColor, new Vector3(1.5f, y, 0f), new Vector3(4.0f, 0.012f, 1f));
            }

            _switchA = MakeQuad("SwitchA", root, SwitchADim, 1).transform;
            _switchA.position = ToWorld(_sim.Room.PlayerSwitch);
            _switchA.localScale = new Vector3(0.52f, 0.52f, 1f);

            _switchB = MakeQuad("SwitchB", root, SwitchBDim, 1).transform;
            _switchB.position = ToWorld(_sim.Room.ShadowSwitch);
            _switchB.localScale = new Vector3(0.52f, 0.52f, 1f);

            _door = MakeQuad("Door", root, DoorLocked, 1).transform;
            _door.position = ToWorld(_sim.Room.Door);
            _door.localScale = new Vector3(0.92f, 0.32f, 1f);

            // 光画在玩家之上：站进去的时候是"被光裹住"，而不是"光被方块盖住"
            _light = MakeQuad("Light", root, LightColor, 7, _glowSprite).transform;
            _light.position = ToWorld(_sim.Room.Light);
            _light.localScale = new Vector3(0.85f, 0.85f, 1f);

            // 给房间加一圈细边，把构图框住，避免画面糊成一片
            var frame = new Color32(0x2E, 0x34, 0x40, 0xFF);
            MakeEdge("FrameTop", root, frame, new Vector3(1.5f, 3.55f, 0f), new Vector3(4.12f, 0.03f, 1f));
            MakeEdge("FrameBottom", root, frame, new Vector3(1.5f, -0.55f, 0f), new Vector3(4.12f, 0.03f, 1f));
            MakeEdge("FrameLeft", root, frame, new Vector3(-0.55f, 1.5f, 0f), new Vector3(0.03f, 4.12f, 1f));
            MakeEdge("FrameRight", root, frame, new Vector3(3.55f, 1.5f, 0f), new Vector3(0.03f, 4.12f, 1f));

            _burstRing = MakeQuad("BurstRing", root, LightColor, 5, _glowSprite).transform;
            _burstRing.gameObject.SetActive(false);

            _burstCore = MakeQuad("BurstCore", root, LightColor, 6, _glowSprite).transform;
            _burstCore.gameObject.SetActive(false);
        }

        private void MakeEdge(string name, Transform parent, Color color, Vector3 pos, Vector3 scale)
        {
            var go = MakeQuad(name, parent, color, 1);
            go.transform.position = pos;
            go.transform.localScale = scale;
        }

        private void BuildActors()
        {
            var root = new GameObject("Actors").transform;
            root.SetParent(transform, false);

            _shadow = MakeQuad("Shadow", root, ShadowColor, 3).transform;
            _shadow.localScale = new Vector3(0.52f, 0.52f, 1f);

            _player = MakeQuad("Player", root, PlayerColor, 4).transform;
            _player.localScale = new Vector3(0.6f, 0.6f, 1f);
        }

        private GameObject MakeQuad(string name, Transform parent, Color color, int order, Sprite sprite = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite != null ? sprite : _sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return go;
        }

        private static Sprite MakeUnitSprite()
        {
            // 1×1 白图 = 1 世界单位，靠 localScale 做尺寸，零美术依赖
            return Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: 1f);
        }

        /// <summary>
        /// 程序生成的径向衰减贴图。没有它，"光"只能画成一个方块——
        /// 这是本作唯一需要的"美术资产"，96×96，二次衰减让边缘不硬。
        /// </summary>
        private static Sprite MakeRadialSprite(int size = 96)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };

            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    float a = Mathf.Clamp01(1f - r);
                    a *= a; // 二次衰减：中心实、边缘柔

                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            return Sprite.Create(
                tex,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit: size);
        }

        private static Vector3 ToWorld(GridPos p) => new Vector3(p.X, p.Y, 0f);

        // ---------- 极简 HUD ----------

        private void EnsureStyles()
        {
            if (_hud != null) return;

            _font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei", "SimHei", "Noto Sans CJK SC", "Arial" }, 18);

            _hud = new GUIStyle { font = _font, fontSize = 18, normal = { textColor = new Color(0.82f, 0.85f, 0.90f) } };
            _hint = new GUIStyle { font = _font, fontSize = 14, normal = { textColor = new Color(0.48f, 0.51f, 0.56f) } };
            _big = new GUIStyle { font = _font, fontSize = 34, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(0.96f, 0.80f, 0.49f) } };
        }

        private void OnGUI()
        {
            EnsureStyles();

            GUI.Label(new Rect(24f, 20f, 620f, 24f), "WASD / 方向键　走一格　·　R　重来　·　F1　坐标", _hint);

            // 教学节拍：开局一个字都不提"影子慢半拍"。
            // 先让玩家自己踩出来；10 拍还没摸到同步，才给一句环境提示（LevelDesign-M1 §2）。
            if (!_sim.DoorLatched && !_sim.ReachedLight && _sim.Beat >= 10)
            {
                GUI.Label(new Rect(24f, 52f, 620f, 24f), "它跟着你，慢了半拍。", _hint);
            }

            if (_sim.DoorLatched && !_sim.ReachedLight)
            {
                GUI.Label(new Rect(24f, 52f, 620f, 24f), "门开了 —— 但你一走，它也走了。", _hint);
            }

            if (_sim.ReachedLight)
            {
                GUI.Label(new Rect(0f, Screen.height * 0.42f, Screen.width, 60f),
                    "你们终于同步了一次", _big);
                GUI.Label(new Rect(0f, Screen.height * 0.42f + 52f, Screen.width, 30f),
                    "门开了，光在前面　·　按 R 再来一遍", _hint);
            }

            if (_debug)
            {
                GUI.Label(new Rect(24f, Screen.height - 34f, 620f, 24f), _sim.Describe(), _hint);
            }
        }
    }
}
