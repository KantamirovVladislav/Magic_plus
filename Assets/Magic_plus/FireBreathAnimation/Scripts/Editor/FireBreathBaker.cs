using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using MagicPlus.FullSkeleton;

namespace MagicPlus.BreathAnimation
{
    public static class FireBreathBaker
    {
        public static void Bake(FullSkeletonPoseLab source, string ready, string gather, string hold, FireBreathSequence.Settings timing)
        {
            timing.Validate(); string folder = FireBreathAnimationWindow.RootPath + "/Clips";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder(FireBreathAnimationWindow.RootPath, "Clips");
            string backup = "Builds/AnimationLab/FireBreathAnimation/Backups/" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            Directory.CreateDirectory(backup);
            foreach (string file in Directory.GetFiles(folder))
                if (file.EndsWith(".anim") || file.EndsWith(".mask") || file.EndsWith(".meta")) File.Copy(file, backup + "/" + Path.GetFileName(file), true);
            var scene = EditorSceneManager.NewPreviewScene(); var root = UnityEngine.Object.Instantiate(source.gameObject);
            SceneManager.MoveGameObjectToScene(root, scene);
            var lab = root.GetComponent<FullSkeletonPoseLab>(); lab.previewActive = false;
            var sequence = new FireBreathSequence(lab, ready, gather, hold, timing);
            try
            {
                var bones = lab.Bones; var spine = lab.character.GetBoneTransform(HumanBodyBones.Spine);
                bool Upper(Transform bone) => bone == spine || bone.IsChildOf(spine);
                foreach (string name in new[] { "Enter", "Hold", "Exit", "PreviewCycle" })
                {
                    float duration = name == "Enter" ? timing.enterSeconds : name == "Hold" ? timing.holdCycleSeconds
                        : name == "Exit" ? timing.exitSeconds : sequence.Duration;
                    var curves = new Dictionary<Transform, AnimationCurve[]>();
                    foreach (var bone in bones) if (Upper(bone))
                    {
                        var c = new AnimationCurve[10]; for (int i = 0; i < c.Length; i++) c[i] = new AnimationCurve(); curves[bone] = c;
                    }
                    var previous = new Dictionary<Transform, Quaternion>();
                    int steps = Mathf.Max(1, Mathf.CeilToInt(duration * 60));
                    for (int f = 0; f <= steps; f++)
                    {
                        float t = duration * f / steps;
                        if (name == "Hold") sequence.SampleReleased(timing.enterSeconds + t, float.MaxValue);
                        else if (name == "Exit") sequence.SampleReleased(timing.enterSeconds + t, timing.enterSeconds);
                        else sequence.SampleAt(t);
                        foreach (var item in curves)
                        {
                            var bone = item.Key; var q = bone.localRotation.normalized; var p = bone.localPosition; var s = bone.localScale;
                            if (previous.TryGetValue(bone, out var last) && Quaternion.Dot(last, q) < 0)
                                q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                            previous[bone] = q;
                            float[] values = { q.x, q.y, q.z, q.w, p.x, p.y, p.z, s.x, s.y, s.z };
                            for (int i = 0; i < values.Length; i++) item.Value[i].AddKey(t, values[i]);
                        }
                    }
                    sequence.Restore();
                    var clip = new AnimationClip { name = "FireBreath_" + name, frameRate = 60, legacy = true,
                        wrapMode = name == "Hold" ? WrapMode.Loop : WrapMode.ClampForever };
                    string[] properties = { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w",
                        "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z", "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" };
                    foreach (var item in curves)
                    {
                        string path = AnimationUtility.CalculateTransformPath(item.Key, lab.character.transform);
                        for (int c = 0; c < item.Value.Length; c++)
                        {
                            // Positions/scales get curves only if the authored motion actually changes them.
                            bool varies = false; var keys = item.Value[c].keys;
                            for (int k = 1; k < keys.Length; k++) if (Mathf.Abs(keys[k].value - keys[0].value) > 1e-8f) { varies = true; break; }
                            if (c >= 4 && !varies) continue;
                            for (int k = 0; k < keys.Length; k++)
                            { AnimationUtility.SetKeyLeftTangentMode(item.Value[c], k, AnimationUtility.TangentMode.Linear); AnimationUtility.SetKeyRightTangentMode(item.Value[c], k, AnimationUtility.TangentMode.Linear); }
                            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), properties[c]), item.Value[c]);
                        }
                    }
                    clip.EnsureQuaternionContinuity();
                    var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = name == "Hold";
                    AnimationUtility.SetAnimationClipSettings(clip, settings); Save(clip, folder + "/" + clip.name + ".anim");
                }
                var mask = new AvatarMask { name = "FireBreath_UpperBody", transformCount = bones.Length };
                for (int i = 0; i < bones.Length; i++)
                { mask.SetTransformPath(i, AnimationUtility.CalculateTransformPath(bones[i], lab.character.transform)); mask.SetTransformActive(i, Upper(bones[i])); }
                foreach (AvatarMaskBodyPart part in Enum.GetValues(typeof(AvatarMaskBodyPart)))
                    if (part != AvatarMaskBodyPart.LastBodyPart) mask.SetHumanoidBodyPartActive(part, part == AvatarMaskBodyPart.Body || part == AvatarMaskBodyPart.Head
                        || part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers);
                Save(mask, folder + "/FireBreath_UpperBody.mask"); AssetDatabase.SaveAssets();
            }
            finally { sequence.Restore(); UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(scene); }
        }
        private static void Save(UnityEngine.Object generated, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (!existing) AssetDatabase.CreateAsset(generated, path);
            else { EditorUtility.CopySerialized(generated, existing); EditorUtility.SetDirty(existing); UnityEngine.Object.DestroyImmediate(generated); }
        }
    }
}
