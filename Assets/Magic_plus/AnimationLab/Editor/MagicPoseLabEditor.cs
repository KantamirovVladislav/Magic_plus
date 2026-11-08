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
            if (GUI.changed) {lab.ApplyPose(); EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);}
            if (Mathf.Max(lab.leftReachError,lab.rightReachError)>0.025f)
                EditorGUILayout.HelpBox("Цель кисти дальше длины руки. Передвиньте её ближе к плечу.",MessageType.Warning);
        }
        public static void DrawControls(MagicPoseLab lab)
        {
            if (!lab) return;
            if(GUILayout.Button("Крупно: руки")) Frame(lab, true);
            if(GUILayout.Button("Показать всего персонажа")) Frame(lab, false);
            string[] labels={"Левая кисть", "Правая кисть", "Левый локоть", "Правый локоть", "Прицел"};
            for(int i=0;i<labels.Length;i++) if(GUILayout.Button(labels[i]))
            { Selection.activeTransform=lab.Controls[i]; Tools.current=Tool.Move; SceneView.RepaintAll(); }
            if(GUILayout.Button("Выбрать настройки манекена")) Selection.activeGameObject=lab.gameObject;
            if(GUILayout.Button("Вернуть исходную позу"))
            {
                Undo.RecordObjects(lab.Controls,"Reset magic pose controls");Undo.RecordObject(lab,"Reset magic pose");
                lab.ResetControls();EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);
            }
            if(GUILayout.Button("Сохранить позу в JSON…"))
            {
                string path=EditorUtility.SaveFilePanel("Сохранить позу",Application.dataPath+"/Magic_plus/AnimationLab","FireArrowPose","json");
                if(!string.IsNullOrEmpty(path)) {File.WriteAllText(path,lab.ExportJson());AssetDatabase.Refresh();}
            }
        }
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
            GUILayout.Label("Огненная стрела — настройка рук",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Выберите точку и двигайте её в Scene. W — перемещение, E — поворот. Play не требуется.",MessageType.Info);
            EditorGUI.BeginChangeCheck();
            float draw=EditorGUILayout.Slider("Натяжение",lab.drawAmount,0f,1f);
            bool follow=EditorGUILayout.Toggle("Следовать прицелу",lab.followAim);
            bool bones=EditorGUILayout.Toggle("Показать кости",lab.showSkeleton);
            if(EditorGUI.EndChangeCheck())
            {Undo.RecordObject(lab,"Change magic pose preview");lab.drawAmount=draw;lab.followAim=follow;lab.showSkeleton=bones;lab.ApplyPose();EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);}
            EditorGUILayout.Space();MagicPoseLabEditor.DrawControls(lab);
        }
        private void OnInspectorUpdate(){Repaint();}
    }
}
