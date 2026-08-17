namespace Aoyon.FaceTune
{
    [AddComponentMenu(MenuPathPrefix + ComponentName)]
    internal class ExpressionComponent : FaceTuneTagComponent,
        IHasConditions,
        IReferenceableExpressionSettings<FacialBlendShapeData>,
        IReferenceableExpressionSettings<EyeBlinkSettings>,
        IReferenceableExpressionSettings<LipSyncSettings>
    {
        internal const string ComponentName = FaceTuneConstants.Name;

        // この表情再生中におけるまばたき/リップシンクの扱い。
        public TrackingPermission AllowEyeBlink = TrackingPermission.Disallow;
        public TrackingPermission AllowLipSync = TrackingPermission.Allow;

        // 下位の表情に対し、Replaceは上書し、Blendは同時に重ねる。
        public ExpressionWriteMode WriteMode = ExpressionWriteMode.Replace;

        public MultiFrameSettings MultiFrame = new();

        // 親のSettingsから集めた顔つきの後に重ねる。
        public SettingsReference FacialBlendShapesReference = new();
        public FacialBlendShapeData FacialBlendShapes = new();

        // 通常条件を迂回し、メニューを条件とする高優先度proxyを生成する。
        public bool DirectMenuEnabled = false;
        public DirectMenuSettings DirectMenuSettings = CreateDefaultDirectMenuSettings();

        // falseなら通常条件では発動しない。Alwaysでも親scopeの条件は受ける。
        public bool HasCondition = false;
        public ConditionSelection Condition = CreateDefaultCondition();

        // trueなら、このExpressionの値を親のSettingsより優先する。
        public bool HasEyeBlink = false;
        public SettingsReference EyeBlinkReference = new();
        public EyeBlinkSettings EyeBlink = new();

        public bool HasLipSync = false;
        public SettingsReference LipSyncReference = new();
        public LipSyncSettings LipSync = new();

        public bool HasTransition = false;
        public TransitionSettings Transition = new();

        public bool HasPriority = false;
        public PrioritySettings Priority = new();

        [ToggleLeft]
        public bool AlwaysOnPreviewEnabled = false;

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


        IEnumerable<Condition> IHasConditions.Conditions
            => HasCondition && Condition.Mode == ConditionSelection.Kind.Conditional
                ? new[] { Condition.Condition }
                : Array.Empty<Condition>();

        ReferenceableExpressionSettings<FacialBlendShapeData> IReferenceableExpressionSettings<FacialBlendShapeData>.Settings
            => new(true, FacialBlendShapesReference.Mode, FacialBlendShapesReference.Source, FacialBlendShapes);

        ReferenceableExpressionSettings<EyeBlinkSettings> IReferenceableExpressionSettings<EyeBlinkSettings>.Settings
            => new(HasEyeBlink, EyeBlinkReference.Mode, EyeBlinkReference.Source, EyeBlink);

        ReferenceableExpressionSettings<LipSyncSettings> IReferenceableExpressionSettings<LipSyncSettings>.Settings
            => new(HasLipSync, LipSyncReference.Mode, LipSyncReference.Source, LipSync);

    }
}