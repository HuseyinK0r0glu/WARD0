using System.Collections;
using UnityEngine;

namespace Ward0.Gameplay
{
    /// <summary>
    /// Minimal immediate-mode overlay: prompts, objective, timer, battery / stamina, messages and full-screen effects
    /// (fade, steam white-out, damage flash, detection vignette). Everything diegetic lives in the world; this is only feedback.
    /// </summary>
    public class HudOverlay : MonoBehaviour
    {
        [SerializeField] private Interactor interactor;
        [SerializeField] private PlayerController player;
        [SerializeField] private FlashlightController flashlight;

        private float _fade = 1f, _flash; private Color _flashColor = Color.red;
        private string _msg = ""; private float _msgT, _msgDur = 1f;
        private bool _end, _win; private string _endText = ""; private float _endT;
        private Texture2D _vig; private GUIStyle _label, _big, _small, _center; private float _t;
        private bool _debug;

        public void SetFade(float f) { _fade = f; }
        public void Say(string m, float seconds) { _msg = m; _msgT = seconds; _msgDur = seconds; }
        public void Flash(Color c, float strength) { _flashColor = c; _flash = strength; }
        public void ShowEnd(bool win, string text) { _end = true; _win = win; _endText = text; _endT = 0f; }
        public IEnumerator Fade(float to, float seconds)
        {
            float from = _fade, t = 0f;
            while (t < seconds) { t += Time.deltaTime; _fade = Mathf.Lerp(from, to, t / seconds); yield return null; }
            _fade = to;
        }

        private void Awake() { MakeVignette(); }

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

        private void Update()
        {
            _t += Time.deltaTime; if (_msgT > 0f) _msgT -= Time.deltaTime; if (_flash > 0f) _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 0.8f);
            if (_end) _endT += Time.deltaTime;
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f1Key.wasPressedThisFrame) _debug = !_debug;
        }

        private void Styles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.UpperLeft, richText = true };
            _big = new GUIStyle(GUI.skin.label) { fontSize = 46, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, richText = true };
            _small = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.UpperLeft, richText = true };
            _center = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter, richText = true };
        }

        private static void Shadowed(Rect r, string s, GUIStyle st, Color c)
        {
            var o = st.normal.textColor; st.normal.textColor = new Color(0, 0, 0, c.a * 0.9f); GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), s, st);
            st.normal.textColor = c; GUI.Label(r, s, st); st.normal.textColor = o;
        }

        private void OnGUI()
        {
            Styles();
            float sc = Screen.height / 1080f; var m = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(sc, sc, 1f));
            float W = Screen.width / sc, H = Screen.height / sc;
            var sm = ShiftManager.Instance; var cam = Camera.main;

            // detection vignette (red edges when a patrol is spotting you)
            float det = PatrolEnemy.MaxDetection;
            if (det > 0.02f && !_end)
            {
                float pulse = 0.75f + 0.25f * Mathf.Sin(_t * (6f + det * 10f));
                GUI.color = new Color(0.9f, 0.05f, 0.02f, Mathf.Clamp01(det * 0.95f) * pulse); GUI.DrawTexture(new Rect(0, 0, W, H), _vig);
            }
            if (_flash > 0f) { GUI.color = new Color(_flashColor.r, _flashColor.g, _flashColor.b, _flash * 0.55f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); }
            if (cam != null) { float o = SteamBurst.Occlusion(cam.transform.position); if (o > 0.01f) { GUI.color = new Color(0.82f, 0.86f, 0.88f, o * 0.98f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); } }

            if (!_end && sm != null)
            {
                // crosshair + prompt
                GUI.color = new Color(1, 1, 1, interactor != null && interactor.Focus != null ? 0.9f : 0.35f); GUI.DrawTexture(new Rect(W / 2 - 2, H / 2 - 2, 4, 4), Texture2D.whiteTexture);
                if (interactor != null && interactor.Focus != null)
                {
                    Shadowed(new Rect(0, H / 2 + 40, W, 40), interactor.Focus.Prompt, _center, new Color(1, 1, 1, 0.95f));
                    float hp = interactor.HoldProgress; if (hp > 0f) { GUI.color = new Color(0, 0, 0, 0.6f); GUI.DrawTexture(new Rect(W / 2 - 150, H / 2 + 90, 300, 10), Texture2D.whiteTexture); GUI.color = new Color(0.95f, 0.75f, 0.2f, 1); GUI.DrawTexture(new Rect(W / 2 - 148, H / 2 + 92, 296 * hp, 6), Texture2D.whiteTexture); }
                }
                // objective + timer
                GUI.color = Color.white;
                string obj = sm.ObjectiveText; Shadowed(new Rect(30, 24, 700, 30), "<b>" + obj + "</b>", _label, new Color(0.9f, 0.9f, 0.85f, 0.95f));
                if (sm.Phase == ShiftPhase.Mine || sm.Phase == ShiftPhase.Return)
                {
                    bool low = sm.TimeLeft < 90f; Shadowed(new Rect(30, 54, 700, 30), "REACTOR  " + ShiftManager.FormatTime(sm.TimeLeft), _label, low ? new Color(1f, 0.3f + 0.2f * Mathf.Sin(_t * 8f), 0.2f) : new Color(0.95f, 0.75f, 0.3f));
                }
                // torch battery + stamina (bottom left)
                if (flashlight != null)
                {
                    GUI.color = new Color(0, 0, 0, 0.5f); GUI.DrawTexture(new Rect(30, H - 52, 160, 10), Texture2D.whiteTexture);
                    GUI.color = flashlight.IsOn ? new Color(1f, 0.85f, 0.4f) : new Color(0.55f, 0.5f, 0.35f); GUI.DrawTexture(new Rect(31, H - 51, 158 * flashlight.Charge01, 8), Texture2D.whiteTexture);
                    GUI.color = Color.white; Shadowed(new Rect(30, H - 78, 300, 24), "TORCH [F]", _small, new Color(0.9f, 0.85f, 0.6f, 0.9f));
                }
                if (player != null && player.Stamina01 < 0.99f)
                { GUI.color = new Color(0, 0, 0, 0.4f); GUI.DrawTexture(new Rect(30, H - 32, 160, 6), Texture2D.whiteTexture); GUI.color = new Color(0.6f, 0.8f, 0.9f, 0.9f); GUI.DrawTexture(new Rect(31, H - 31, 158 * player.Stamina01, 4), Texture2D.whiteTexture); }
                // message
                if (_msgT > 0f) { float a = Mathf.Clamp01(Mathf.Min(_msgT, 0.5f) / 0.5f); Shadowed(new Rect(0, 120, W, 80), _msg, _center, new Color(1f, 0.95f, 0.85f, a)); }
                if (_t < 14f) { GUI.color = Color.white; Shadowed(new Rect(0, H - 60, W, 30), "WASD move   Shift sprint (loud)   Ctrl crouch (quiet, hide behind cover)   E interact   F torch   F1 debug", _small, new Color(0.8f, 0.8f, 0.75f, Mathf.Clamp01((14f - _t) / 2f) * 0.8f)); }
            }

            // fade to black
            if (_fade > 0.001f) { GUI.color = new Color(0, 0, 0, _fade); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); }

            if (_end)
            {
                float a = Mathf.Clamp01(_endT / 1.6f);
                GUI.color = new Color(_win ? 0f : 0.12f, 0f, 0f, a * 0.92f); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
                GUI.color = Color.white;
                Shadowed(new Rect(0, H * 0.28f, W, 90), _win ? "SHIFT COMPLETE" : "YOU DIED", _big, new Color(_win ? 0.8f : 0.9f, _win ? 0.95f : 0.2f, _win ? 0.8f : 0.2f, a));
                Shadowed(new Rect(W * 0.2f, H * 0.28f + 100, W * 0.6f, 160), _endText, _center, new Color(0.95f, 0.9f, 0.85f, a));
                if (_endT > 2f) Shadowed(new Rect(0, H * 0.75f, W, 40), "Press R to restart", _center, new Color(1, 1, 1, 0.55f + 0.3f * Mathf.Sin(_t * 3f)));
            }

            if (_debug && sm != null)
            {
                var s = SanitySystem.Instance; GUI.color = Color.white;
                string d = "phase " + sm.Phase + "\nsanity " + (s != null ? s.Effective.ToString("0") + " (base " + s.Baseline.ToString("0") + ", decay " + s.Decay.ToString("0") + ", tier " + s.Tier + ")" : "-") + "\nlight " + LightMeter.Level.ToString("0.00") + "  hidden " + (player != null && player.IsHidden) + "\ndetect " + PatrolEnemy.MaxDetection.ToString("0.00");
                Shadowed(new Rect(W - 520, 24, 500, 140), d, _small, Color.yellow);
            }
            GUI.matrix = m; GUI.color = Color.white;
        }
    }
}
