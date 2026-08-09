namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(MenuPathPrefix + ComponentName)]
    internal class ExpressionComponent : FaceTuneTagComponent,
        IHasConditions,
        IReferenceableExpressionSettings<FacialBlendShapeDataSource>,
        IReferenceableExpressionSettings<EyeBlinkSettingsSource>,
        IReferenceableExpressionSettings<LipSyncSettingsSource>,
        IReferenceableExpressionSettings<ParameterDriverSettingsSource>
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

        // Todo: falseかつ親に条件があるときの挙動を考える
        // falseなら通常条件では発動しない。Alwaysでも親のSettingsの条件は受ける。
        public bool HasCondition = false;
        public ConditionSelection Condition = new();

        // 親のSettingsから集めたParameter Driverの後に追加する。
        public bool HasParameterDriver = false;
        public ParameterDriverSettingsSource ParameterDriver = new();

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

        FacialBlendShapeDataSource? IReferenceableExpressionSettings<FacialBlendShapeDataSource>.SettingsSource
            => FacialBlendShapes;

        EyeBlinkSettingsSource? IReferenceableExpressionSettings<EyeBlinkSettingsSource>.SettingsSource
            => HasEyeBlink ? EyeBlink : null;

        LipSyncSettingsSource? IReferenceableExpressionSettings<LipSyncSettingsSource>.SettingsSource
            => HasLipSync ? LipSync : null;

        ParameterDriverSettingsSource? IReferenceableExpressionSettings<ParameterDriverSettingsSource>.SettingsSource
            => HasParameterDriver ? ParameterDriver : null;
    }
}