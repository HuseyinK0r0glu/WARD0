using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>
    /// Minimal immediate-mode overlay: prompts, objective, timer, battery / stamina, messages, hints and full-screen effects
    /// (fade, steam white-out, damage flash, detection vignette). Everything diegetic lives in the world; this is only feedback
    /// and instructions. All text comes from <see cref="GameText"/>. Colour language: cream = info, amber = warning,
    /// red = danger, green = success.
    /// </summary>
    public class HudOverlay : MonoBehaviour
    {
        /// <summary>Colour language of a message or hint plate.</summary>
        public enum Tone { Info, Warn, Danger, Good }

        private struct Msg { public string Text; public float Seconds; public bool Important; public Tone Tone; }

        private const float MinScale = 0.72f;        // never render the UI smaller than this (smallest Game view stays readable)
        private const int MaxQueue = 5;
        private const float MsgY = 146f, MsgMaxW = 1100f, HintMaxW = 1000f;
        private static readonly Color Cream = new Color(0.96f, 0.93f, 0.84f), Amber = new Color(1f, 0.76f, 0.26f), Red = new Color(1f, 0.33f, 0.28f), Green = new Color(0.45f, 0.92f, 0.55f);
        private static readonly GUIContent Gc = new GUIContent();

        [SerializeField] private Interactor interactor;
        [SerializeField] private PlayerController player;
        [SerializeField] private FlashlightController flashlight;

        private float _fade = 1f, _flash; private Color _flashColor = Color.red;
        private Msg _cur; private bool _curActive; private float _curT; private readonly List<Msg> _queue = new List<Msg>(MaxQueue + 2);
        private bool _end, _win; private string _endTitle = "", _endBody = "", _endNext = ""; private float _endT, _endBodyH = -1f, _endNextH;
        private Texture2D _vig, _ring, _arrow; private GUIStyle _msgS, _objS, _capS, _promptS, _keyS, _tagS, _hintS, _badgeS, _noteS, _timerS, _titleS, _bodyS, _nextS, _ctrlS, _dbgS, _wpS; private float _t;
        private bool _debug; private HintDirector _hints;
        // cached text measurements (recomputed only when the text changes)
        private string _msgSrc, _objSrc, _promptRaw, _promptText, _hintSrc, _timerStr = "", _wpLabel = "", _ctrlSrc;
        private float _msgW, _msgH, _msgTextW, _objW, _objH, _promptW, _hintW, _hintH, _ctrlW, _ctrlH; private int _timerSec = -1, _wpMeters = -1;

        public void SetFade(float f) { _fade = f; }
        /// <summary>Queue a routine message (kept short; routine messages can be dropped when many arrive at once).</summary>
        public void Say(string m, float seconds) { Say(m, seconds, false, Tone.Info); }

        /// <summary>
        /// Queue a message. Messages never overwrite each other: a new one waits until the current one has been readable
        /// long enough. Important messages jump ahead of routine ones and are never dropped in their favour.
        /// </summary>
        public void Say(string m, float seconds, bool important, Tone tone)
        {
            if (string.IsNullOrEmpty(m) || _end) return;
            if (_curActive && _cur.Text == m) { _curT = 0f; _cur.Seconds = Mathf.Max(_cur.Seconds, seconds); return; }
            for (int i = 0; i < _queue.Count; i++) if (_queue[i].Text == m) return;
            var msg = new Msg { Text = m, Seconds = seconds, Important = important, Tone = tone };
            if (!_curActive && _queue.Count == 0) { Show(msg); return; }
            if (important) { int at = 0; while (at < _queue.Count && _queue[at].Important) at++; _queue.Insert(at, msg); }
            else _queue.Add(msg);
            while (_queue.Count > MaxQueue) { int drop = _queue.FindIndex(q => !q.Important); if (drop < 0) break; _queue.RemoveAt(drop); }
        }

        public void Flash(Color c, float strength) { _flashColor = c; _flash = strength; }
        /// <summary>Show the result screen with a generic title and advice.</summary>
        public void ShowEnd(bool win, string text) { ShowEnd(win, win ? GameText.WinTitle : GameText.FailTitleDeath, text, win ? GameText.WinNext : GameText.FailNext(FailCause.Other)); }
        /// <summary>Show the result screen: title, what happened, and what to do next.</summary>
        public void ShowEnd(bool win, string title, string body, string next)
        {
            _end = true; _win = win; _endTitle = title; _endBody = body; _endNext = next; _endT = 0f; _endBodyH = -1f; _curActive = false; _queue.Clear();
        }
        public IEnumerator Fade(float to, float seconds)
        {
            float from = _fade, t = 0f;
            while (t < seconds) { t += Time.deltaTime; _fade = Mathf.Lerp(from, to, t / seconds); yield return null; }
            _fade = to;
        }

        private void Awake()
        {
            MakeVignette(); _ring = MakeShape(true); _arrow = MakeShape(false);
            _hints = HintDirector.Create(interactor, player); _hints.transform.SetParent(transform, false);
        }

        private void OnDestroy() { if (_vig != null) Destroy(_vig); if (_ring != null) Destroy(_ring); if (_arrow != null) Destroy(_arrow); }

        private void MakeVignette()
        {
            int n = 128; _vig = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float u = (x / (float)(n - 1)) * 2f - 1f, v = (y / (float)(n - 1)) * 2f - 1f; float d = Mathf.Sqrt(u * u + v * v);
                    float a = Mathf.SmoothStep(0.45f, 1.35f, d); _vig.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            _vig.Apply();
        }

        /// <summary>Soft white ring (control marker) or upward-pointing arrow (off-screen pointer), tinted at draw time.</summary>
        private static Texture2D MakeShape(bool ring)
        {
            int n = 64; var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
                {
                    float u = x / (float)(n - 1), v = y / (float)(n - 1), a;
                    if (ring) { float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) * 2f; a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.78f) / 0.1f); }
                    else { float half = (0.92f - v) / 0.82f * 0.38f; a = v < 0.1f || v > 0.92f ? 0f : Mathf.Clamp01((half - Mathf.Abs(u - 0.5f)) / 0.04f); }
                    tex.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            tex.Apply(); return tex;
        }

        private void Show(Msg m) { _cur = m; _curActive = true; _curT = 0f; }

        private void Update()
        {
            _t += Time.deltaTime; if (_flash > 0f) _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 0.8f);
            if (_end) _endT += Time.deltaTime;
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f1Key.wasPressedThisFrame) _debug = !_debug;
            if (_curActive)
            {
                _curT += Time.deltaTime; bool done = _curT >= _cur.Seconds;
                // something is waiting: let the current one go as soon as it has been readable long enough (important ones wait for important ones only)
                if (!done && _queue.Count > 0 && (!_cur.Important || _queue[0].Important) && _curT >= Mathf.Min(_cur.Seconds, 1f + 0.03f * _cur.Text.Length)) done = true;
                if (done) { _curActive = false; if (_queue.Count > 0) { Show(_queue[0]); _queue.RemoveAt(0); } }
            }
            else if (_queue.Count > 0) { Show(_queue[0]); _queue.RemoveAt(0); }
        }

        private static Color ToneColor(Tone t)
        {
            switch (t) { case Tone.Warn: return Amber; case Tone.Danger: return Red; case Tone.Good: return Green; default: return Cream; }
        }

        private static GUIStyle Style(int size, FontStyle fs, TextAnchor al, bool wrap)
        {
            return new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fs, alignment = al, wordWrap = wrap, richText = true, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0), clipping = TextClipping.Overflow };
        }

        private void Styles()
        {
            if (_msgS != null) return;
            _msgS = Style(27, FontStyle.Normal, TextAnchor.MiddleCenter, true);
            _objS = Style(26, FontStyle.Bold, TextAnchor.UpperLeft, true);
            _capS = Style(14, FontStyle.Bold, TextAnchor.UpperLeft, false);
            _promptS = Style(28, FontStyle.Bold, TextAnchor.MiddleLeft, false);
            _keyS = Style(30, FontStyle.Bold, TextAnchor.MiddleCenter, false);
            _tagS = Style(15, FontStyle.Bold, TextAnchor.MiddleRight, false);
            _hintS = Style(25, FontStyle.Normal, TextAnchor.MiddleLeft, true);
            _badgeS = Style(30, FontStyle.Bold, TextAnchor.MiddleCenter, false);
            _noteS = Style(15, FontStyle.Normal, TextAnchor.UpperRight, false);
            _timerS = Style(24, FontStyle.Bold, TextAnchor.UpperLeft, false);
            _titleS = Style(64, FontStyle.Bold, TextAnchor.MiddleCenter, false);
            _bodyS = Style(30, FontStyle.Normal, TextAnchor.MiddleCenter, true);
            _nextS = Style(26, FontStyle.Normal, TextAnchor.UpperCenter, true);
            _ctrlS = Style(20, FontStyle.Normal, TextAnchor.MiddleCenter, true);
            _dbgS = Style(15, FontStyle.Normal, TextAnchor.UpperLeft, true);
            _wpS = Style(22, FontStyle.Bold, TextAnchor.MiddleCenter, false);
        }

        // ------------------------------------------------------------ drawing helpers
        private static GUIContent C(string s) { Gc.text = s; return Gc; }

        /// <summary>Text with a dark outline (four diagonal copies) so it stays readable on any background.</summary>
        private static void Txt(Rect r, string s, GUIStyle st, Color c)
        {
            var content = C(s); var o = st.normal.textColor; GUI.color = Color.white;
            st.normal.textColor = new Color(0f, 0f, 0f, c.a * 0.92f); const float d = 1.6f;
            GUI.Label(new Rect(r.x - d, r.y - d, r.width, r.height), content, st); GUI.Label(new Rect(r.x + d, r.y - d, r.width, r.height), content, st);
            GUI.Label(new Rect(r.x - d, r.y + d, r.width, r.height), content, st); GUI.Label(new Rect(r.x + d, r.y + d, r.width, r.height), content, st);
            st.normal.textColor = c; GUI.Label(r, content, st); st.normal.textColor = o;
        }

        private static void Fill(Rect r, Color c) { GUI.color = c; GUI.DrawTexture(r, Texture2D.whiteTexture); }

        /// <summary>Dark semi-transparent plate with a thin accent border and a heavier accent bar on the left.</summary>
        private static void Plate(Rect r, Color accent, float a, float boost = 0f)
        {
            Fill(r, new Color(0.03f, 0.04f, 0.05f, 0.82f * a));
            var ac = new Color(accent.r, accent.g, accent.b, Mathf.Clamp01(0.8f + boost) * a);
            Fill(new Rect(r.x, r.y, r.width, 2f), ac); Fill(new Rect(r.x, r.yMax - 2f, r.width, 2f), ac); Fill(new Rect(r.xMax - 2f, r.y, 2f, r.height), ac); Fill(new Rect(r.x, r.y, 6f, r.height), ac);
        }

        /// <summary>Width and wrapped height of <paramref name="s"/> in <paramref name="st"/>, limited to <paramref name="maxW"/> (plate padding included by the caller).</summary>
        private static void Measure(string s, GUIStyle st, float maxW, out float w, out float h)
        {
            w = Mathf.Min(maxW, st.CalcSize(C(s)).x + 2f); h = st.CalcHeight(C(s), w);
        }

        // ------------------------------------------------------------ main draw
        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            Styles();
            float sc = Mathf.Max(MinScale, Screen.height / 1080f); var m = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(sc, sc, 1f));
            float W = Screen.width / sc, H = Screen.height / sc;
            var sm = ShiftManager.Instance; var cam = Camera.main;

            // detection vignette (red edges when a patrol is spotting you)
            float det = PatrolEnemy.MaxDetection;
            if (det > 0.02f && !_end)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(_t * (6f + det * 10f));
                GUI.color = new Color(0.9f, 0.05f, 0.02f, Mathf.Clamp01(det * 0.95f) * pulse); GUI.DrawTexture(new Rect(0, 0, W, H), _vig);
            }
            if (_flash > 0f) Fill(new Rect(0, 0, W, H), new Color(_flashColor.r, _flashColor.g, _flashColor.b, _flash * 0.55f));
            if (cam != null) { float o = SteamBurst.Occlusion(cam.transform.position); if (o > 0.01f) Fill(new Rect(0, 0, W, H), new Color(0.82f, 0.86f, 0.88f, o * 0.98f)); }

            if (!_end && sm != null)
            {
                DrawCrosshairAndPrompt(W, H);
                DrawObjective(sm);
                DrawTorchAndStamina(H);
                DrawMessage(W);
                DrawHint(cam, sc, W, H);
                DrawControlsHint(W, H);
            }

            // fade to black
            if (_fade > 0.001f) Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, _fade));

            if (_end) DrawEnd(W, H);

            if (_debug && sm != null)
            {
                var s = SanitySystem.Instance;
                string d = "phase " + sm.Phase + "\nsanity " + (s != null ? s.Effective.ToString("0") + " (base " + s.Baseline.ToString("0") + ", decay " + s.Decay.ToString("0") + ", tier " + s.Tier + ")" : "-") + "\nlight " + LightMeter.Level.ToString("0.00") + "  hidden " + (player != null && player.IsHidden) + "\ndetect " + PatrolEnemy.MaxDetection.ToString("0.00");
                Plate(new Rect(W - 540, 20, 520, 100), Amber, 1f); Txt(new Rect(W - 524, 28, 500, 90), d, _dbgS, Color.yellow);
            }
            GUI.matrix = m; GUI.color = Color.white;
        }

        private void DrawCrosshairAndPrompt(float W, float H)
        {
            var focus = interactor != null ? interactor.Focus : null;
            Fill(new Rect(W / 2 - 4, H / 2 - 4, 8, 8), new Color(0, 0, 0, focus != null ? 0.6f : 0.35f));
            Fill(new Rect(W / 2 - 2, H / 2 - 2, 4, 4), new Color(1, 1, 1, focus != null ? 0.95f : 0.45f));
            if (focus == null) return;

            string raw = focus.Prompt; bool hold = focus.HoldSeconds > 0f;
            if (!ReferenceEquals(raw, _promptRaw) && raw != _promptRaw)
            {
                _promptRaw = raw; _promptText = raw;
                if (raw.StartsWith("Hold E: ", System.StringComparison.Ordinal)) _promptText = raw.Substring(8);
                else if (raw.StartsWith("E: ", System.StringComparison.Ordinal)) _promptText = raw.Substring(3);
                _promptW = _promptS.CalcSize(C(_promptText)).x;
            }
            float tagW = hold ? 74f : 0f, keyW = 46f, pw = 24f + tagW + keyW + 16f + _promptW + 24f, ph = 62f;
            var r = new Rect((W - pw) / 2f, H / 2f + 40f, pw, ph); Plate(r, hold ? Amber : Cream, 1f);
            float x = r.x + 24f;
            if (hold) { Txt(new Rect(x, r.y, tagW - 8f, ph), GameText.KeyHold, _tagS, Amber); x += tagW; }
            var key = new Rect(x, r.y + 8f, keyW, ph - 16f);
            Fill(key, new Color(0.62f, 0.58f, 0.5f)); Fill(new Rect(key.x, key.y, key.width, key.height - 5f), new Color(0.95f, 0.92f, 0.82f));
            GUI.color = Color.white; var ko = _keyS.normal.textColor; _keyS.normal.textColor = new Color(0.08f, 0.08f, 0.08f); GUI.Label(new Rect(key.x, key.y, key.width, key.height - 5f), C(GameText.KeyInteract), _keyS); _keyS.normal.textColor = ko;
            Txt(new Rect(key.xMax + 16f, r.y, _promptW + 8f, ph), _promptText, _promptS, Cream);
            float hp = interactor.HoldProgress;
            if (hold)
            {
                var bar = new Rect(r.x, r.yMax + 8f, pw, 18f); Fill(bar, new Color(0.03f, 0.04f, 0.05f, 0.85f));
                Fill(new Rect(bar.x, bar.y, bar.width, 2f), Amber); Fill(new Rect(bar.x, bar.yMax - 2f, bar.width, 2f), Amber); Fill(new Rect(bar.x, bar.y, 2f, bar.height), Amber); Fill(new Rect(bar.xMax - 2f, bar.y, 2f, bar.height), Amber);
                if (hp > 0f) Fill(new Rect(bar.x + 4f, bar.y + 4f, (bar.width - 8f) * hp, bar.height - 8f), Color.Lerp(Amber, Green, Mathf.SmoothStep(0f, 1f, (hp - 0.7f) / 0.3f)));
            }
        }

        private void DrawObjective(ShiftManager sm)
        {
            string obj = sm.ObjectiveText; bool timer = sm.Phase == ShiftPhase.Mine || sm.Phase == ShiftPhase.Return;
            if (!ReferenceEquals(obj, _objSrc)) { _objSrc = obj; Measure(obj, _objS, 800f, out _objW, out _objH); }
            float w = Mathf.Max(_objW + 44f, timer ? 300f : 260f), h = 14f + 22f + _objH + (timer ? 36f : 0f) + 12f;
            var r = new Rect(28f, 24f, w, h); Plate(r, Cream, 1f);
            Txt(new Rect(r.x + 22f, r.y + 10f, w, 20f), GameText.ObjectiveCaption, _capS, Amber);
            Txt(new Rect(r.x + 22f, r.y + 30f, _objW + 4f, _objH), obj, _objS, new Color(0.96f, 0.93f, 0.84f));
            if (!timer) return;
            int sec = Mathf.CeilToInt(sm.TimeLeft); if (sec != _timerSec) { _timerSec = sec; _timerStr = GameText.Reactor(ShiftManager.FormatTime(sec)); }
            bool low = sm.TimeLeft < 90f;
            Txt(new Rect(r.x + 22f, r.y + 30f + _objH + 8f, w, 30f), _timerStr, _timerS, low ? new Color(1f, 0.3f + 0.2f * Mathf.Sin(_t * 8f), 0.2f) : Amber);
        }

        private void DrawTorchAndStamina(float H)
        {
            if (flashlight == null) return;
            bool stam = player != null && player.Stamina01 < 0.99f;
            Plate(new Rect(24f, H - 100f, 196f, stam ? 68f : 52f), Amber, 0.85f);
            Txt(new Rect(42f, H - 96f, 170f, 20f), GameText.TorchLabel, _capS, new Color(0.95f, 0.88f, 0.6f));
            Fill(new Rect(42f, H - 72f, 160f, 10f), new Color(0, 0, 0, 0.6f));
            Fill(new Rect(43f, H - 71f, 158f * flashlight.Charge01, 8f), flashlight.IsOn ? new Color(1f, 0.85f, 0.4f) : new Color(0.62f, 0.56f, 0.38f));
            if (!stam) return;
            Fill(new Rect(42f, H - 52f, 160f, 8f), new Color(0, 0, 0, 0.6f));
            Fill(new Rect(43f, H - 51f, 158f * player.Stamina01, 6f), new Color(0.6f, 0.82f, 0.95f));
        }

        private void DrawMessage(float W)
        {
            if (!_curActive) return;
            float a = Mathf.Clamp01(Mathf.Min(_curT / 0.2f, (_cur.Seconds - _curT) / 0.45f));
            float maxW = Mathf.Min(MsgMaxW, W - 80f);
            if (!ReferenceEquals(_msgSrc, _cur.Text) || _msgW != maxW)
            {
                _msgSrc = _cur.Text; _msgW = maxW; float tw; Measure(_cur.Text, _msgS, maxW - 56f, out tw, out _msgH); _msgTextW = tw;
            }
            float w = _msgTextW + 56f, h = _msgH + 28f; var r = new Rect((W - w) / 2f, MsgY, w, h);
            var tone = ToneColor(_cur.Tone); Plate(r, tone, a);
            Txt(new Rect(r.x + 28f, r.y + 14f, _msgTextW + 4f, _msgH), _cur.Text, _msgS, new Color(Cream.r, Cream.g, Cream.b, a));
        }

        private void DrawHint(Camera cam, float sc, float W, float H)
        {
            var hd = _hints; if (hd == null || !HintDirector.HintsEnabled) return;
            DrawMarkers(cam, sc, W, H, hd);
            if (hd.Text.Length == 0) return;
            float pw = Mathf.Min(HintMaxW, W - 80f), badgeW = 96f, inner = pw - badgeW - 56f;
            if (!ReferenceEquals(_hintSrc, hd.Text) || _hintW != pw) { _hintSrc = hd.Text; _hintW = pw; _hintH = _hintS.CalcHeight(C(hd.Text), inner); }
            float ph = Mathf.Max(66f, _hintH + 24f), age = Time.unscaledTime - hd.ChangedAt, a = Mathf.Clamp01(age / 0.25f);
            var r = new Rect((W - pw) / 2f, H - 112f - ph, pw, ph); var tone = ToneColor(hd.Tone);
            Plate(r, tone, a, Mathf.Clamp01(1f - age / 0.9f) * 0.2f);
            string label = hd.StepLabel.Length > 0 ? hd.StepLabel : GameText.HintCaption;
            Txt(new Rect(r.x + 14f, r.y, badgeW, ph), label, _badgeS, new Color(Amber.r, Amber.g, Amber.b, a));
            Fill(new Rect(r.x + badgeW + 18f, r.y + 12f, 2f, ph - 24f), new Color(1f, 1f, 1f, 0.18f * a));
            Txt(new Rect(r.x + badgeW + 34f, r.y, inner, ph), hd.Text, _hintS, new Color(Cream.r, Cream.g, Cream.b, a));
            Txt(new Rect(r.xMax - 240f, r.yMax + 3f, 240f, 20f), GameText.HintHideNote, _noteS, new Color(0.85f, 0.82f, 0.72f, 0.7f * a));
        }

        /// <summary>Pulsing rings on the desk controls to use next; an elevator waypoint (label + distance) in the Descent and Return phases. Off-screen targets get an edge arrow.</summary>
        private void DrawMarkers(Camera cam, float sc, float W, float H, HintDirector hd)
        {
            if (cam == null) return;
            for (int i = 0; i < hd.ControlCount; i++) Marker(cam, sc, W, H, hd.ControlPos(i), false);
            if (hd.HasWaypoint) Marker(cam, sc, W, H, hd.WaypointPos, true);
        }

        private void Marker(Camera cam, float sc, float W, float H, Vector3 world, bool waypoint)
        {
            float dist = Vector3.Distance(cam.transform.position, world);
            if (waypoint && dist < 2.2f) return;
            Vector3 sp = cam.WorldToScreenPoint(world); bool behind = sp.z <= 0f;
            float x = sp.x / sc, y = (Screen.height - sp.y) / sc;
            if (behind) { x = W - x; y = H - y; }
            bool on = !behind && x > 24f && x < W - 24f && y > 24f && y < H - 24f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(_t * 5f);
            var col = new Color(Amber.r, Amber.g, Amber.b, 0.55f + 0.45f * pulse);
            if (on)
            {
                float s = 54f + 12f * pulse; GUI.color = col; GUI.DrawTexture(new Rect(x - s / 2f, y - s / 2f, s, s), _ring);
            }
            else
            {
                float dx = x - W / 2f, dy = y - H / 2f, k = Mathf.Min((W / 2f - 70f) / Mathf.Max(Mathf.Abs(dx), 0.01f), (H / 2f - 70f) / Mathf.Max(Mathf.Abs(dy), 0.01f));
                x = W / 2f + dx * k; y = H / 2f + dy * k;
                var saved = GUI.matrix; GUIUtility.RotateAroundPivot(Mathf.Atan2(dx, -dy) * Mathf.Rad2Deg, new Vector2(x, y));
                GUI.color = col; GUI.DrawTexture(new Rect(x - 24f, y - 24f, 48f, 48f), _arrow); GUI.matrix = saved;
            }
            if (!waypoint) return;
            int metres = Mathf.RoundToInt(dist); if (metres != _wpMeters) { _wpMeters = metres; _wpLabel = GameText.Waypoint(GameText.WaypointElevator, metres); }
            float lw = _wpS.CalcSize(C(_wpLabel)).x + 36f, lh = 36f;
            float lx = Mathf.Clamp(x - lw / 2f, 12f, W - lw - 12f), ly = on ? y - 27f - lh - 4f : (y < H / 2f ? y + 34f : y - 34f - lh);
            var r = new Rect(lx, ly, lw, lh); Plate(r, Amber, 0.95f); Txt(new Rect(r.x + 6f, r.y, lw, lh), _wpLabel, _wpS, Amber);
        }

        private void DrawControlsHint(float W, float H)
        {
            if (_t >= 14f) return;
            float a = Mathf.Clamp01((14f - _t) / 2f) * Mathf.Clamp01(_t / 0.6f), maxW = W - 80f;
            if (!ReferenceEquals(_ctrlSrc, GameText.ControlsHint) || _ctrlW != maxW) { _ctrlSrc = GameText.ControlsHint; _ctrlW = maxW; _ctrlH = _ctrlS.CalcHeight(C(GameText.ControlsHint), maxW - 48f); }
            float tw = Mathf.Min(maxW - 48f, _ctrlS.CalcSize(C(GameText.ControlsHint)).x + 4f), w = tw + 48f, h = _ctrlH + 18f;
            var r = new Rect((W - w) / 2f, H - 28f - h, w, h); Plate(r, Cream, 0.85f * a);
            Txt(new Rect(r.x + 24f, r.y + 9f, tw, _ctrlH), GameText.ControlsHint, _ctrlS, new Color(0.9f, 0.88f, 0.8f, a));
        }

        private void DrawEnd(float W, float H)
        {
            float a = Mathf.Clamp01(_endT / 1.6f);
            Fill(new Rect(0, 0, W, H), new Color(_win ? 0f : 0.12f, 0f, 0f, a * 0.92f));
            var accent = _win ? Green : Red;
            Txt(new Rect(0, H * 0.16f, W, 90f), _endTitle, _titleS, new Color(accent.r, accent.g, accent.b, a));
            float pw = Mathf.Min(900f, W - 120f), inner = pw - 64f;
            if (_endBodyH < 0f) { _endBodyH = _bodyS.CalcHeight(C(_endBody), inner); _endNextH = _nextS.CalcHeight(C(_endNext), inner); }
            float x = (W - pw) / 2f, y = H * 0.16f + 104f, bh = _endBodyH + 40f;
            Plate(new Rect(x, y, pw, bh), accent, a);
            Txt(new Rect(x + 32f, y + 20f, inner, _endBodyH), _endBody, _bodyS, new Color(Cream.r, Cream.g, Cream.b, a));
            y += bh + 14f; float nh = 24f + _endNextH + 22f;
            Plate(new Rect(x, y, pw, nh), Amber, a);
            Txt(new Rect(x + 32f, y + 10f, 200f, 20f), GameText.NextCaption, _capS, new Color(Amber.r, Amber.g, Amber.b, a));
            Txt(new Rect(x + 32f, y + 30f, inner, _endNextH), _endNext, _nextS, new Color(Cream.r, Cream.g, Cream.b, a));
            if (_endT > 2f) Txt(new Rect(0, y + nh + 28f, W, 40f), GameText.Restart, _bodyS, new Color(1, 1, 1, 0.65f + 0.3f * Mathf.Sin(_t * 3f)));
        }
    }
}
