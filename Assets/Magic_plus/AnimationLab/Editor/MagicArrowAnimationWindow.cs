using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace MagicPlus.AnimationLab
{
    public sealed class MagicArrowAnimationWindow : EditorWindow
    {
        public const string SettingsPath="Assets/Magic_plus/AnimationLab/Animation/FireArrowSequence.json";
        private const string Poses="Assets/Magic_plus/AnimationLab/Poses/Reviewed/";
        [SerializeField] private MagicPoseLab _lab;
        [SerializeField] private TextAsset _start,_loaded,_release,_ready;
        [SerializeField] private MagicPoseSequence.Settings _settings=new MagicPoseSequence.Settings();
        [SerializeField] private float _speed=1f;
        [SerializeField] private bool _loop;
        private MagicPoseSequence _preview;
        private bool _playing,_keyHeld,_resumeAfterSave,_saving,_sources,_arcs;
        private double _lastTime;
        private float _cursor;
        private Vector2 _scroll;
        private string _error;
        public MagicPoseSequence Preview=>_preview;
        public static bool Owns(MagicPoseLab lab)
        {
            foreach(var window in Resources.FindObjectsOfTypeAll<MagicArrowAnimationWindow>())
                if(window._lab==lab && window._preview!=null && window._preview.OwnsPose)return true;
            return false;
        }
        [MenuItem("MagicPlus/Fire Arrow Animation")]
        public static void Open()
        {
            var window=GetWindow<MagicArrowAnimationWindow>("Огненная стрела — движение");window.minSize=new Vector2(390,650);window.Show();
        }
        public static void OpenFor(MagicPoseLab lab)
        {Open();var window=GetWindow<MagicArrowAnimationWindow>();window.StopPreview();window._lab=lab;window.DefaultSources();window.Repaint();}
        private void OnEnable()
        {
            DefaultSources();LoadSettings();_lastTime=EditorApplication.timeSinceStartup;
            EditorApplication.update+=Tick;AssemblyReloadEvents.beforeAssemblyReload+=StopPreview;
            EditorApplication.playModeStateChanged+=ModeChanged;
            EditorSceneManager.sceneOpening+=SceneOpening;EditorSceneManager.sceneSaving+=SceneSaving;EditorSceneManager.sceneSaved+=SceneSaved;
        }
        private void OnDisable()
        {
            StopPreview();EditorApplication.update-=Tick;AssemblyReloadEvents.beforeAssemblyReload-=StopPreview;
            EditorApplication.playModeStateChanged-=ModeChanged;
            EditorSceneManager.sceneOpening-=SceneOpening;EditorSceneManager.sceneSaving-=SceneSaving;EditorSceneManager.sceneSaved-=SceneSaved;
        }
        private void DefaultSources()
        {
            if(!_start)_start=AssetDatabase.LoadAssetAtPath<TextAsset>(Poses+"FireArrow_Start_Reviewed.json");
            if(!_loaded)_loaded=AssetDatabase.LoadAssetAtPath<TextAsset>(Poses+"FireArrow_Loaded_Reviewed.json");
            if(!_release)_release=AssetDatabase.LoadAssetAtPath<TextAsset>(Poses+"FireArrow_Release_Reviewed.json");
            if(!_ready)_ready=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Magic_plus/AnimationLab/LocalReference/SequenceReady.json");
        }
        private void LoadSettings()
        {
            if(File.Exists(SettingsPath))try{_settings=JsonUtility.FromJson<MagicPoseSequence.Settings>(File.ReadAllText(SettingsPath));}catch(Exception e){_error=e.Message;}
            if(_settings==null)_settings=new MagicPoseSequence.Settings();_settings.Validate();
        }
        private void SaveSettings()
        {
            _settings.Validate();Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            File.WriteAllText(SettingsPath,JsonUtility.ToJson(_settings,true));AssetDatabase.ImportAsset(SettingsPath);
        }
        private MagicPoseSequence CreatePreview()
        {
            if(!_lab)_lab=UnityEngine.Object.FindFirstObjectByType<MagicPoseLab>();
            if(!_lab || !_start || !_loaded || !_release)throw new InvalidOperationException("Нужны манекен и три сохранённые позы.");
            return new MagicPoseSequence(_lab,_start.text,_loaded.text,_release.text,_ready?_ready.text:null,_settings);
        }
        public void StartPreview(bool automatic)
        {
            StopPreview();_preview=CreatePreview();_preview.Begin(automatic);_playing=true;_error=null;_cursor=0;_lastTime=EditorApplication.timeSinceStartup;
            SceneView.RepaintAll();
        }
        public void StopPreview()
        {
            _playing=false;_keyHeld=false;_saving=false;_resumeAfterSave=false;
            if(_preview!=null)_preview.Restore();_preview=null;_cursor=0;SceneView.RepaintAll();Repaint();
        }
        private void Scrub(float seconds)
        {
            _playing=false;if(_preview==null)_preview=CreatePreview();_preview.SampleAt(seconds);_cursor=_preview.TimelineTime;
            SceneView.RepaintAll();
        }
        private void Try(Action action)
        {try{action();_error=null;}catch(Exception e){StopPreview();_error=e.Message;}}
        private void Tick()
        {
            double now=EditorApplication.timeSinceStartup;float delta=Mathf.Clamp((float)(now-_lastTime),0,.1f);_lastTime=now;
            if(!_lab && _preview!=null){StopPreview();return;}
            if(!_playing || _saving || _preview==null)return;
            Try(()=>{
                _preview.Advance(delta*_speed);_cursor=_preview.TimelineTime;
                if(_preview.CurrentStage==MagicPoseSequence.Stage.Complete){if(_loop)_preview.Begin(true);else _playing=false;}
                SceneView.RepaintAll();
            });Repaint();
        }
        private void ModeChanged(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingEditMode)StopPreview();}
        private void SceneOpening(string path,OpenSceneMode mode){StopPreview();_lab=null;}
        private void SceneSaving(Scene scene,string path)
        {
            if(!_lab || _lab.gameObject.scene!=scene || _preview==null || !_preview.OwnsPose)return;
            _saving=true;_resumeAfterSave=_playing;_playing=false;_preview.ShowSavedPose();
        }
        private void SceneSaved(Scene scene)
        {
            if(!_saving || !_lab || _lab.gameObject.scene!=scene || _preview==null)return;
            _preview.Reapply();_playing=_resumeAfterSave;_saving=false;_lastTime=EditorApplication.timeSinceStartup;SceneView.RepaintAll();
        }
        private void OnLostFocus(){if(_keyHeld){_keyHeld=false;_preview?.RequestRelease();}}
        private static string StageName(MagicPoseSequence.Stage stage)
        {
            switch(stage){case MagicPoseSequence.Stage.Enter:return "Подъём рук → Start";case MagicPoseSequence.Stage.Draw:return "Натяжение → Loaded";
                case MagicPoseSequence.Stage.Hold:return "Удержание";case MagicPoseSequence.Stage.Release:return "Выпуск";
                case MagicPoseSequence.Stage.Recover:return "Возврат рук";case MagicPoseSequence.Stage.Complete:return "Завершено";default:return "Готово к просмотру";}
        }
        private void Keyboard()
        {
            var e=Event.current;if(EditorGUIUtility.editingTextField)return;
            if(e.type==EventType.KeyDown && e.keyCode==KeyCode.G){if(!_keyHeld){Try(()=>StartPreview(false));_keyHeld=true;}e.Use();}
            if(e.type==EventType.KeyUp && e.keyCode==KeyCode.G){_keyHeld=false;_preview?.RequestRelease();e.Use();}
            if(e.type==EventType.KeyDown && e.keyCode==KeyCode.Escape){StopPreview();e.Use();}
            if(e.type==EventType.KeyDown && e.keyCode==KeyCode.Space && _preview!=null){_playing=!_playing;_lastTime=EditorApplication.timeSinceStartup;e.Use();}
        }
        private void OnGUI()
        {
            Keyboard();if(!_lab)_lab=UnityEngine.Object.FindFirstObjectByType<MagicPoseLab>();
            _scroll=EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Label("Огненная стрела — целое движение",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Работает без Play. Автопоказ проигрывает весь жест. Для свободного удержания используйте две кнопки или зажмите G в этом окне.",MessageType.Info);
            if(!_lab){EditorGUILayout.HelpBox("Откройте сцену MagicPlus_FireArrowAnimation.",MessageType.Info);EditorGUILayout.EndScrollView();return;}
            EditorGUILayout.LabelField("Фаза",StageName(_preview?.CurrentStage??MagicPoseSequence.Stage.Idle));
            if(_preview!=null && _preview.ReleaseQueued && _preview.CurrentStage<MagicPoseSequence.Stage.Release)
                EditorGUILayout.HelpBox("Отпускание запрошено: сначала закончится подготовка.",MessageType.None);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Автопоказ"))Try(()=>StartPreview(true));
                if(GUILayout.Button("Начать удержание"))Try(()=>StartPreview(false));
            }
            using(new EditorGUILayout.HorizontalScope())
            {
                using(new EditorGUI.DisabledScope(_preview==null || !_preview.IsRunning))if(GUILayout.Button("Выпустить"))_preview.RequestRelease();
                using(new EditorGUI.DisabledScope(_preview==null))if(GUILayout.Button(_playing?"Пауза":"Продолжить")){_playing=!_playing;_lastTime=EditorApplication.timeSinceStartup;}
            }
            if(GUILayout.Button("Стоп / вернуть исходную позу"))StopPreview();
            _loop=EditorGUILayout.Toggle("Повторять автопоказ",_loop);_speed=EditorGUILayout.Slider("Скорость просмотра",_speed,.25f,2f);
            float duration=_preview?.Duration??(_settings.enterSeconds+_settings.drawSeconds+_settings.holdSeconds+_settings.releaseSeconds+_settings.recoverSeconds);
            EditorGUI.BeginChangeCheck();float cursor=EditorGUILayout.Slider("Время, с",_cursor,0,duration);
            if(EditorGUI.EndChangeCheck())Try(()=>Scrub(cursor));
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("← 1 кадр"))Try(()=>Scrub(Mathf.Max(0,_cursor-1f/60f)));
                if(GUILayout.Button("1 кадр →"))Try(()=>Scrub(Mathf.Min(duration,_cursor+1f/60f)));
            }
            EditorGUILayout.LabelField("Метка выпуска",(_settings.releaseSeconds*_settings.releaseMarker*1000).ToString("F0")+" мс после начала Release");
            if(_preview!=null && _preview.HasPassedMarker)EditorGUILayout.LabelField("Выпуск", "Метка пройдена");
            EditorGUILayout.Space();GUILayout.Label("Темп движения",EditorStyles.boldLabel);
            using(new EditorGUI.DisabledScope(_playing))
            {
                _settings.enterSeconds=EditorGUILayout.Slider("Подъём рук, с",_settings.enterSeconds,.05f,1f);
                _settings.drawSeconds=EditorGUILayout.Slider("Натяжение, с",_settings.drawSeconds,.1f,2f);
                _settings.holdSeconds=EditorGUILayout.Slider("Автоудержание, с",_settings.holdSeconds,.1f,5f);
                _settings.releaseSeconds=EditorGUILayout.Slider("Выпуск, с",_settings.releaseSeconds,.05f,.6f);
                _settings.recoverSeconds=EditorGUILayout.Slider("Возврат, с",_settings.recoverSeconds,.05f,1.5f);
                _arcs=EditorGUILayout.Foldout(_arcs,"Отметка выстрела",true);
                if(_arcs)_settings.releaseMarker=EditorGUILayout.Slider("Момент выпуска",_settings.releaseMarker,.05f,.95f);
                if(GUILayout.Button("Сохранить темп"))Try(SaveSettings);
                if(GUILayout.Button("Собрать клипы Unity"))Try(()=>
                {StopPreview();SaveSettings();MagicPoseSequenceBaker.Bake(_lab,_start.text,_loaded.text,_release.text,_ready?_ready.text:null,_settings);});
                _sources=EditorGUILayout.Foldout(_sources,"Исходные позы",true);
                if(_sources){_start=(TextAsset)EditorGUILayout.ObjectField("Start",_start,typeof(TextAsset),false);_loaded=(TextAsset)EditorGUILayout.ObjectField("Loaded",_loaded,typeof(TextAsset),false);_release=(TextAsset)EditorGUILayout.ObjectField("Release",_release,typeof(TextAsset),false);_ready=(TextAsset)EditorGUILayout.ObjectField("Опущенные руки",_ready,typeof(TextAsset),false);}
            }
            EditorGUILayout.HelpBox("G — удержать и отпустить; пробел — пауза; Esc — вернуть позу. Метка выпуска обозначает будущий момент появления снаряда. Для правки самих поз остановите предпросмотр.",MessageType.None);
            if(!string.IsNullOrEmpty(_error))EditorGUILayout.HelpBox(_error,MessageType.Error);
            EditorGUILayout.EndScrollView();
        }
    }
}
