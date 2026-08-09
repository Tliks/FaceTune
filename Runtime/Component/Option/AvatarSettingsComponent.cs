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

        // FaceTune外部のまばたき/リップシンク制御との競合問題を良い感じにする契約。
        // VRCにおける現行実装はTracking ControlのAAPへの置き換えと中央制御。
        public bool AvoidEyeBlinkConflicts = true;
        public bool AvoidLipSyncConflicts = true;

        void IHasObjectReferences.ResolveReferences() => FaceObjectReference.Get(this);
    }
}