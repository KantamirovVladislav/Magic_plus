using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicPlus.FullSkeleton
{
    [DisallowMultipleComponent]
    public sealed class FullSkeletonPoseLab : MonoBehaviour
    {
        public Animator character;
        public TextAsset breathHold;
        public bool showSkeleton = true;
        [SerializeField, HideInInspector] private PoseDocument _neutral;
        public const string Format = "magicplus.full-skeleton";
        public Transform[] Bones => character ? character.GetComponentsInChildren<Transform>(true) : Array.Empty<Transform>();

        [Serializable]
        public sealed class BonePose
        {
            public string path;
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
        }
        [Serializable]
        public sealed class PoseDocument
        {
            public string format = Format;
            public int version = 1;
            public string name;
            public BonePose[] bones;
        }
        public string BonePath(Transform bone)
        {
            if (!character || !bone || (bone != character.transform && !bone.IsChildOf(character.transform)))
                throw new ArgumentException("Выберите кость этого манекена.");
            string path = "";
            for (var t = bone; t != character.transform; t = t.parent)
                path = t.name + (path.Length == 0 ? "" : "/" + path);
            return path;
        }
        public PoseDocument Capture(string poseName)
        {
            var bones = Bones;
            if (bones.Length == 0) throw new InvalidOperationException("Не назначен манекен.");
            var result = new PoseDocument { name = poseName, bones = new BonePose[bones.Length] };
            for (int i = 0; i < bones.Length; i++)
                result.bones[i] = new BonePose { path = BonePath(bones[i]), position = bones[i].localPosition,
                    rotation = bones[i].localRotation, scale = bones[i].localScale };
            Validate(result);
            return result;
        }
        public void CaptureNeutral() { _neutral = Capture("T-pose"); }
        public string ExportJson(string poseName) => JsonUtility.ToJson(Capture(poseName), true);
        public string ExportNeutralJson()
        {
            Validate(_neutral);
            return JsonUtility.ToJson(_neutral, true);
        }
        public void LoadJson(string json)
        {
            var pose = JsonUtility.FromJson<PoseDocument>(json);
            Apply(pose);
        }
        public void ResetPose() { Apply(_neutral); }
        public void ResetBone(Transform bone)
        {
            Validate(_neutral);
            string path = BonePath(bone);
            var item = Array.Find(_neutral.bones, p => p.path == path);
            ApplyBone(bone, item);
        }
        private Dictionary<string, Transform> RigMap()
        {
            var map = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var bone in Bones)
            {
                string path = BonePath(bone);
                if (map.ContainsKey(path)) throw new InvalidOperationException("Повторяющийся путь кости: " + path);
                map.Add(path, bone);
            }
            return map;
        }
        private Dictionary<string, Transform> Validate(PoseDocument pose)
        {
            if (pose == null || pose.format != Format || pose.version != 1)
                throw new ArgumentException("Нужен JSON из окна Full Skeleton Pose. Позы прежнего стенда используют другой формат.");
            var map = RigMap();
            if (pose.bones == null || pose.bones.Length != map.Count || map.Count == 0)
                throw new ArgumentException("Количество костей в JSON не совпадает с манекеном.");
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in pose.bones)
            {
                if (p == null || p.path == null || !paths.Add(p.path) || !map.ContainsKey(p.path))
                    throw new ArgumentException("Неверный или повторяющийся путь кости в JSON.");
                if (!Finite(p.position) || !Finite(p.scale) || Mathf.Abs(p.scale.x) < 1e-7f
                    || Mathf.Abs(p.scale.y) < 1e-7f || Mathf.Abs(p.scale.z) < 1e-7f
                    || !Finite(p.rotation.x) || !Finite(p.rotation.y) || !Finite(p.rotation.z) || !Finite(p.rotation.w))
                    throw new ArgumentException("Некорректные координаты кости: " + p.path);
                float length = p.rotation.x * p.rotation.x + p.rotation.y * p.rotation.y
                    + p.rotation.z * p.rotation.z + p.rotation.w * p.rotation.w;
                if (!Finite(length) || length < 1e-8f)
                    throw new ArgumentException("Некорректный поворот кости: " + p.path);
            }
            return map;
        }
        private void Apply(PoseDocument pose)
        {
            // Validate the complete document first: a bad file must not partially change the pose.
            var map = Validate(pose);
            foreach (var p in pose.bones) ApplyBone(map[p.path], p);
        }
        private static void ApplyBone(Transform bone, BonePose pose)
        {
            bone.localPosition = pose.position;
            bone.localRotation = pose.rotation.normalized;
            bone.localScale = pose.scale;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private void OnDrawGizmos()
        {
            if (!showSkeleton || !character) return;
            var bones = Bones;
            Gizmos.color = new Color(.5f, .85f, 1f, .85f);
            foreach (var bone in bones)
            {
                if (bone == character.transform || bone.name == "body") continue;
                if (bone.parent && bone.parent.IsChildOf(character.transform)) Gizmos.DrawLine(bone.parent.position, bone.position);
                Gizmos.DrawSphere(bone.position, .006f);
            }
#if UNITY_EDITOR
            var selected = UnityEditor.Selection.activeTransform;
            if (selected && selected.IsChildOf(character.transform))
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(selected.position, .025f);
            }
#endif
        }
    }
}
