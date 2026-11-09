using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace MagicPlus.FullSkeleton
{
    public sealed class FullSkeletonPoseWindow : EditorWindow
    {
        public const string RootPath = "Assets/Magic_plus/FullSkeletonLab";
        public const string ScenePath = RootPath + "/Scenes/MagicPlus_FullSkeleton.unity";
        [SerializeField] private FullSkeletonPoseLab _lab;
        private Vector2 _scroll, _boneScroll;
        private string _search = "", _error;
        private bool _list = true;

        [MenuItem("MagicPlus/Full Skeleton Pose")]
        public static void Open()
        {
            var window = GetWindow<FullSkeletonPoseWindow>("Весь скелет — поза");
            window.minSize = new Vector2(390, 630);
            window.Show();
        }
        public static void OpenFor(FullSkeletonPoseLab lab)
        {
            Open();
            var window = GetWindow<FullSkeletonPoseWindow>();
            window._lab = lab;
            window.Repaint();
        }
        private void OnGUI()
        {
            if (!_lab)
                foreach (var lab in UnityEngine.Object.FindObjectsByType<FullSkeletonPoseLab>(FindObjectsSortMode.None))
                    if (lab.gameObject.scene == SceneManager.GetActiveScene()) _lab = lab;
            if (!_lab)
            {
                EditorGUILayout.HelpBox("Откройте сцену с полным скелетом персонажа.", MessageType.Info);
                if (GUILayout.Button("Открыть сцену") && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    EditorSceneManager.OpenScene(ScenePath);
                return;
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Label("Полный скелет персонажа", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Выберите кость. E — вращение, W — перемещение, Local — оси кости. Автоматическая анимация отключена. Ctrl+Z отменяет правки.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            bool show = EditorGUILayout.Toggle("Показать скелет", _lab.showSkeleton);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_lab, "Toggle skeleton guides");
                _lab.showSkeleton = show; Dirty();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Персонаж целиком")) Frame(false);
                if (GUILayout.Button("Выбранная кость")) Frame(true);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Таз")) Select(HumanBodyBones.Hips);
                if (GUILayout.Button("Спина")) Select(HumanBodyBones.Spine);
                if (GUILayout.Button("Голова")) Select(HumanBodyBones.Head);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Левая кисть")) Select(HumanBodyBones.LeftHand);
                if (GUILayout.Button("Правая кисть")) Select(HumanBodyBones.RightHand);
                if (GUILayout.Button("Стопы")) Select(HumanBodyBones.LeftFoot);
            }
            DrawSelected();
            _list = EditorGUILayout.Foldout(_list, "Все кости и объекты скелета (" + _lab.Bones.Length + ")", true);
            if (_list)
            {
                _search = EditorGUILayout.TextField("Поиск", _search);
                _boneScroll = EditorGUILayout.BeginScrollView(_boneScroll, GUILayout.Height(230));
                foreach (var bone in _lab.Bones)
                {
                    string path = _lab.BonePath(bone);
                    if (_search.Length > 0 && path.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    int depth = path.Length == 0 ? 0 : path.Split('/').Length;
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(Mathf.Min(depth, 8) * 9);
                        var style = Selection.activeTransform == bone ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
                        if (GUILayout.Button(new GUIContent(bone.name, path), style)) Select(bone);
                    }
                }
                EditorGUILayout.EndScrollView();
            }
            EditorGUILayout.Space();
            if (GUILayout.Button("Сбросить весь скелет в T-позу")) Try(() =>
            {
                Record(); _lab.ResetPose(); Dirty();
            });
            if (_lab.breathHold && GUILayout.Button("Загрузить FireBreath_Hold_v1")) Try(() =>
            {
                Record(); _lab.LoadJson(_lab.breathHold.text); Dirty();
            });
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Загрузить JSON…")) Load();
                if (GUILayout.Button("Сохранить JSON…")) Save();
            }
            EditorGUILayout.HelpBox("JSON сохраняет положение, поворот и масштаб всех объектов скелета. Положение костей влияет на длины суставов и деформацию модели.", MessageType.None);
            if (!string.IsNullOrEmpty(_error)) EditorGUILayout.HelpBox(_error, MessageType.Error);
            EditorGUILayout.EndScrollView();
        }
        private void DrawSelected()
        {
            var bone = Selection.activeTransform;
            if (!bone || !_lab.character || (bone != _lab.character.transform && !bone.IsChildOf(_lab.character.transform))) return;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Выбрано: " + bone.name, EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            var position = EditorGUILayout.Vector3Field("Положение (Local)", bone.localPosition);
            var angles = EditorGUILayout.Vector3Field("Поворот (Local)", bone.localEulerAngles);
            var scale = EditorGUILayout.Vector3Field("Масштаб (Local)", bone.localScale);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(bone, "Edit full skeleton bone");
                bone.localPosition = position; bone.localRotation = Quaternion.Euler(angles); bone.localScale = scale;
                Dirty();
            }
            if (GUILayout.Button("Сбросить выбранную кость")) Try(() =>
            {
                Undo.RecordObject(bone, "Reset skeleton bone");
                _lab.ResetBone(bone); Dirty();
            });
        }
        private void Select(HumanBodyBones id)
        {
            if (_lab.character) Select(_lab.character.GetBoneTransform(id));
        }
        private void Select(Transform bone)
        {
            if (!bone) return;
            Selection.activeTransform = bone; EditorGUIUtility.PingObject(bone.gameObject);
            Tools.current = Tool.Rotate; Tools.pivotRotation = PivotRotation.Local;
            SceneView.RepaintAll();
        }
        private void Frame(bool selected)
        {
            var view = SceneView.lastActiveSceneView;
            if (!view) return;
            var bone = Selection.activeTransform;
            bool focus = selected && bone && _lab.character && (bone == _lab.character.transform || bone.IsChildOf(_lab.character.transform));
            var point = focus ? bone.position : _lab.transform.position + Vector3.up;
            view.LookAtDirect(point, Quaternion.LookRotation(new Vector3(-2f, -.4f, -3f)), focus ? .32f : 1.3f);
            view.Focus();
        }
        private void Record() { Undo.RecordObjects(_lab.Bones, "Apply full skeleton pose"); }
        private void Dirty()
        {
            EditorSceneManager.MarkSceneDirty(_lab.gameObject.scene);
            SceneView.RepaintAll();
        }
        private void Try(Action action)
        {
            try { action(); _error = null; }
            catch (Exception error) { _error = error.Message; Debug.LogWarning("Full Skeleton Pose: " + error.Message); }
        }
        private void Save()
        {
            string folder = Path.GetFullPath(RootPath + "/Poses");
            string path = EditorUtility.SaveFilePanel("Сохранить полную позу", folder, "FullSkeletonPose", "json");
            if (string.IsNullOrEmpty(path)) return;
            Try(() =>
            {
                File.WriteAllText(path, _lab.ExportJson(Path.GetFileNameWithoutExtension(path)));
                AssetDatabase.Refresh();
            });
        }
        private void Load()
        {
            string path = EditorUtility.OpenFilePanel("Загрузить полную позу", Path.GetFullPath(RootPath + "/Poses"), "json");
            if (string.IsNullOrEmpty(path)) return;
            Try(() => { string json = File.ReadAllText(path); Record(); _lab.LoadJson(json); Dirty(); });
        }
        private void OnInspectorUpdate() { Repaint(); }
    }

    [CustomEditor(typeof(FullSkeletonPoseLab))]
    public sealed class FullSkeletonPoseInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            var lab = (FullSkeletonPoseLab)target;
            EditorGUILayout.HelpBox("Все кости редактируются вручную в Hierarchy или окне Full Skeleton Pose.", MessageType.Info);
            EditorGUILayout.LabelField("Объектов скелета", lab.Bones.Length.ToString());
            if (GUILayout.Button("Открыть редактор полного скелета")) FullSkeletonPoseWindow.OpenFor(lab);
        }
    }
}
