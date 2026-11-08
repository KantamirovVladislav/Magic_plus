using System;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MagicPlus.AnimationLab
{
    // Authoring component used only by the isolated animation laboratory scene.
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class MagicPoseLab : MonoBehaviour
    {
        public Animator character;
        [HideInInspector] public AnimationClip basePose;
        public Transform aimFrame, aimTarget;
        public Transform leftHandTarget, rightHandTarget, leftElbowHint, rightElbowHint;
        public LineRenderer arrowGuide;
        public Vector3 shoulderPivot;
        [Range(0f, 1f)] public float drawAmount = 1f;
        [HideInInspector, Range(0f, 1f)] public float sampleTime = 0.3f;
        [Range(0f, 1f)] public float bodyAimWeight = 0.35f;
        [Range(10f, 85f)] public float pitchLimit = 65f;
        [Range(10f, 90f)] public float yawLimit = 70f;
        public bool previewEnabled = true;
        public bool followAim = true;
        public bool showSkeleton = true;
        public bool showGuides = true;
        [HideInInspector] public Vector3[] defaultPositions;
        [HideInInspector] public Quaternion[] defaultRotations;
        [HideInInspector] public Transform[] poseBones;
        [HideInInspector] public Vector3[] restPositions;
        [HideInInspector] public Quaternion[] restRotations;
        [NonSerialized] public float leftReachError, rightReachError;
        private bool _applying;
        private int _lastHash;
        [HideInInspector] public string basisName = "Исходный скелет";

        private Transform Bone(HumanBodyBones bone) => character.GetBoneTransform(bone);
        public Transform[] Controls => new[] { leftHandTarget, rightHandTarget, leftElbowHint, rightElbowHint, aimTarget };

        private void OnEnable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
            EditorApplication.update += EditorTick;
#endif
            _lastHash = 0;
        }
        private void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.update -= EditorTick;
#endif
        }
        private void OnValidate() { _lastHash = 0; }
        private void Update() { if (Application.isPlaying && previewEnabled) ApplyPose(); }
        private int InputHash()
        {
            int hash = drawAmount.GetHashCode() ^ sampleTime.GetHashCode() * 7 ^ bodyAimWeight.GetHashCode() * 13
                ^ pitchLimit.GetHashCode() * 17 ^ yawLimit.GetHashCode() * 19 ^ followAim.GetHashCode();
            foreach (var control in Controls) if (control) hash = hash * 31 + control.localToWorldMatrix.GetHashCode();
            return hash;
        }
        public void RememberCurrentInput() { _lastHash = InputHash(); }
#if UNITY_EDITOR
        private void EditorTick()
        {
            if (Application.isPlaying || !previewEnabled || !character || !aimFrame || !aimTarget) return;
            int hash = InputHash();
            if (_lastHash == hash) return;
            ApplyPose();
            RememberCurrentInput();
            SceneView.RepaintAll();
        }
#endif
        public void ApplyPose()
        {
            if (_applying || !character || !character.avatar || !character.avatar.isHuman || !aimFrame || !aimTarget) return;
            if (!leftHandTarget || !rightHandTarget || !leftElbowHint || !rightElbowHint) return;
            _applying = true;
            try
            {
                Vector3 position = character.transform.localPosition, scale = character.transform.localScale;
                Quaternion rotation = character.transform.localRotation;
                if (poseBones != null && restPositions != null && restRotations != null)
                    for (int i=0;i<poseBones.Length;i++) if(poseBones[i])
                    {poseBones[i].localPosition=restPositions[i];poseBones[i].localRotation=restRotations[i];}
                if (basePose) basePose.SampleAnimation(character.gameObject, sampleTime * basePose.length);
                character.transform.localPosition = position;
                character.transform.localRotation = rotation;
                character.transform.localScale = scale;
                aimFrame.position = transform.TransformPoint(shoulderPivot);
                Vector3 direction = aimTarget.position - aimFrame.position;
                if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;
                Vector3 local = transform.InverseTransformDirection(direction.normalized);
                float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -yawLimit, yawLimit);
                float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg, -pitchLimit, pitchLimit);
                Quaternion offset = followAim ? Quaternion.Euler(-pitch, yaw, 0f) : Quaternion.identity;
                aimFrame.rotation = transform.rotation * offset;
                var chest = Bone(HumanBodyBones.Chest);
                if (chest) chest.rotation = transform.rotation * Quaternion.Slerp(Quaternion.identity, offset, bodyAimWeight)
                    * Quaternion.Inverse(transform.rotation) * chest.rotation;
                Solve(Bone(HumanBodyBones.LeftUpperArm), Bone(HumanBodyBones.LeftLowerArm), Bone(HumanBodyBones.LeftHand),
                    leftHandTarget.position, leftHandTarget.rotation, leftElbowHint.position);
                Vector3 start = Vector3.Lerp(leftHandTarget.position, rightHandTarget.position, 0.55f);
                Vector3 rightPosition = Vector3.Lerp(start, rightHandTarget.position, drawAmount);
                Solve(Bone(HumanBodyBones.RightUpperArm), Bone(HumanBodyBones.RightLowerArm), Bone(HumanBodyBones.RightHand),
                    rightPosition, rightHandTarget.rotation, rightElbowHint.position);
                leftReachError = Vector3.Distance(Bone(HumanBodyBones.LeftHand).position, leftHandTarget.position);
                rightReachError = Vector3.Distance(Bone(HumanBodyBones.RightHand).position, rightPosition);
                if (arrowGuide)
                {
                    arrowGuide.enabled = showGuides;
                    arrowGuide.positionCount = 2;
                    arrowGuide.SetPosition(0, Bone(HumanBodyBones.RightHand).position);
                    arrowGuide.SetPosition(1, Bone(HumanBodyBones.LeftHand).position + aimFrame.forward * 0.24f);
                }
            }
            finally { _applying = false; }
        }

        // Analytic two-bone preview solver. It preserves bone lengths and needs no runtime package.
        private static void Solve(Transform upper, Transform lower, Transform hand, Vector3 target, Quaternion grip, Vector3 hint)
        {
            if (!upper || !lower || !hand) return;
            Vector3 root = upper.position, delta = target - root;
            float a = Vector3.Distance(root, lower.position), b = Vector3.Distance(lower.position, hand.position);
            if (a < 0.0001f || b < 0.0001f || delta.sqrMagnitude < 0.0000001f) return;
            Vector3 forward = delta.normalized;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + 0.0001f, a + b - 0.0001f);
            Vector3 pole = Vector3.ProjectOnPlane(hint - root, forward);
            if (pole.sqrMagnitude < 0.000001f) pole = Vector3.ProjectOnPlane(lower.position - root, forward);
            if (pole.sqrMagnitude < 0.000001f) pole = Vector3.Cross(forward, Vector3.up);
            if (pole.sqrMagnitude < 0.000001f) pole = Vector3.Cross(forward, Vector3.right);
            pole.Normalize();
            float cosine = Mathf.Clamp((a * a + distance * distance - b * b) / (2f * a * distance), -1f, 1f);
            Vector3 elbow = root + forward * (a * cosine) + pole * (a * Mathf.Sqrt(Mathf.Max(0f, 1f - cosine * cosine)));
            upper.rotation = Quaternion.FromToRotation(lower.position - root, elbow - root) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, target - lower.position) * lower.rotation;
            hand.rotation = grip;
        }

        public void CaptureDefaults()
        {
            defaultPositions = new Vector3[Controls.Length]; defaultRotations = new Quaternion[Controls.Length];
            for (int i = 0; i < Controls.Length; i++)
            { defaultPositions[i] = Controls[i].localPosition; defaultRotations[i] = Controls[i].localRotation; }
        }
        public void CaptureRestPose()
        {
            poseBones = character.GetComponentsInChildren<Transform>(true);
            restPositions = new Vector3[poseBones.Length]; restRotations = new Quaternion[poseBones.Length];
            for(int i=0;i<poseBones.Length;i++) {restPositions[i]=poseBones[i].localPosition;restRotations[i]=poseBones[i].localRotation;}
        }
        public bool IsUpperBody(Transform bone)
        {
            var spine = Bone(HumanBodyBones.Spine);
            return spine && (bone == spine || bone.IsChildOf(spine));
        }
        [Serializable] public struct FingerPose
        {
            public float first, second, third, spread;
            public FingerPose(float first, float second, float third, float spread=0f)
            {this.first=first;this.second=second;this.third=third;this.spread=spread;}
        }
        private static readonly string[] FingerNames={"Thumb","Index","Middle","Ring","Little"};
        private static readonly string[] JointNames={"Proximal","Intermediate","Distal"};
        public Transform FingerBone(bool left,int finger,int joint)
        {
            return Bone((HumanBodyBones)Enum.Parse(typeof(HumanBodyBones),(left ? "Left" : "Right")+FingerNames[finger]+JointNames[joint]));
        }
        private static int MuscleIndex(bool left,int finger,string property)
        {
            string name=(left ? "Left " : "Right ")+FingerNames[finger]+" "+property;
            int index=Array.IndexOf(HumanTrait.MuscleName,name);
            if(index<0)throw new InvalidOperationException("Нет параметра пальца: "+name);
            return index;
        }
        public FingerPose ReadFinger(bool left,int finger)
        {
            var pose=new HumanPose();
            using(var handler=new HumanPoseHandler(character.avatar,character.transform))handler.GetHumanPose(ref pose);
            return new FingerPose((1f-pose.muscles[MuscleIndex(left,finger,"1 Stretched")])*0.5f,
                (1f-pose.muscles[MuscleIndex(left,finger,"2 Stretched")])*0.5f,
                (1f-pose.muscles[MuscleIndex(left,finger,"3 Stretched")])*0.5f,
                pose.muscles[MuscleIndex(left,finger,"Spread")]);
        }
        public void SetFinger(bool left,int finger,FingerPose value)
        {
            var positions=new Vector3[poseBones.Length];var rotations=new Quaternion[poseBones.Length];
            for(int i=0;i<poseBones.Length;i++){positions[i]=poseBones[i].localPosition;rotations[i]=poseBones[i].localRotation;}
            var joints=new[]{FingerBone(left,finger,0),FingerBone(left,finger,1),FingerBone(left,finger,2)};
            var calculated=new Quaternion[3];
            try
            {
                var pose=new HumanPose();
                using(var handler=new HumanPoseHandler(character.avatar,character.transform))
                {
                    handler.GetHumanPose(ref pose);
                    pose.muscles[MuscleIndex(left,finger,"1 Stretched")]=1f-2f*value.first;
                    pose.muscles[MuscleIndex(left,finger,"2 Stretched")]=1f-2f*value.second;
                    pose.muscles[MuscleIndex(left,finger,"3 Stretched")]=1f-2f*value.third;
                    pose.muscles[MuscleIndex(left,finger,"Spread")]=value.spread;
                    handler.SetHumanPose(ref pose);
                    for(int i=0;i<joints.Length;i++)calculated[i]=joints[i].localRotation;
                }
            }
            finally
            {
                // Humanoid solving can touch the full skeleton. Retain only the chosen finger.
                for(int i=0;i<poseBones.Length;i++){poseBones[i].localPosition=positions[i];poseBones[i].localRotation=rotations[i];}
            }
            for(int i=0;i<joints.Length;i++)
            {
                joints[i].localRotation=calculated[i];
                int index=Array.IndexOf(poseBones,joints[i]);if(index>=0)restRotations[index]=calculated[i];
            }
            if(previewEnabled)ApplyPose();
            RememberCurrentInput();
        }
        public void RebaseControls()
        {
            leftHandTarget.SetPositionAndRotation(Bone(HumanBodyBones.LeftHand).position, Bone(HumanBodyBones.LeftHand).rotation);
            rightHandTarget.SetPositionAndRotation(Bone(HumanBodyBones.RightHand).position, Bone(HumanBodyBones.RightHand).rotation);
            leftElbowHint.position = Bone(HumanBodyBones.LeftLowerArm).position;
            rightElbowHint.position = Bone(HumanBodyBones.RightLowerArm).position;
            drawAmount = 1f;
        }
        public void CaptureUpperBodyEdits()
        {
            // Hips and legs retain the original basis; only torso, head, arms and fingers are captured.
            var chest = Bone(HumanBodyBones.Chest);
            Quaternion undoAim = Quaternion.identity;
            if (followAim && chest)
                undoAim = Quaternion.Inverse(transform.rotation * Quaternion.Slerp(Quaternion.identity, aimFrame.localRotation, bodyAimWeight)
                    * Quaternion.Inverse(transform.rotation));
            for (int i = 0; i < poseBones.Length; i++) if (poseBones[i] && IsUpperBody(poseBones[i]))
            {
                restRotations[i] = poseBones[i] == chest
                    ? Quaternion.Inverse(chest.parent.rotation) * undoAim * chest.rotation : poseBones[i].localRotation;
            }
            RebaseControls();
            basisName = "Своя поза верха тела";
            if (previewEnabled) ApplyPose();
            RememberCurrentInput();
        }
        public void ResetControls()
        {
            if (defaultPositions == null || defaultPositions.Length != Controls.Length) return;
            for (int i = 0; i < Controls.Length; i++)
            { Controls[i].localPosition = defaultPositions[i]; Controls[i].localRotation = defaultRotations[i]; }
            drawAmount = 1f; ApplyPose();
        }

        [Serializable] public sealed class PoseSettings
        {
            public int version = 2;
            public string baseClip;
            public float sampleTime, drawAmount, bodyAimWeight, pitchLimit, yawLimit;
            public Vector3 shoulderPivot;
            public Vector3[] controlPositions;
            public Quaternion[] controlRotations;
            public string[] bonePaths;
            public Vector3[] bonePositions;
            public Quaternion[] boneRotations;
        }
        public string ExportJson()
        {
            var data = new PoseSettings { baseClip=basisName, sampleTime=sampleTime, drawAmount=drawAmount,
                bodyAimWeight=bodyAimWeight, pitchLimit=pitchLimit, yawLimit=yawLimit, shoulderPivot=shoulderPivot,
                controlPositions=new Vector3[Controls.Length], controlRotations=new Quaternion[Controls.Length],
                bonePaths=new string[poseBones.Length], bonePositions=(Vector3[])restPositions.Clone(), boneRotations=(Quaternion[])restRotations.Clone() };
            for (int i=0;i<Controls.Length;i++) {data.controlPositions[i]=Controls[i].localPosition;data.controlRotations[i]=Controls[i].localRotation;}
            for (int i=0;i<poseBones.Length;i++) data.bonePaths[i]=BonePath(poseBones[i]);
            return JsonUtility.ToJson(data, true);
        }
        private string BonePath(Transform bone)
        {
            string path="";
            for(var current=bone;current && current!=character.transform;current=current.parent)
                path=current.name+(path.Length>0 ? "/"+path : "");
            return path;
        }
        public void LoadPoseJson(string json)
        {
            var data=JsonUtility.FromJson<PoseSettings>(json);
            if(data==null || (data.version!=1 && data.version!=2))throw new ArgumentException("Неизвестный формат позы.");
            if(data.bonePaths!=null && (data.bonePositions==null || data.boneRotations==null
                || data.bonePositions.Length!=data.bonePaths.Length || data.boneRotations.Length!=data.bonePaths.Length))
                throw new ArgumentException("Повреждённый набор костей.");
            if(data.bonePaths!=null)for(int i=0;i<data.bonePaths.Length;i++)
            {
                var bone=data.bonePaths[i].Length==0 ? character.transform : character.transform.Find(data.bonePaths[i]);
                if(!bone)throw new ArgumentException("Нет кости: "+data.bonePaths[i]);
                int index=Array.IndexOf(poseBones,bone);
                if(index>=0){restPositions[index]=data.bonePositions[i];restRotations[index]=data.boneRotations[i];}
            }
            for(int i=0;i<poseBones.Length;i++){poseBones[i].localPosition=restPositions[i];poseBones[i].localRotation=restRotations[i];}
            basePose=null;basisName=data.baseClip ?? "Загруженная поза";
            shoulderPivot=data.shoulderPivot;drawAmount=data.drawAmount;sampleTime=data.sampleTime;
            bodyAimWeight=data.bodyAimWeight;pitchLimit=data.pitchLimit;yawLimit=data.yawLimit;
            if(data.controlPositions!=null && data.controlRotations!=null && data.controlPositions.Length==Controls.Length && data.controlRotations.Length==Controls.Length)
                for(int i=0;i<Controls.Length;i++){Controls[i].localPosition=data.controlPositions[i];Controls[i].localRotation=data.controlRotations[i];}
            else
            {
                aimFrame.SetPositionAndRotation(transform.TransformPoint(shoulderPivot),transform.rotation);
                aimTarget.position=aimFrame.position+transform.forward*2.5f;
                RebaseControls();
            }
            CaptureDefaults();
            previewEnabled=true;ApplyPose();RememberCurrentInput();
        }

        private void OnDrawGizmos()
        {
            if (!character || !character.avatar || !character.avatar.isHuman) return;
            if (showSkeleton)
            {
                Gizmos.color = new Color(0.65f, 0.88f, 1f, 0.9f);
                foreach (var bone in character.GetComponentsInChildren<Transform>())
                    if (bone.parent && bone.name != "body" && bone.parent != character.transform)
                        Gizmos.DrawLine(bone.position, bone.parent.position);
            }
            if (!showGuides) return;
            var colors = new[] {Color.cyan, new Color(1f,0.65f,0.1f), Color.green, Color.green, new Color(1f,0.3f,0.3f)};
            var labels = new[] {"Левая кисть", "Правая кисть", "Левый локоть", "Правый локоть", "Прицел"};
            for (int i=0;i<Controls.Length;i++) if (Controls[i])
            {
                Gizmos.color=colors[i]; Gizmos.DrawWireSphere(Controls[i].position, i==4 ? 0.12f : 0.045f);
#if UNITY_EDITOR
                Handles.Label(Controls[i].position + Vector3.up * 0.06f, labels[i]);
#endif
            }
        }
    }
}
