using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using MagicPlus.FullSkeleton;

namespace MagicPlus.BreathAnimation
{
    public sealed class FireBreathAnimationWindow : EditorWindow
    {
        public const string RootPath = "Assets/Magic_plus/FireBreathAnimation";
        public const string ScenePath = RootPath + "/Scenes/MagicPlus_FireBreathAnimation.unity";
        public const string SettingsPath = RootPath + "/FireBreathSequence.json";
        [SerializeField] private FullSkeletonPoseLab _lab;
        [SerializeField] private TextAsset _ready, _gather, _hold;
        [SerializeField] private FireBreathSequence.Settings _settings = new FireBreathSequence.Settings();
        [SerializeField] private float _speed = 1f;
        [SerializeField] private bool _loop;
        private FireBreathSequence _preview;
        private bool _playing, _keyHeld, _saving, _resume, _sources;
        private double _lastTime;
        private float _cursor;
        private Vector2 _scroll;
        private string _error;
        public FireBreathSequence Preview => _preview;

        [MenuItem("MagicPlus/Fire Breath Animation")]
        public static void Open()
        {
            var window = GetWindow<FireBreathAnimationWindow>("Огненное дыхание — движение");
            window.minSize = new Vector2(400, 650); window.Show();
        }
        public static void OpenFor(FullSkeletonPoseLab lab)
        {
            Open(); var window = GetWindow<FireBreathAnimationWindow>();
            window.StopPreview(); window._lab = lab; window.DefaultSources(); window.Repaint();
        }
        private void OnEnable()
        {
            DefaultSources();
            if (File.Exists(SettingsPath))
                try { _settings = JsonUtility.FromJson<FireBreathSequence.Settings>(File.ReadAllText(SettingsPath)); }
                catch (Exception e) { _error = e.Message; }
            if (_settings == null) _settings = new FireBreathSequence.Settings();
            _settings.Validate(); _lastTime = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick; AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
            EditorSceneManager.sceneOpening += SceneOpening; EditorSceneManager.sceneSaving += SceneSaving; EditorSceneManager.sceneSaved += SceneSaved;
            EditorApplication.playModeStateChanged += ModeChanged;
        }
        private void OnDisable()
        {
            StopPreview(); EditorApplication.update -= Tick; AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
            EditorSceneManager.sceneOpening -= SceneOpening; EditorSceneManager.sceneSaving -= SceneSaving; EditorSceneManager.sceneSaved -= SceneSaved;
            EditorApplication.playModeStateChanged -= ModeChanged;
        }
        private void DefaultSources()
        {
            if (!_ready) _ready = AssetDatabase.LoadAssetAtPath<TextAsset>(RootPath + "/Poses/Ready.json");
            if (!_gather) _gather = AssetDatabase.LoadAssetAtPath<TextAsset>(RootPath + "/Poses/Gather.json");
            if (!_hold) _hold = AssetDatabase.LoadAssetAtPath<TextAsset>(RootPath + "/Poses/Hold.json");
        }
        private FireBreathSequence CreatePreview()
        {
            if (!_lab || !_ready || !_gather || !_hold) throw new InvalidOperationException("Нужны манекен и три исходные позы.");
            return new FireBreathSequence(_lab, _ready.text, _gather.text, _hold.text, _settings);
        }
        public void StartPreview(bool automatic)
        {
            StopPreview(); _preview = CreatePreview(); _preview.Begin(automatic);
            _playing = true; _error = null; _lastTime = EditorApplication.timeSinceStartup;
        }
        public void StopPreview()
        {
            _playing = _keyHeld = _saving = _resume = false;
            if (_preview != null) _preview.Restore(); _preview = null; _cursor = 0;
            SceneView.RepaintAll(); Repaint();
        }
        private void Scrub(float seconds)
        {
            _playing = false; if (_preview == null) _preview = CreatePreview();
            _preview.SampleAt(seconds); _cursor = _preview.Time; SceneView.RepaintAll();
        }
        private void Tick()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = Mathf.Clamp((float)(now - _lastTime), 0f, .1f); _lastTime = now;
            if (!_lab && _preview != null) { StopPreview(); return; }
            if (!_playing || _saving || _preview == null) return;
            Try(() =>
            {
                _preview.Advance(dt * _speed); _cursor = _preview.Time;
                if (_preview.CurrentPhase == FireBreathSequence.Phase.Complete)
                { if (_loop) _preview.Begin(true); else _playing = false; }
                SceneView.RepaintAll();
            }); Repaint();
        }
        private void SceneOpening(string path, OpenSceneMode mode) { StopPreview(); _lab = null; }
        private void ModeChanged(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingEditMode) StopPreview(); }
        private void SceneSaving(Scene scene, string path)
        {
            if (!_lab || _lab.gameObject.scene != scene || _preview == null || !_preview.OwnsPose) return;
            _saving = true; _resume = _playing; _playing = false; _preview.ShowSavedPose();
        }
        private void SceneSaved(Scene scene)
        {
            if (!_saving || !_lab || _lab.gameObject.scene != scene || _preview == null) return;
            _preview.Reapply(); _playing = _resume; _saving = false; _lastTime = EditorApplication.timeSinceStartup;
            SceneView.RepaintAll();
        }
        private void OnLostFocus() { if (_keyHeld) { _keyHeld = false; _preview?.RequestStop(); } }
        private void Keyboard()
        {
            var e = Event.current; if (EditorGUIUtility.editingTextField) return;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.H)
            { if (!_keyHeld) { Try(() => StartPreview(false)); _keyHeld = true; } e.Use(); }
            if (e.type == EventType.KeyUp && e.keyCode == KeyCode.H) { _keyHeld = false; _preview?.RequestStop(); e.Use(); }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Space && _preview != null)
            { _playing = !_playing; _lastTime = EditorApplication.timeSinceStartup; e.Use(); }
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape) { StopPreview(); e.Use(); }
        }
        private void Try(Action action)
        {
            try { action(); _error = null; }
            catch (Exception e) { StopPreview(); _error = e.Message; }
        }
        private static string PhaseName(FireBreathSequence.Phase phase)
        {
            switch (phase)
            {
                case FireBreathSequence.Phase.Enter: return "Вход — подъём руки";
                case FireBreathSequence.Phase.Hold: return "Удержание — выдох";
                case FireBreathSequence.Phase.Exit: return "Выход — опускание руки";
                case FireBreathSequence.Phase.Complete: return "Завершено";
                default: return "Готово к просмотру";
            }
        }
        private void OnGUI()
        {
            if (!_lab)
                foreach (var candidate in UnityEngine.Object.FindObjectsByType<FullSkeletonPoseLab>(FindObjectsSortMode.None))
                    if (candidate.gameObject.scene.path == ScenePath) _lab = candidate;
            if (!_lab)
            {
                EditorGUILayout.HelpBox("Откройте сцену анимации огненного дыхания.", MessageType.Info);
                if (GUILayout.Button("Открыть сцену дыхания") && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            Keyboard(); _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Label("Огненное дыхание — целое движение", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Предпросмотр работает без Play. Зажмите H в этом окне или используйте кнопки. Пробел — пауза, Esc — вернуть исходную позу.", MessageType.Info);
            EditorGUILayout.LabelField("Фаза", PhaseName(_preview?.CurrentPhase ?? FireBreathSequence.Phase.Ready));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Автопоказ")) Try(() => StartPreview(true));
                if (GUILayout.Button("Начать удержание")) Try(() => StartPreview(false));
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(_preview == null || !_preview.Running))
                    if (GUILayout.Button("Прекратить дыхание")) _preview.RequestStop();
                using (new EditorGUI.DisabledScope(_preview == null))
                    if (GUILayout.Button(_playing ? "Пауза" : "Продолжить")) { _playing = !_playing; _lastTime = EditorApplication.timeSinceStartup; }
            }
            if (GUILayout.Button("Стоп / вернуть исходную позу")) StopPreview();
            _loop = EditorGUILayout.Toggle("Повторять автопоказ", _loop);
            _speed = EditorGUILayout.Slider("Скорость просмотра", _speed, .25f, 2f);
            float duration = _settings.enterSeconds + _settings.automaticHoldSeconds + _settings.exitSeconds;
            EditorGUI.BeginChangeCheck(); float time = EditorGUILayout.Slider("Время, с", Mathf.Min(_cursor, duration), 0f, duration);
            if (EditorGUI.EndChangeCheck()) Try(() => Scrub(time));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("← 1 кадр")) Try(() => Scrub(Mathf.Max(0, _cursor - 1f / 60f)));
                if (GUILayout.Button("1 кадр →")) Try(() => Scrub(Mathf.Min(duration, _cursor + 1f / 60f)));
            }
            EditorGUILayout.Space(); GUILayout.Label("Темп и удержание", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(_preview != null))
            {
                _settings.enterSeconds = EditorGUILayout.Slider("Вход, с", _settings.enterSeconds, .1f, 1.5f);
                _settings.automaticHoldSeconds = EditorGUILayout.Slider("Автоудержание, с", _settings.automaticHoldSeconds, .2f, 6f);
                _settings.exitSeconds = EditorGUILayout.Slider("Выход, с", _settings.exitSeconds, .1f, 1.5f);
                _settings.holdCycleSeconds = EditorGUILayout.Slider("Период удержания, с", _settings.holdCycleSeconds, .6f, 6f);
                _settings.breathPitchDegrees = EditorGUILayout.Slider("Движение корпуса, °", _settings.breathPitchDegrees, 0f, 3f);
                if (GUILayout.Button("Сохранить темп")) Try(SaveSettings);
                if (GUILayout.Button("Пересобрать клипы Unity")) Try(() =>
                { SaveSettings(); FireBreathBaker.Bake(_lab, _ready.text, _gather.text, _hold.text, _settings); });
                _sources = EditorGUILayout.Foldout(_sources, "Исходные позы", true);
                if (_sources)
                {
                    _ready = (TextAsset)EditorGUILayout.ObjectField("Опущенные руки", _ready, typeof(TextAsset), false);
                    _gather = (TextAsset)EditorGUILayout.ObjectField("Подготовка", _gather, typeof(TextAsset), false);
                    _hold = (TextAsset)EditorGUILayout.ObjectField("Hold", _hold, typeof(TextAsset), false);
                }
                if (GUILayout.Button("Открыть Hold для правки")) Try(() =>
                { _lab.LoadJson(_hold.text); EditorSceneManager.MarkSceneDirty(_lab.gameObject.scene); FullSkeletonPoseWindow.OpenFor(_lab); });
                if (GUILayout.Button("Сохранить текущую позу как Hold…")) Try(SaveCurrentHold);
            }
            if (_preview != null) EditorGUILayout.HelpBox("Для изменения темпа или исходных поз нажмите «Стоп».", MessageType.None);
            if (!string.IsNullOrEmpty(_error)) EditorGUILayout.HelpBox(_error, MessageType.Error);
            EditorGUILayout.EndScrollView();
        }
        private void SaveSettings()
        {
            _settings.Validate(); File.WriteAllText(SettingsPath, JsonUtility.ToJson(_settings, true)); AssetDatabase.ImportAsset(SettingsPath);
        }
        private void SaveCurrentHold()
        {
            string path = EditorUtility.SaveFilePanel("Рабочая поза удержания", Path.GetFullPath(RootPath + "/Poses"), "Hold", "json");
            if (string.IsNullOrEmpty(path)) return;
            string relative = FileUtil.GetProjectRelativePath(path);
            if (!relative.StartsWith("Assets/", StringComparison.Ordinal)) throw new ArgumentException("Сохраните позу внутри Assets проекта.");
            File.WriteAllText(path, _lab.ExportJson(Path.GetFileNameWithoutExtension(path))); AssetDatabase.ImportAsset(relative);
            _hold = AssetDatabase.LoadAssetAtPath<TextAsset>(relative);
        }
    }
}
