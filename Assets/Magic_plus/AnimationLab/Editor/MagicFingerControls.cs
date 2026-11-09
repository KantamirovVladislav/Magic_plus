using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace MagicPlus.AnimationLab
{
    internal static class MagicFingerControls
    {
        private static int _side, _finger=1, _joint;
        private static bool _open=true, _details;
        private static readonly string[] Names={"Большой","Указательный","Средний","Безымянный","Мизинец"};
        public static void SelectRightHand() { _side=1; _finger=1; }
        public static void Draw(MagicPoseLab lab)
        {
            _open=EditorGUILayout.Foldout(_open,"Кисти и пальцы",true);
            if(!_open)return;
            _side=GUILayout.Toolbar(_side,new[]{"Левая рука","Правая рука"});
            bool left=_side==0;
            var target=left ? lab.leftHandTarget : lab.rightHandTarget;
            var hand=lab.character.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            var rotationObject=lab.previewEnabled ? target : hand;
            EditorGUI.BeginChangeCheck();
            var angle=EditorGUILayout.Vector3Field("Поворот кисти",rotationObject.localEulerAngles);
            if(EditorGUI.EndChangeCheck())
            {
                MagicPoseLabEditor.RecordPoseUndo(lab,"Rotate magic hand");
                rotationObject.localEulerAngles=angle;
                if(lab.previewEnabled)lab.ApplyPose();lab.RememberCurrentInput();Dirty(lab);
            }
            if(GUILayout.Button("Повернуть кисть в Scene")){Selection.activeTransform=rotationObject;Tools.current=Tool.Rotate;}
            _finger=EditorGUILayout.Popup("Палец",_finger,Names);
            var value=lab.ReadFinger(left,_finger);
            EditorGUI.BeginChangeCheck();
            float bend=EditorGUILayout.Slider("Общий сгиб",(value.first+value.second+value.third)/3f,0f,1f);
            if(EditorGUI.EndChangeCheck())
            {
                MagicPoseLabEditor.RecordPoseUndo(lab,"Bend magic finger");
                value.first=value.second=value.third=bend;lab.SetFinger(left,_finger,value);Dirty(lab);
            }
            _details=EditorGUILayout.Foldout(_details,"Отдельные суставы",true);
            EditorGUI.BeginChangeCheck();
            value.spread=EditorGUILayout.Slider("Разведение",value.spread,-2f,2f);
            if(_details)
            {
                value.first=EditorGUILayout.Slider("Первый сустав",value.first,-0.5f,1.5f);
                value.second=EditorGUILayout.Slider("Средний сустав",value.second,-0.5f,1.5f);
                value.third=EditorGUILayout.Slider("Концевой сустав",value.third,-0.5f,1.5f);
            }
            if(EditorGUI.EndChangeCheck())
            {
                MagicPoseLabEditor.RecordPoseUndo(lab,"Shape magic finger");lab.SetFinger(left,_finger,value);Dirty(lab);
            }
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button("Выпрямить")){MagicPoseLabEditor.RecordPoseUndo(lab,"Open magic finger");lab.SetFinger(left,_finger,new MagicPoseLab.FingerPose(0,0,0,value.spread));Dirty(lab);}
                if(GUILayout.Button("Согнуть")){MagicPoseLabEditor.RecordPoseUndo(lab,"Close magic finger");lab.SetFinger(left,_finger,new MagicPoseLab.FingerPose(1,1,1,value.spread));Dirty(lab);}
            }
            _joint=EditorGUILayout.Popup("Сустав в Scene",_joint,new[]{"Первый","Средний","Концевой"});
            if(GUILayout.Button("Редактировать сустав вручную"))
            {
                Undo.RecordObject(lab,"Manual finger editing");lab.previewEnabled=false;
                Selection.activeTransform=lab.FingerBone(left,_finger,_joint);Tools.current=Tool.Rotate;Dirty(lab);
            }
            EditorGUILayout.HelpBox("Сгиб: 0 — раскрыт, 1 — согнут. Для точной правки выберите сустав. Ноги не меняются.",MessageType.None);
        }
        private static void Dirty(MagicPoseLab lab)
        {EditorSceneManager.MarkSceneDirty(lab.gameObject.scene);SceneView.RepaintAll();}
    }
}
