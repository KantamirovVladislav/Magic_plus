using System;
using UnityEngine;
using MagicPlus.FullSkeleton;

namespace MagicPlus.BreathAnimation
{
    public sealed class FireBreathSequence
    {
        public enum Phase { Ready, Enter, Hold, Exit, Complete }
        [Serializable]
        public sealed class Settings
        {
            public int version = 1;
            public float enterSeconds = .6f, automaticHoldSeconds = 2.4f, exitSeconds = .5f;
            public float holdCycleSeconds = 2.2f, breathPitchDegrees = 1f;
            private static float Safe(float v, float fallback, float min, float max)
                => float.IsNaN(v) || float.IsInfinity(v) ? fallback : Mathf.Clamp(v, min, max);
            public void Validate()
            {
                enterSeconds = Safe(enterSeconds, .6f, .1f, 3f);
                automaticHoldSeconds = Safe(automaticHoldSeconds, 2.4f, .2f, 20f);
                exitSeconds = Safe(exitSeconds, .5f, .1f, 3f);
                holdCycleSeconds = Safe(holdCycleSeconds, 2.2f, .6f, 6f);
                breathPitchDegrees = Safe(breathPitchDegrees, 1f, 0f, 3f);
            }
        }
        private readonly FullSkeletonPoseLab _lab;
        private readonly Transform[] _bones;
        private readonly bool[] _upper;
        private readonly Transform _spine;
        private readonly FullSkeletonPoseLab.PoseDocument _ready, _gather, _hold;
        private FullSkeletonPoseLab.PoseDocument _snapshot;
        private readonly Vector3[] _exitPositions, _exitScales;
        private readonly Quaternion[] _exitRotations;
        private float _time;
        private double _release = double.NaN;
        public readonly Settings Timing;
        public Phase CurrentPhase { get; private set; } = Phase.Ready;
        public bool OwnsPose => _snapshot != null;
        public bool Running => OwnsPose && CurrentPhase != Phase.Complete;
        public float Time => _time;
        public float Duration => Timing.enterSeconds + Timing.automaticHoldSeconds + Timing.exitSeconds;
        public int UpperBoneCount { get; }

        public FireBreathSequence(FullSkeletonPoseLab lab, string ready, string gather, string hold, Settings settings)
        {
            _lab = lab ? lab : throw new ArgumentNullException(nameof(lab));
            Timing = settings ?? new Settings(); Timing.Validate();
            lab.ValidateJson(ready); lab.ValidateJson(gather); lab.ValidateJson(hold);
            _ready = JsonUtility.FromJson<FullSkeletonPoseLab.PoseDocument>(ready);
            _gather = JsonUtility.FromJson<FullSkeletonPoseLab.PoseDocument>(gather);
            _hold = JsonUtility.FromJson<FullSkeletonPoseLab.PoseDocument>(hold);
            _bones = lab.Bones; _upper = new bool[_bones.Length];
            _spine = lab.character.GetBoneTransform(HumanBodyBones.Spine);
            if (!_spine) throw new ArgumentException("Нет Spine в скелете.");
            _ready = InRigOrder(_ready); _gather = InRigOrder(_gather); _hold = InRigOrder(_hold);
            int count = 0;
            for (int i = 0; i < _bones.Length; i++)
            { _upper[i] = _bones[i] == _spine || _bones[i].IsChildOf(_spine); if (_upper[i]) count++; }
            UpperBoneCount = count;
            _exitPositions = new Vector3[_bones.Length]; _exitRotations = new Quaternion[_bones.Length]; _exitScales = new Vector3[_bones.Length];
        }
        private FullSkeletonPoseLab.PoseDocument InRigOrder(FullSkeletonPoseLab.PoseDocument input)
        {
            var result = new FullSkeletonPoseLab.PoseDocument { name = input.name, bones = new FullSkeletonPoseLab.BonePose[_bones.Length] };
            for (int i = 0; i < _bones.Length; i++)
            {
                string path = _lab.BonePath(_bones[i]);
                result.bones[i] = Array.Find(input.bones, b => b.path == path);
            }
            return result;
        }
        private void Capture()
        {
            if (_snapshot != null) return;
            if (_lab.previewActive) throw new InvalidOperationException("Скелетом уже управляет другой предпросмотр.");
            _snapshot = _lab.Capture("Before breath animation");
            _lab.previewActive = true;
        }
        public void Begin(bool automatic)
        {
            Restore(); Capture(); Timing.Validate(); _time = 0f;
            _release = automatic ? Timing.enterSeconds + Timing.automaticHoldSeconds : double.NaN;
            Reapply();
        }
        public void RequestStop()
        {
            if (!OwnsPose || (CurrentPhase != Phase.Enter && CurrentPhase != Phase.Hold)) return;
            _release = _time; Reapply();
        }
        public void Advance(float seconds)
        {
            if (!Running || float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0f) return;
            _time += seconds; Reapply();
        }
        public void SampleAt(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds)) throw new ArgumentOutOfRangeException(nameof(seconds));
            Capture(); Timing.Validate();
            _release = Timing.enterSeconds + Timing.automaticHoldSeconds;
            _time = Mathf.Clamp(seconds, 0f, Duration); Reapply();
        }
        public void SampleReleased(float seconds, float releasedAt)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || float.IsNaN(releasedAt) || float.IsInfinity(releasedAt))
                throw new ArgumentOutOfRangeException(nameof(seconds));
            Capture(); Timing.Validate();
            _release = Math.Max(0, releasedAt); _time = Mathf.Max(0, seconds); Reapply();
        }
        public void Reapply()
        {
            if (!_lab || !OwnsPose) return;
            if (!double.IsNaN(_release) && _time >= _release)
            {
                float age = _time - (float)_release;
                if (age >= Timing.exitSeconds)
                { ApplyEndpoint(_ready); CurrentPhase = Phase.Complete; return; }
                ApplyChannel((float)_release);
                for (int i = 0; i < _bones.Length; i++) if (_upper[i])
                {
                    _exitPositions[i] = _bones[i].localPosition; _exitRotations[i] = _bones[i].localRotation; _exitScales[i] = _bones[i].localScale;
                }
                float t = Smooth(age / Timing.exitSeconds);
                for (int i = 0; i < _bones.Length; i++) if (_upper[i])
                {
                    _bones[i].localPosition = Vector3.Lerp(_exitPositions[i], _ready.bones[i].position, t);
                    _bones[i].localRotation = Quaternion.Slerp(_exitRotations[i], _ready.bones[i].rotation, t);
                    _bones[i].localScale = Vector3.Lerp(_exitScales[i], _ready.bones[i].scale, t);
                }
                CurrentPhase = Phase.Exit; return;
            }
            ApplyChannel(_time);
        }
        private void ApplyChannel(float seconds)
        {
            if (seconds < Timing.enterSeconds)
            {
                float t = Smooth(Mathf.Clamp01(seconds / Timing.enterSeconds));
                for (int i = 0; i < _bones.Length; i++) if (_upper[i])
                {
                    var a = _ready.bones[i]; var b = _gather.bones[i]; var c = _hold.bones[i];
                    _bones[i].localPosition = Vector3.Lerp(Vector3.Lerp(a.position, b.position, t), Vector3.Lerp(b.position, c.position, t), t);
                    _bones[i].localRotation = Quaternion.Slerp(Quaternion.Slerp(a.rotation, b.rotation, t), Quaternion.Slerp(b.rotation, c.rotation, t), t);
                    _bones[i].localScale = Vector3.Lerp(Vector3.Lerp(a.scale, b.scale, t), Vector3.Lerp(b.scale, c.scale, t), t);
                }
                CurrentPhase = Phase.Enter; return;
            }
            ApplyEndpoint(_hold);
            float cycle = (seconds - Timing.enterSeconds) / Timing.holdCycleSeconds;
            float pitch = Timing.breathPitchDegrees * .5f * (1f - Mathf.Cos(cycle * Mathf.PI * 2f));
            _spine.rotation = Quaternion.AngleAxis(pitch, _lab.character.transform.right) * _spine.rotation;
            CurrentPhase = Phase.Hold;
        }
        private void ApplyEndpoint(FullSkeletonPoseLab.PoseDocument pose)
        {
            for (int i = 0; i < _bones.Length; i++) if (_upper[i])
            {
                _bones[i].localPosition = pose.bones[i].position;
                _bones[i].localRotation = pose.bones[i].rotation;
                _bones[i].localScale = pose.bones[i].scale;
            }
        }
        private static float Smooth(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);
        public void ShowSavedPose()
        {
            if (!_lab || _snapshot == null) return;
            _lab.LoadJson(JsonUtility.ToJson(_snapshot));
        }
        public void Restore()
        {
            if (_snapshot == null) return;
            ShowSavedPose();
            if (_lab) _lab.previewActive = false;
            _snapshot = null; _time = 0; _release = double.NaN; CurrentPhase = Phase.Ready;
        }
    }
}
