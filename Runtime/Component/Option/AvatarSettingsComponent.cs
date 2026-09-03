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

        // facetune外部(組み込みFX Controllerや外部MergeAnimator等)のまばたき/リップシンク制御へ介入するか。
        // VRCにおける現状実装は、外部ControllerのTracking Control Behaviorを書き換えAAP制御へ移行させ、FaceTune側のレイヤーで中央制御する。
        [ToggleLeft]
        public bool AvoidEyeBlinkConflicts = DefaultAvoidEyeBlinkConflicts;
        [ToggleLeft]
        public bool AvoidLipSyncConflicts = DefaultAvoidLipSyncConflicts;

#region Defaults

        internal const bool DefaultAvoidEyeBlinkConflicts = true;
        internal const bool DefaultAvoidLipSyncConflicts = true;

#endregion

#region Interfaces

        void IHasObjectReferences.ResolveReferences() => FaceObjectReference.Get(this);

#endregion
    }
}