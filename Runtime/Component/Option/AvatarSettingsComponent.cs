namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class AvatarSettingsComponent : FaceTuneTagComponent, IHasObjectReferences
    {
        internal const string ComponentName = ComponentNamePrefix + "Avatar Settings";

        // 空なら自動推定
        public AvatarObjectReference FaceObjectReference = new();

        // 読み書きしないBlendShape。空なら全て読み書き。
        public List<string> ExcludedBlendShapeNames = new();

        // facetune外部（VRChat標準Tracking等）のまばたき/リップシンク制御へ介入するか。
        // internalなAAP中央制御とは独立し、ここは外部Trackingの統制（VRCAnimatorTrackingControl）有無のみを決める。
        [ToggleLeft]
        public bool AvoidEyeBlinkConflicts = true;
        [ToggleLeft]
        public bool AvoidLipSyncConflicts = true;

        void IHasObjectReferences.ResolveReferences() => FaceObjectReference.Get(this);
    }
}