
namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class ExpressionDataComponent : FaceTuneTagComponent,
        IExpressionDefinitionProvider,
        ISettingProvider<ExpressionBehavior>,
        ISettingProvider<MultiFrameSettings>,
        ISettingProvider<FacialBlendShapeData>,
        ISettingProvider<NonFacialAnimationData>,
        ISettingProviderWithReference<EyeBlinkSettings>,
        ISettingProviderWithReference<LipSyncSettings>
    {
        internal const string ComponentName = ComponentNamePrefix + "Data";

        public bool HasFacialBlendShapes;
        public FacialBlendShapeData FacialBlendShapes = new();

        public bool HasFacialBehavior;
        public ExpressionWriteMode WriteMode;
        public TrackingPermission AllowEyeBlink;
        public TrackingPermission AllowLipSync;

        public bool HasMultiFrame;
        public MultiFrameSettings MultiFrame = new();

        public bool HasEyeBlink;
        public SettingsReference EyeBlinkReference = new();
        public EyeBlinkSettings EyeBlink = new();

        public bool HasLipSync;
        public SettingsReference LipSyncReference = new();
        public LipSyncSettings LipSync = new();

        public bool HasNonFacialAnimations;
        public NonFacialAnimationData NonFacialAnimations = new();

#region Defaults

        private void Reset()
        {
            HasFacialBlendShapes = DefaultHasFacialBlendShapes;
            HasFacialBehavior = DefaultHasFacialBehavior;
            HasMultiFrame = DefaultHasMultiFrame;
        }

        internal const bool DefaultHasFacialBlendShapes = true;
        internal const bool DefaultHasFacialBehavior = true;
        internal const bool DefaultHasMultiFrame = true;

#endregion

#region Interfaces

        (bool Enabled, FacialBlendShapeData Value) ISettingProvider<FacialBlendShapeData>.Setting => (HasFacialBlendShapes, FacialBlendShapes);
        (bool Enabled, NonFacialAnimationData Value) ISettingProvider<NonFacialAnimationData>.Setting => (HasNonFacialAnimations, NonFacialAnimations);
        (bool Enabled, EyeBlinkSettings Value) ISettingProvider<EyeBlinkSettings>.Setting => (HasEyeBlink, EyeBlink);
        (SettingsReferenceMode Mode, FaceTuneTagComponent? Source) ISettingProviderWithReference<EyeBlinkSettings>.Reference => (EyeBlinkReference.Mode, EyeBlinkReference.ComponentSource);
        (bool Enabled, LipSyncSettings Value) ISettingProvider<LipSyncSettings>.Setting => (HasLipSync, LipSync);
        (SettingsReferenceMode Mode, FaceTuneTagComponent? Source) ISettingProviderWithReference<LipSyncSettings>.Reference => (LipSyncReference.Mode, LipSyncReference.ComponentSource);
        (bool Enabled, ExpressionBehavior Value) ISettingProvider<ExpressionBehavior>.Setting => (HasFacialBehavior, new(WriteMode, AllowEyeBlink, AllowLipSync));
        (bool Enabled, MultiFrameSettings Value) ISettingProvider<MultiFrameSettings>.Setting => (HasMultiFrame, MultiFrame);

#endregion

    }
}