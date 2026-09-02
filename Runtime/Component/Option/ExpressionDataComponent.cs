
namespace Aoyon.FaceTune
{
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class ExpressionDataComponent : FaceTuneTagComponent,
        IReferenceableExpressionSettings<FacialBlendShapeData>,
        IReferenceableExpressionSettings<NonFacialAnimationData>,
        IReferenceableExpressionSettings<EyeBlinkSettings>,
        IReferenceableExpressionSettings<LipSyncSettings>,
        IReferenceableExpression
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

        ReferenceableExpressionSettings<FacialBlendShapeData> IReferenceableExpressionSettings<FacialBlendShapeData>.Settings
            => new(HasFacialBlendShapes, SettingsReferenceMode.Direct, null, FacialBlendShapes);

        ReferenceableExpressionSettings<NonFacialAnimationData> IReferenceableExpressionSettings<NonFacialAnimationData>.Settings
            => new(HasNonFacialAnimations, SettingsReferenceMode.Direct, null, NonFacialAnimations);

        ReferenceableExpressionSettings<EyeBlinkSettings> IReferenceableExpressionSettings<EyeBlinkSettings>.Settings
            => new(HasEyeBlink, EyeBlinkReference.Mode, EyeBlinkReference.Source, EyeBlink);

        ReferenceableExpressionSettings<LipSyncSettings> IReferenceableExpressionSettings<LipSyncSettings>.Settings
            => new(HasLipSync, LipSyncReference.Mode, LipSyncReference.Source, LipSync);
        
        ExpressionWriteMode IReferenceableExpression.WriteMode => WriteMode;
        TrackingPermission IReferenceableExpression.AllowEyeBlink => AllowEyeBlink;
        TrackingPermission IReferenceableExpression.AllowLipSync => AllowLipSync;
        MultiFrameSettings IReferenceableExpression.MultiFrame => MultiFrame;

    }
}