
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
        internal const string ComponentName = ComponentNamePrefix + "Expression Data";

        public bool HasFacialBlendShapes = true;
        public FacialBlendShapeData FacialBlendShapes = new();

        public bool HasFacialBehavior = true;
        public ExpressionWriteMode WriteMode = ExpressionWriteMode.Replace;
        public TrackingPermission AllowEyeBlink = TrackingPermission.Allow;
        public TrackingPermission AllowLipSync = TrackingPermission.Allow;

        public bool HasMultiFrame = true;
        public MultiFrameSettings MultiFrame = new();

        public bool HasEyeBlink = false;
        public SettingsReference EyeBlinkReference = new();
        public EyeBlinkSettings EyeBlink = new();

        public bool HasLipSync = false;
        public SettingsReference LipSyncReference = new();
        public LipSyncSettings LipSync = new();

        public bool HasNonFacialAnimations = false;
        public NonFacialAnimationData NonFacialAnimations = new();

        (bool Enabled, FacialBlendShapeData Value) ISettingProvider<FacialBlendShapeData>.Setting => (HasFacialBlendShapes, FacialBlendShapes);
        (bool Enabled, NonFacialAnimationData Value) ISettingProvider<NonFacialAnimationData>.Setting => (HasNonFacialAnimations, NonFacialAnimations);
        (bool Enabled, EyeBlinkSettings Value) ISettingProvider<EyeBlinkSettings>.Setting => (HasEyeBlink, EyeBlink);
        (SettingsReferenceMode Mode, Transform? Source) ISettingProviderWithReference<EyeBlinkSettings>.Reference => (EyeBlinkReference.Mode, EyeBlinkReference.Source);
        (bool Enabled, LipSyncSettings Value) ISettingProvider<LipSyncSettings>.Setting => (HasLipSync, LipSync);
        (SettingsReferenceMode Mode, Transform? Source) ISettingProviderWithReference<LipSyncSettings>.Reference => (LipSyncReference.Mode, LipSyncReference.Source);
        (bool Enabled, ExpressionBehavior Value) ISettingProvider<ExpressionBehavior>.Setting => (HasFacialBehavior, new(WriteMode, AllowEyeBlink, AllowLipSync));
        (bool Enabled, MultiFrameSettings Value) ISettingProvider<MultiFrameSettings>.Setting => (HasMultiFrame, MultiFrame);

    }
}