using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MagicPlus.AnimationLab
{
    internal static class MagicAimControls
    {
        private static bool _angles;
        public static void Draw(MagicPoseLab lab)
        {
            EditorGUILayout.HelpBox("Красная линия — прицел, голубая — два пальца, фиолетовая — взгляд. После ручного поворота торса их можно согласовать отдельно.",MessageType.Info);
            EditorGUILayout.LabelField("Пальцы / прицел",Vector3.Angle(lab.LeftFingerDirection,lab.AimDirection).ToString("F1")+"°");
            EditorGUILayout.LabelField("Взгляд / прицел",Vector3.Angle(lab.HeadDirection,lab.AimDirection).ToString("F1")+"°");
            if(GUILayout.Button("Прицел по двум пальцам"))
            {
                MagicPoseLabEditor.RecordPoseUndo(lab,"Match aim to fingers");Undo.RecordObject(lab.aimFrame,"Match aim to fingers");
                lab.MatchAimToLeftFingers();Dirty(lab);
            }
            EditorGUILayout.HelpBox("Переставляет прицел, сохраняя видимую позу. Для изменения всей позы двигайте сам прицел с включённым IK.",MessageType.None);
            if(GUILayout.Button("Направить голову по прицелу"))
            {MagicPoseLabEditor.RecordPoseUndo(lab,"Align head to aim");lab.AlignHeadToAim();Dirty(lab);}
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Выбрать голову"))Select(lab,HumanBodyBones.Head);
                if(GUILayout.Button("Выбрать шею"))Select(lab,HumanBodyBones.Neck);
            }
            _angles=EditorGUILayout.Foldout(_angles,"Точный поворот головы и шеи",true);
            if(_angles){Angles(lab,HumanBodyBones.Neck,"Шея");Angles(lab,HumanBodyBones.Head,"Голова");}
            EditorGUILayout.HelpBox("Кнопки и поля поворота сохраняют правки сразу. После вращения кости вручную нажмите «Запомнить правки верха тела» на вкладке «Поза и руки».",MessageType.None);
        }
        private static void Select(MagicPoseLab lab,HumanBodyBones id)
        {
            Undo.RecordObject(lab,"Manual head editing");lab.previewEnabled=false;
            Selection.activeTransform=lab.character.GetBoneTransform(id);Tools.current=Tool.Rotate;Dirty(lab);
        }
        private static void Angles(MagicPoseLab lab,HumanBodyBones id,string label)
        {
            var bone=lab.character.GetBoneTransform(id);if(!bone)return;
            EditorGUI.BeginChangeCheck();var angles=EditorGUILayout.Vector3Field(label,bone.localEulerAngles);
            if(EditorGUI.EndChangeCheck())
            {MagicPoseLabEditor.RecordPoseUndo(lab,"Rotate head or neck");lab.SetUpperBoneRotation(id,Quaternion.Euler(angles));Dirty(lab);}
        }
        private static void Dirty(MagicPoseLab lab)
        {EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);SceneView.RepaintAll();}
    }
}
