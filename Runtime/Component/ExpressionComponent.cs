namespace Aoyon.FaceTune
{
    [AddComponentMenu(MenuPathPrefix + ComponentName)]
    internal class ExpressionComponent : FaceTuneTagComponent,
        IHasConditions,
        IReferenceableExpressionSettings<FacialBlendShapeData>,
        IReferenceableExpressionSettings<NonFacialAnimationData>,
        IReferenceableExpressionSettings<EyeBlinkSettings>,
        IReferenceableExpressionSettings<LipSyncSettings>,
        IReferenceableExpression
    {
        internal const string ComponentName = FaceTuneConstants.Name;

#region Condition

        // falseなら通常条件では発動しない。Alwaysでも親scopeの条件は受ける。
        public bool HasCondition = false;
        public ConditionSelection Condition = CreateDefaultCondition();

        // 通常条件を迂回し、メニューを条件とする高優先度proxyを生成する。
        public bool DirectMenuEnabled = false;
        public DirectMenuSettings DirectMenuSettings = CreateDefaultDirectMenuSettings();

#endregion

#region ExpressionData

        public SettingsReference ExpressionDataReference = new();

        // 以下はExpressionDataReferenceがDirectのとき

        public FacialBlendShapeData FacialBlendShapes = new();

        // 下位の表情に対し、Replaceは上書し、Blendは同時に重ねる。
        public ExpressionWriteMode WriteMode = DefaultWriteMode;
        public MultiFrameSettings MultiFrame = new();
        // この表情再生中におけるまばたき/リップシンクの扱い。
        public TrackingPermission AllowEyeBlink = DefaultAllowEyeBlink;
        public TrackingPermission AllowLipSync = DefaultAllowLipSync;

        // trueなら、このExpressionの値を親のSettingsより優先する。
        public bool HasEyeBlink = false;
        public SettingsReference EyeBlinkReference = new();
        public EyeBlinkSettings EyeBlink = new();

        public bool HasLipSync = false;
        public SettingsReference LipSyncReference = new();
        public LipSyncSettings LipSync = new();

        public NonFacialAnimationData NonFacialAnimations = new();

#endregion

# region Other Expression Settings

        public bool HasTransition = false;
        public TransitionSettings Transition = new();

        public bool HasPriority = false;
        public PrioritySettings Priority = new();

        [ToggleLeft]
        public bool AlwaysOnPreviewEnabled = DefaultAlwaysOnPreviewEnabled;

#endregion

#region Defaults

        internal const TrackingPermission DefaultAllowEyeBlink = TrackingPermission.Disallow;
        internal const TrackingPermission DefaultAllowLipSync = TrackingPermission.Allow;
        internal const ExpressionWriteMode DefaultWriteMode = ExpressionWriteMode.Replace;
        internal const bool DefaultAlwaysOnPreviewEnabled = false;

        internal static DirectMenuSettings CreateDefaultDirectMenuSettings()
        {
            var settings = new DirectMenuSettings();
            settings.Menu.Icon.Mode = MenuIconSettings.Kind.ExpressionPreview;
            return settings;
        }

        internal static ConditionSelection CreateDefaultCondition()
            => new()
            {
                Condition = new Condition(
                    ConditionCase.From(new HandGestureCondition()))
            };

#endregion

# region Interfaces

        IEnumerable<Condition> IHasConditions.Conditions
            => HasCondition && Condition.Mode == ConditionSelection.Kind.Conditional
                ? new[] { Condition.Condition }
                : Array.Empty<Condition>();

        ReferenceableExpressionSettings<FacialBlendShapeData> IReferenceableExpressionSettings<FacialBlendShapeData>.Settings
            => new(true, SettingsReferenceMode.Direct, null, FacialBlendShapes);

        ReferenceableExpressionSettings<NonFacialAnimationData> IReferenceableExpressionSettings<NonFacialAnimationData>.Settings
            => new(true, SettingsReferenceMode.Direct, null, NonFacialAnimations);

        ReferenceableExpressionSettings<EyeBlinkSettings> IReferenceableExpressionSettings<EyeBlinkSettings>.Settings
            => new(HasEyeBlink, EyeBlinkReference.Mode, EyeBlinkReference.Source, EyeBlink);

        ReferenceableExpressionSettings<LipSyncSettings> IReferenceableExpressionSettings<LipSyncSettings>.Settings
            => new(HasLipSync, LipSyncReference.Mode, LipSyncReference.Source, LipSync);

        ExpressionWriteMode IReferenceableExpression.WriteMode => WriteMode;
        TrackingPermission IReferenceableExpression.AllowEyeBlink => AllowEyeBlink;
        TrackingPermission IReferenceableExpression.AllowLipSync => AllowLipSync;
        MultiFrameSettings IReferenceableExpression.MultiFrame => MultiFrame;

#endregion

    }
}