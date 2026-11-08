using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MagicPlus.AnimationLab
{
    [CustomEditor(typeof(MagicPoseLab))]
    public sealed class MagicPoseLabEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            var lab = (MagicPoseLab)target;
            EditorGUILayout.HelpBox("Работает без Play. Выберите кисть или локоть кнопкой, затем W — движение, E — поворот. Ctrl+S сохраняет сцену. Прицел проверяет наведение вверх/вниз.", MessageType.Info);
            DrawControls(lab);
            EditorGUILayout.Space();
            DrawDefaultInspector();
            if (GUI.changed) {if(lab.previewEnabled)lab.ApplyPose(); EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);}
            if (Mathf.Max(lab.leftReachError,lab.rightReachError)>0.025f)
                EditorGUILayout.HelpBox("Цель кисти дальше длины руки. Передвиньте её ближе к плечу.",MessageType.Warning);
        }
        public static void DrawControls(MagicPoseLab lab)
        {
            if (!lab) return;
            var reference=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Magic_plus/AnimationLab/Poses/FireArrow_TwoFingerHold.json");
            using(new EditorGUI.DisabledScope(!reference))if(GUILayout.Button("Поза по референсам: два пальца"))
            {RecordPoseUndo(lab,"Load two-finger fire arrow pose");lab.LoadPoseJson(reference.text);EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);}
            var native=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Magic_plus/AnimationLab/LocalReference/NativeBowHold.json");
            using(new EditorGUI.DisabledScope(!native)) if(GUILayout.Button("Поза лука из игры"))
            {RecordPoseUndo(lab,"Load original bow pose");lab.LoadPoseJson(native.text);EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);}
            if(GUILayout.Button("Запомнить правки верха тела"))
            {RecordPoseUndo(lab,"Capture upper body pose");lab.CaptureUpperBodyEdits();EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);}
            if(GUILayout.Button("Загрузить позу из JSON…"))
            {
                string path=EditorUtility.OpenFilePanel("Загрузить позу",Application.dataPath+"/Magic_plus/AnimationLab","json");
                if(!string.IsNullOrEmpty(path)){RecordPoseUndo(lab,"Load magic pose");lab.LoadPoseJson(File.ReadAllText(path));EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);}
            }
            if(GUILayout.Button("Крупно: руки")) Frame(lab, true);
            if(GUILayout.Button("Показать всего персонажа")) Frame(lab, false);
            string[] labels={"Левая кисть", "Правая кисть", "Левый локоть", "Правый локоть", "Прицел"};
            for(int i=0;i<labels.Length;i++) if(GUILayout.Button(labels[i]))
            { Selection.activeTransform=lab.Controls[i]; Tools.current=Tool.Move; SceneView.RepaintAll(); }
            if(GUILayout.Button("Выбрать настройки манекена")) Selection.activeGameObject=lab.gameObject;
            if(GUILayout.Button("Сбросить цели рук"))
            {
                Undo.RecordObjects(lab.Controls,"Reset magic pose controls");Undo.RecordObject(lab,"Reset magic pose");
                lab.ResetControls();EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);
            }
            if(GUILayout.Button("Сохранить позу в JSON…"))
            {
                string path=EditorUtility.SaveFilePanel("Сохранить позу",Application.dataPath+"/Magic_plus/AnimationLab","FireArrowPose","json");
                if(!string.IsNullOrEmpty(path))
                {
                    if(!lab.previewEnabled){RecordPoseUndo(lab,"Save manual upper body pose");lab.CaptureUpperBodyEdits();EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);}
                    File.WriteAllText(path,lab.ExportJson());AssetDatabase.Refresh();
                }
            }
        }
        internal static void RecordPoseUndo(MagicPoseLab lab,string label)
        {Undo.RecordObject(lab,label);Undo.RecordObjects(lab.Controls,label);Undo.RecordObjects(lab.poseBones,label);}
        private static void Frame(MagicPoseLab lab, bool close)
        {
            var view=SceneView.lastActiveSceneView;
            if(!view)return;
            view.LookAtDirect(lab.transform.position+new Vector3(0,close ? 1.48f : 1.0f,0.15f),
                Quaternion.Euler(12,155,0),close ? 0.72f : 1.3f);
            view.Focus();
        }
    }

    public sealed class MagicPoseLabWindow : EditorWindow
    {
        private Vector2 _scroll;
        private int _tab;
        [MenuItem("MagicPlus/Pose Lab")]
        public static void Open()
        {
            var window=GetWindow<MagicPoseLabWindow>("MagicPlus — поза");
            window.minSize=new Vector2(310,480);
        }
        private void OnGUI()
        {
            var lab=Object.FindFirstObjectByType<MagicPoseLab>();
            if(!lab){EditorGUILayout.HelpBox("Откройте сцену MagicPlus_AnimationLab.",MessageType.Info);return;}
            _scroll=EditorGUILayout.BeginScrollView(_scroll);
            GUILayout.Label("Огненная стрела — настройка рук",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Выберите точку и двигайте её в Scene. W — перемещение, E — поворот. Play не требуется.",MessageType.Info);
            EditorGUILayout.LabelField("Основа",lab.basisName);
            EditorGUI.BeginChangeCheck();
            bool preview=EditorGUILayout.Toggle(new GUIContent("Предпросмотр (IK)","Выключите для ручного вращения торса и пальцев."),lab.previewEnabled);
            float draw=EditorGUILayout.Slider("Натяжение",lab.drawAmount,0f,1f);
            bool follow=EditorGUILayout.Toggle("Следовать прицелу",lab.followAim);
            bool bones=EditorGUILayout.Toggle("Показать кости",lab.showSkeleton);
            if(EditorGUI.EndChangeCheck())
            {Undo.RecordObject(lab,"Change magic pose preview");lab.previewEnabled=preview;lab.drawAmount=draw;lab.followAim=follow;lab.showSkeleton=bones;if(preview)lab.ApplyPose();EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);}
            if(!lab.previewEnabled)EditorGUILayout.HelpBox("Ручной режим. Вращайте кости, затем нажмите «Запомнить правки верха тела». Ноги останутся из исходной стойки.",MessageType.Info);
            EditorGUILayout.Space();_tab=GUILayout.Toolbar(_tab,new[]{"Поза и руки","Пальцы"});
            EditorGUILayout.Space();
            if(_tab==0)MagicPoseLabEditor.DrawControls(lab);else MagicFingerControls.Draw(lab);
            EditorGUILayout.EndScrollView();
        }
        private void OnInspectorUpdate(){Repaint();}
    }
}
