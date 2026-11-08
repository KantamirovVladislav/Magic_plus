using System;
using System.Collections.Generic;
using UnityEngine;

namespace MagicPlus.AnimationLab
{
    // Editor preview, deliberately separate from in-game casting and network/resource logic.
    public sealed class MagicPoseSequence
    {
        public enum Stage { Idle, Enter, Draw, Hold, Release, Recover, Complete }
        [Serializable] public sealed class Settings
        {
            public int version=1;
            public float enterSeconds=.2f,drawSeconds=.6f,holdSeconds=1f,releaseSeconds=.14f,recoverSeconds=.3f;
            public float releaseMarker=.35f;
            private static float Safe(float value,float fallback,float min,float max)
                => float.IsNaN(value)||float.IsInfinity(value)?fallback:Mathf.Clamp(value,min,max);
            public void Validate()
            {
                enterSeconds=Safe(enterSeconds,.2f,.05f,3f);drawSeconds=Safe(drawSeconds,.6f,.1f,5f);
                holdSeconds=Safe(holdSeconds,1f,.1f,20f);releaseSeconds=Safe(releaseSeconds,.14f,.05f,2f);
                recoverSeconds=Safe(recoverSeconds,.3f,.05f,3f);releaseMarker=Safe(releaseMarker,.35f,.05f,.95f);
            }
        }
        private sealed class Snapshot
        {
            public string json;
            public Vector3[] positions,defaults;
            public Quaternion[] rotations,defaultRotations;
            public bool preview,follow,guides,skeleton;
            public AnimationClip clip;
            public Vector3 framePosition;public Quaternion frameRotation;
        }
        public readonly Settings Timing;
        private readonly MagicPoseLab _lab;
        private readonly MagicPoseLab.PoseSettings _start,_loaded,_release,_ready;
        private readonly int[] _indices;
        private readonly Vector3[] _basePositions;
        private readonly Quaternion[] _baseRotations;
        private readonly Dictionary<MagicPoseLab.PoseSettings,Quaternion[]> _evaluated=new Dictionary<MagicPoseLab.PoseSettings,Quaternion[]>();
        private Snapshot _snapshot;
        private bool _automatic,_releaseQueued,_fired;
        private float _elapsed;
        public Stage CurrentStage {get;private set;}=Stage.Idle;
        public int ReleaseCount {get;private set;}
        public bool ReleaseQueued=>_releaseQueued;
        public bool OwnsPose=>_snapshot!=null;
        public bool IsRunning=>CurrentStage>=Stage.Enter && CurrentStage<=Stage.Recover;
        public bool HasPassedMarker=>CurrentStage==Stage.Recover || CurrentStage==Stage.Complete || (CurrentStage==Stage.Release && _elapsed>=Timing.releaseSeconds*Timing.releaseMarker);
        public event Action ReleaseMoment;
        public float Duration=>Timing.enterSeconds+Timing.drawSeconds+Timing.holdSeconds+Timing.releaseSeconds+Timing.recoverSeconds;
        public float MarkerTime=>Timing.enterSeconds+Timing.drawSeconds+Timing.holdSeconds+Timing.releaseSeconds*Timing.releaseMarker;
        public float PhaseProgress=>Mathf.Clamp01(_elapsed/Length(CurrentStage));
        public float TimelineTime
        {
            get
            {
                float e=Timing.enterSeconds,d=Timing.drawSeconds,h=Timing.holdSeconds,r=Timing.releaseSeconds;
                switch(CurrentStage){case Stage.Enter:return _elapsed;case Stage.Draw:return e+_elapsed;
                    case Stage.Hold:return e+d+Mathf.Min(_elapsed,h);case Stage.Release:return e+d+h+_elapsed;
                    case Stage.Recover:return e+d+h+r+_elapsed;case Stage.Complete:return Duration;default:return 0;}
            }
        }
        public MagicPoseSequence(MagicPoseLab lab,string start,string loaded,string release,string ready,Settings timing)
        {
            _lab=lab?lab:throw new ArgumentNullException(nameof(lab));Timing=timing??new Settings();Timing.Validate();
            _start=Parse(start);_loaded=Parse(loaded);_release=Parse(release);_ready=string.IsNullOrEmpty(ready)?_start:Parse(ready);
            _basePositions=(Vector3[])lab.restPositions.Clone();_baseRotations=(Quaternion[])lab.restRotations.Clone();
            _indices=new int[_start.bonePaths.Length];
            for(int i=0;i<_indices.Length;i++)
            {
                string path=_start.bonePaths[i];var bone=path.Length==0?lab.character.transform:lab.character.transform.Find(path);
                int index=Array.IndexOf(lab.poseBones,bone);if(index<0)throw new ArgumentException("Нет кости: "+path);_indices[i]=index;
                foreach(var data in new[]{_loaded,_release,_ready})
                    if(data.bonePaths.Length!=_indices.Length || data.bonePaths[i]!=path || Vector3.Distance(data.bonePositions[i],_start.bonePositions[i])>.000001f)
                        throw new ArgumentException("Позы используют разные пути или длины костей: "+path);
            }
            Capture();
            try
            {
                foreach(var data in new[]{_ready,_start,_loaded,_release})
                {
                    _lab.followAim=true;_lab.LoadPoseJson(JsonUtility.ToJson(data));var rotations=new Quaternion[_lab.poseBones.Length];
                    for(int i=0;i<rotations.Length;i++)rotations[i]=_lab.poseBones[i].localRotation;
                    _evaluated[data]=rotations;
                }
            }
            finally{Restore();}
        }
        private static MagicPoseLab.PoseSettings Parse(string json)
        {
            var data=JsonUtility.FromJson<MagicPoseLab.PoseSettings>(json);
            if(data==null || data.version!=2 || data.bonePaths==null || data.boneRotations==null || data.bonePositions==null
                || data.bonePaths.Length!=data.boneRotations.Length || data.bonePaths.Length!=data.bonePositions.Length
                || data.controlPositions==null || data.controlPositions.Length!=5 || data.controlRotations==null || data.controlRotations.Length!=5)
                throw new ArgumentException("Для анимации нужна полная поза JSON v2.");
            return data;
        }
        private void Capture()
        {
            if(_snapshot!=null)return;
            _snapshot=new Snapshot{json=_lab.ExportJson(),positions=new Vector3[_lab.poseBones.Length],rotations=new Quaternion[_lab.poseBones.Length],
                defaults=_lab.defaultPositions==null?null:(Vector3[])_lab.defaultPositions.Clone(),defaultRotations=_lab.defaultRotations==null?null:(Quaternion[])_lab.defaultRotations.Clone(),
                preview=_lab.previewEnabled,follow=_lab.followAim,guides=_lab.showGuides,skeleton=_lab.showSkeleton,clip=_lab.basePose,
                framePosition=_lab.aimFrame.localPosition,frameRotation=_lab.aimFrame.localRotation};
            for(int i=0;i<_lab.poseBones.Length;i++){_snapshot.positions[i]=_lab.poseBones[i].localPosition;_snapshot.rotations[i]=_lab.poseBones[i].localRotation;}
        }
        public void ShowSavedPose()
        {
            if(_snapshot==null || !_lab)return;
            _lab.LoadPoseJson(_snapshot.json);
            _lab.previewEnabled=_snapshot.preview;_lab.followAim=_snapshot.follow;_lab.showGuides=_snapshot.guides;_lab.showSkeleton=_snapshot.skeleton;_lab.basePose=_snapshot.clip;
            _lab.defaultPositions=_snapshot.defaults==null?null:(Vector3[])_snapshot.defaults.Clone();
            _lab.defaultRotations=_snapshot.defaultRotations==null?null:(Quaternion[])_snapshot.defaultRotations.Clone();
            _lab.aimFrame.localPosition=_snapshot.framePosition;_lab.aimFrame.localRotation=_snapshot.frameRotation;
            for(int i=0;i<_lab.poseBones.Length;i++){_lab.poseBones[i].localPosition=_snapshot.positions[i];_lab.poseBones[i].localRotation=_snapshot.rotations[i];}
            _lab.RememberCurrentInput();
        }
        public void Restore()
        {ShowSavedPose();_snapshot=null;CurrentStage=Stage.Idle;_elapsed=0;_releaseQueued=false;}
        public void Begin(bool automatic)
        {
            Restore();Capture();Timing.Validate();_automatic=automatic;_releaseQueued=false;_fired=false;ReleaseCount=0;
            SetStage(Stage.Enter);
        }
        public void RequestRelease()
        {
            if(CurrentStage<Stage.Enter || CurrentStage>Stage.Hold)return;
            _releaseQueued=true;if(CurrentStage==Stage.Hold)SetStage(Stage.Release);
        }
        private float Length(Stage stage)
        {
            switch(stage){case Stage.Enter:return Timing.enterSeconds;case Stage.Draw:return Timing.drawSeconds;
                case Stage.Hold:return Timing.holdSeconds;case Stage.Release:return Timing.releaseSeconds;case Stage.Recover:return Timing.recoverSeconds;default:return 1;}
        }
        private void SetStage(Stage stage){CurrentStage=stage;_elapsed=0;Reapply();}
        public void Advance(float seconds)
        {
            if(float.IsNaN(seconds)||float.IsInfinity(seconds)||seconds<=0 || !IsRunning)return;
            Timing.Validate();int guard=0;
            while(seconds>0 && IsRunning && guard++<16)
            {
                if(CurrentStage==Stage.Hold && _releaseQueued){SetStage(Stage.Release);continue;}
                if(CurrentStage==Stage.Hold && !_automatic){_elapsed+=seconds;Reapply();return;}
                float length=Length(CurrentStage),step=Mathf.Min(seconds,Mathf.Max(0,length-_elapsed));
                _elapsed+=step;seconds-=step;
                if(CurrentStage==Stage.Release && !_fired && _elapsed>=Timing.releaseSeconds*Timing.releaseMarker)
                {_fired=true;ReleaseCount++;ReleaseMoment?.Invoke();}
                Reapply();
                if(_elapsed<length-.000001f)return;
                switch(CurrentStage){case Stage.Enter:SetStage(Stage.Draw);break;case Stage.Draw:SetStage(Stage.Hold);break;
                    case Stage.Hold:SetStage(Stage.Release);break;case Stage.Release:SetStage(Stage.Recover);break;case Stage.Recover:SetStage(Stage.Complete);break;}
            }
        }
        public void SampleAt(float seconds)
        {
            Capture();Timing.Validate();seconds=Mathf.Clamp(seconds,0,Duration);_automatic=true;_releaseQueued=false;_fired=seconds>=MarkerTime;
            if(seconds>=Duration){CurrentStage=Stage.Complete;_elapsed=0;Reapply();return;}
            foreach(var stage in new[]{Stage.Enter,Stage.Draw,Stage.Hold,Stage.Release,Stage.Recover})
            {
                float length=Length(stage);if(seconds<length){CurrentStage=stage;_elapsed=seconds;Reapply();return;}seconds-=length;
            }
        }
        public void Reapply()
        {
            if(!_lab || CurrentStage==Stage.Idle)return;
            float t=PhaseProgress;float smooth=t*t*(3f-2f*t);
            switch(CurrentStage)
            {
                case Stage.Enter:Blend(_ready,_start,smooth);break;
                case Stage.Draw:Blend(_start,_loaded,smooth);break;
                case Stage.Hold:Blend(_loaded,_loaded,0);break;
                case Stage.Release:Blend(_loaded,_release,smooth);break;
                case Stage.Recover:Blend(_release,_ready,smooth);break;
                case Stage.Complete:Blend(_ready,_ready,0);break;
            }
        }
        private void Blend(MagicPoseLab.PoseSettings from,MagicPoseLab.PoseSettings to,float t)
        {
            _lab.basePose=null;_lab.previewEnabled=false;_lab.followAim=false;
            for(int i=0;i<_lab.poseBones.Length;i++){_lab.restPositions[i]=_basePositions[i];_lab.restRotations[i]=_baseRotations[i];}
            for(int i=0;i<_indices.Length;i++)
            {
                int index=_indices[i];if(_lab.IsUpperBody(_lab.poseBones[index]))_lab.restRotations[index]=Quaternion.Slerp(_evaluated[from][index],_evaluated[to][index],t);
            }
            _lab.shoulderPivot=Vector3.Lerp(from.shoulderPivot,to.shoulderPivot,t);_lab.drawAmount=1;
            _lab.bodyAimWeight=Mathf.Lerp(from.bodyAimWeight,to.bodyAimWeight,t);_lab.pitchLimit=Mathf.Lerp(from.pitchLimit,to.pitchLimit,t);_lab.yawLimit=Mathf.Lerp(from.yawLimit,to.yawLimit,t);
            var controls=_lab.Controls;
            controls[4].localPosition=Vector3.Lerp(from.controlPositions[4],to.controlPositions[4],t);
            _lab.aimFrame.SetPositionAndRotation(_lab.transform.TransformPoint(_lab.shoulderPivot),Quaternion.LookRotation(_lab.AimDirection,_lab.transform.up));
            for(int i=0;i<4;i++){controls[i].localPosition=Vector3.Lerp(from.controlPositions[i],to.controlPositions[i],t);controls[i].localRotation=Quaternion.Slerp(from.controlRotations[i],to.controlRotations[i],t);}
            for(int i=0;i<_lab.poseBones.Length;i++){_lab.poseBones[i].localPosition=_lab.restPositions[i];_lab.poseBones[i].localRotation=_lab.restRotations[i];}
            if(_lab.arrowGuide)_lab.arrowGuide.enabled=false;
            _lab.leftReachError=_lab.rightReachError=0;
            _lab.basisName="Анимация — "+CurrentStage;_lab.RememberCurrentInput();
        }
    }
}
