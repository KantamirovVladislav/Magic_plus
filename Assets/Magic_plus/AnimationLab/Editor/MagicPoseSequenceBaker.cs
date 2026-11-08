using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace MagicPlus.AnimationLab
{
    public static class MagicPoseSequenceBaker
    {
        public const string Output="Assets/Magic_plus/AnimationLab/Animation/Clips";
        public static string[] Bake(MagicPoseLab lab,string start,string loaded,string release,string ready,MagicPoseSequence.Settings settings)
        {
            settings.Validate();var sequence=new MagicPoseSequence(lab,start,loaded,release,ready,settings);
            if(!AssetDatabase.IsValidFolder(Output))AssetDatabase.CreateFolder("Assets/Magic_plus/AnimationLab/Animation","Clips");
            string backup="Assets/Magic_plus/AnimationLab/LocalReference/Backups/AnimationBake_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string[] names={"Enter","Draw","Hold","Release","Recover"};
            float[] lengths={settings.enterSeconds,settings.drawSeconds,settings.holdSeconds,settings.releaseSeconds,settings.recoverSeconds};
            var result=new List<string>();float offset=0;
            try
            {
                for(int part=0;part<names.Length;part++)
                {
                    var clip=new AnimationClip{name="FireArrow_"+names[part],frameRate=60,legacy=true,wrapMode=names[part]=="Hold"?WrapMode.Loop:WrapMode.ClampForever};
                    var curves=new Dictionary<Transform,AnimationCurve[]>();
                    foreach(var bone in lab.poseBones)if(lab.IsUpperBody(bone))curves[bone]=new[]{new AnimationCurve(),new AnimationCurve(),new AnimationCurve(),new AnimationCurve()};
                    var previous=new Dictionary<Transform,Quaternion>();
                    int frames=Mathf.CeilToInt(lengths[part]*60);
                    for(int frame=0;frame<=frames;frame++)
                    {
                        float time=Mathf.Min(lengths[part],frame/60f);sequence.SampleAt(offset+time);
                        foreach(var item in curves)
                        {
                            Quaternion q=item.Key.localRotation.normalized;
                            if(previous.TryGetValue(item.Key,out var prev) && Quaternion.Dot(prev,q)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);
                            previous[item.Key]=q;float[] values={q.x,q.y,q.z,q.w};
                            for(int axis=0;axis<4;axis++)item.Value[axis].AddKey(new Keyframe(time,values[axis]));
                        }
                    }
                    string[] channels={"x","y","z","w"};
                    foreach(var item in curves)for(int axis=0;axis<4;axis++)
                    {
                        var curve=item.Value[axis];bool constant=true;float value=curve[0].value;
                        for(int key=1;key<curve.length;key++)if(Mathf.Abs(curve[key].value-value)>.0000001f){constant=false;break;}
                        if(constant)curve=new AnimationCurve(new Keyframe(0,value),new Keyframe(lengths[part],value));
                        for(int key=0;key<curve.length;key++){AnimationUtility.SetKeyLeftTangentMode(curve,key,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,key,AnimationUtility.TangentMode.Linear);}
                        string path=AnimationUtility.CalculateTransformPath(item.Key,lab.character.transform);
                        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),"m_LocalRotation."+channels[axis]),curve);
                    }
                    clip.EnsureQuaternionContinuity();var options=AnimationUtility.GetAnimationClipSettings(clip);options.loopTime=names[part]=="Hold";AnimationUtility.SetAnimationClipSettings(clip,options);
                    string asset=Output+"/"+clip.name+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(asset);
                    if(existing)
                    {
                        Directory.CreateDirectory(backup);File.Copy(asset,backup+"/"+Path.GetFileName(asset),true);
                        EditorUtility.CopySerialized(clip,existing);EditorUtility.SetDirty(existing);UnityEngine.Object.DestroyImmediate(clip);
                    }
                    else AssetDatabase.CreateAsset(clip,asset);
                    result.Add(asset);offset+=lengths[part];
                }
                var mask=new AvatarMask{name="FireArrow_UpperBody",transformCount=lab.poseBones.Length};
                for(int i=0;i<lab.poseBones.Length;i++){mask.SetTransformPath(i,AnimationUtility.CalculateTransformPath(lab.poseBones[i],lab.character.transform));mask.SetTransformActive(i,lab.IsUpperBody(lab.poseBones[i]));}
                for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,false);
                foreach(var part in new[]{AvatarMaskBodyPart.Body,AvatarMaskBodyPart.Head,AvatarMaskBodyPart.LeftArm,AvatarMaskBodyPart.RightArm,AvatarMaskBodyPart.LeftFingers,AvatarMaskBodyPart.RightFingers})mask.SetHumanoidBodyPartActive(part,true);
                string maskPath=Output+"/FireArrow_UpperBody.mask";var oldMask=AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
                if(oldMask){Directory.CreateDirectory(backup);File.Copy(maskPath,backup+"/"+Path.GetFileName(maskPath),true);EditorUtility.CopySerialized(mask,oldMask);EditorUtility.SetDirty(oldMask);UnityEngine.Object.DestroyImmediate(mask);}else AssetDatabase.CreateAsset(mask,maskPath);
                result.Add(maskPath);AssetDatabase.SaveAssets();return result.ToArray();
            }
            finally{sequence.Restore();}
        }
    }
}
