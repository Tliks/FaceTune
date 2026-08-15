namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
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
        public FacialBlendShapeDataSource FacialBlendShapes = new();

        // 通常条件を迂回し、メニューを条件とする高優先度proxyを生成する。
        public bool DirectMenuEnabled = false;
        public DirectMenuSettings DirectMenuSettings = new();

        // falseなら通常条件では発動しない。Alwaysでも親scopeの条件は受ける。
        public bool HasCondition = false;
        public ConditionSelection Condition = new();

        // trueなら、このExpressionの値を親のSettingsより優先する。
        public bool HasEyeBlink = false;
        public EyeBlinkSettingsSource EyeBlink = new();

        public bool HasLipSync = false;
        public LipSyncSettingsSource LipSync = new();

        public bool HasTransition = false;
        public TransitionSettings Transition = new();

        public bool HasPriority = false;
        public PrioritySettings Priority = new();

        [ToggleLeft]
        public bool AlwaysOnPreviewEnabled = false;


        IEnumerable<Condition> IHasConditions.Conditions
            => HasCondition && Condition.Mode == ConditionSelection.Kind.Conditional
                ? new[] { Condition.Condition }
                : Array.Empty<Condition>();

        ISettingsSource<FacialBlendShapeData>? IReferenceableExpressionSettings<FacialBlendShapeData>.SettingsSource
            => FacialBlendShapes;

        ISettingsSource<EyeBlinkSettings>? IReferenceableExpressionSettings<EyeBlinkSettings>.SettingsSource
            => HasEyeBlink ? EyeBlink : null;

        ISettingsSource<LipSyncSettings>? IReferenceableExpressionSettings<LipSyncSettings>.SettingsSource
            => HasLipSync ? LipSync : null;

    }
}