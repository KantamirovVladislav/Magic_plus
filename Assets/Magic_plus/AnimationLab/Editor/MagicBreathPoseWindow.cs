using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MagicPlus.AnimationLab
{
    public sealed class MagicBreathPoseWindow : EditorWindow
    {
        public const string ScenePath = "Assets/Magic_plus/AnimationLab/Scenes/MagicPlus_FireBreathHold.unity";
        public const string PosePath = "Assets/Magic_plus/AnimationLab/Poses/FireBreath/FireBreath_Hold_Draft.json";
        [SerializeField] private MagicPoseLab _lab;
        private Vector2 _scroll;
        private int _tab;
        private bool _torso;

        [MenuItem("MagicPlus/Fire Breath Pose")]
        public static void Open()
        {
            var window = GetWindow<MagicBreathPoseWindow>("Огненное дыхание — поза");
            window.minSize = new Vector2(360, 580);
            window.Show();
        }
        public static void OpenFor(MagicPoseLab lab)
        {
            Open();
            var window = GetWindow<MagicBreathPoseWindow>();
            window._lab = lab;
            MagicFingerControls.SelectRightHand();
            window.Repaint();
        }
        private void OnGUI()
        {
            if (!_lab)
                foreach (var candidate in Object.FindObjectsByType<MagicPoseLab>(FindObjectsSortMode.None))
                    if (candidate.gameObject.scene.path == ScenePath) _lab = candidate;
            if (!_lab)
            {
                EditorGUILayout.HelpBox("Откройте сцену удержания огненного дыхания.", MessageType.Info);
                if (GUILayout.Button("Открыть сцену дыхания") && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            var lab = _lab;
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Label("Огненное дыхание — удержание", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Работает без Play. W — двигать кисть или локоть, E — поворачивать. Ctrl+S сохраняет сцену.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            bool preview = EditorGUILayout.Toggle("Предпросмотр (IK)", lab.previewEnabled);
            bool skeleton = EditorGUILayout.Toggle("Показать кости", lab.showSkeleton);
            if (EditorGUI.EndChangeCheck())
            {
                MagicPoseLabEditor.RecordPoseUndo(lab, "Change breath pose preview");
                lab.previewEnabled = preview; lab.showSkeleton = skeleton; lab.drawAmount = 1f;
                if (preview) lab.ApplyPose();
                Dirty();
            }
            _tab = GUILayout.Toolbar(_tab, new[] { "Поза и руки", "Пальцы", "Торс и взгляд" });
            EditorGUILayout.Space();
            if (_tab == 0)
            {
                Control("Правая кисть", lab.rightHandTarget);
                Control("Правый локоть", lab.rightElbowHint);
                Control("Левая кисть", lab.leftHandTarget);
                Control("Левый локоть", lab.leftElbowHint);
                if (GUILayout.Button("Крупно: лицо и руки")) Frame(true);
                if (GUILayout.Button("Показать всего персонажа")) Frame(false);
                CaptureButton();
                if (GUILayout.Button("Вернуть исходную тестовую позу"))
                {
                    MagicPoseLabEditor.RecordPoseUndo(lab, "Load breath hold draft");
                    lab.followAim = false; lab.LoadPoseJson(File.ReadAllText(PosePath)); Dirty();
                }
            }
            else if (_tab == 1) MagicFingerControls.Draw(lab);
            else
            {
                EditorGUILayout.HelpBox("Для точной позы меняйте торс и шею. Затем проверьте положение пальцев относительно подбородка.", MessageType.Info);
                _torso = EditorGUILayout.Foldout(_torso, "Повороты торса", true);
                if (_torso)
                {
                    Angles(HumanBodyBones.Spine, "Низ спины");
                    Angles(HumanBodyBones.Chest, "Грудь");
                    Angles(HumanBodyBones.UpperChest, "Верх груди");
                }
                Angles(HumanBodyBones.Neck, "Шея");
                Angles(HumanBodyBones.Head, "Голова");
                if (GUILayout.Button("Направить голову по прицелу"))
                {
                    MagicPoseLabEditor.RecordPoseUndo(lab, "Align breath head");
                    lab.AlignHeadToAim(); Dirty();
                }
                CaptureButton();
            }
            if (Mathf.Max(lab.leftReachError, lab.rightReachError) > .025f)
                EditorGUILayout.HelpBox("Цель кисти дальше длины руки. Передвиньте её ближе к плечу.", MessageType.Warning);
            EditorGUILayout.Space();
            if (GUILayout.Button("Загрузить позу из JSON…"))
            {
                string path = EditorUtility.OpenFilePanel("Поза дыхания", Application.dataPath + "/Magic_plus/AnimationLab/Poses/FireBreath", "json");
                if (!string.IsNullOrEmpty(path))
                {
                    MagicPoseLabEditor.RecordPoseUndo(lab, "Load breath pose");
                    lab.followAim = false; lab.LoadPoseJson(File.ReadAllText(path)); Dirty();
                }
            }
            if (GUILayout.Button("Сохранить позу в JSON…"))
            {
                string path = EditorUtility.SaveFilePanel("Сохранить позу дыхания", Application.dataPath + "/Magic_plus/AnimationLab/Poses/FireBreath", "FireBreath_Hold", "json");
                if (!string.IsNullOrEmpty(path))
                {
                    if (!lab.previewEnabled) Capture();
                    File.WriteAllText(path, lab.ExportJson());
                    AssetDatabase.Refresh();
                }
            }
            EditorGUILayout.EndScrollView();
        }
        private void Control(string label, Transform control)
        {
            if (!GUILayout.Button(label)) return;
            Selection.activeTransform = control;
            Tools.current = Tool.Move;
            SceneView.RepaintAll();
        }
        private void CaptureButton()
        {
            if (GUILayout.Button("Запомнить правки верха тела")) Capture();
        }
        private void Capture()
        {
            MagicPoseLabEditor.RecordPoseUndo(_lab, "Capture breath upper body");
            _lab.CaptureUpperBodyEdits();
            _lab.basisName = "Огненное дыхание — удержание";
            Dirty();
        }
        private void Angles(HumanBodyBones id, string label)
        {
            var bone = _lab.character.GetBoneTransform(id);
            if (!bone) return;
            EditorGUI.BeginChangeCheck();
            var angles = EditorGUILayout.Vector3Field(label, bone.localEulerAngles);
            if (!EditorGUI.EndChangeCheck()) return;
            MagicPoseLabEditor.RecordPoseUndo(_lab, "Rotate breath torso");
            _lab.SetUpperBoneRotation(id, Quaternion.Euler(angles)); Dirty();
        }
        private void Dirty()
        {
            EditorSceneManager.MarkSceneDirty(_lab.gameObject.scene);
            SceneView.RepaintAll();
        }
        private void Frame(bool close)
        {
            var view = SceneView.lastActiveSceneView;
            if (!view) return;
            view.LookAtDirect(_lab.transform.position + new Vector3(0, close ? 1.48f : 1f, .15f),
                Quaternion.LookRotation(new Vector3(-2f, -.4f, -3f)), close ? .65f : 1.25f);
            view.Focus();
        }
        private void OnInspectorUpdate() { Repaint(); }
    }
}
