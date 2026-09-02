namespace Aoyon.FaceTune
{
    [AddComponentMenu(MenuPathPrefix + ComponentName)]
    internal class ExpressionComponent : FaceTuneTagComponent,
        IHasConditions,
        IExpressionDefinitionProviderWithReference,
        ISettingProvider<ExpressionBehavior>,
        ISettingProvider<MultiFrameSettings>,
        ISettingProvider<FacialBlendShapeData>,
        ISettingProvider<NonFacialAnimationData>,
        ISettingProviderWithReference<EyeBlinkSettings>,
        ISettingProviderWithReference<LipSyncSettings>,
        ISettingProvider<TransitionSettings>,
        ISettingProvider<PrioritySettings>
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
        public ExpressionWriteMode WriteMode = ExpressionBehavior.Default.WriteMode;
        public MultiFrameSettings MultiFrame = new();
        // この表情再生中におけるまばたき/リップシンクの扱い。
        public TrackingPermission AllowEyeBlink = ExpressionBehavior.Default.AllowEyeBlink;
        public TrackingPermission AllowLipSync = ExpressionBehavior.Default.AllowLipSync;

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

        SettingsReferenceMode IExpressionDefinitionProviderWithReference.DefinitionMode => ExpressionDataReference.Mode;
        Transform? IExpressionDefinitionProviderWithReference.DefinitionSource => ExpressionDataReference.Source;

        IEnumerable<Condition> IHasConditions.Conditions
            => HasCondition && Condition.Mode == ConditionSelection.Kind.Conditional
                ? new[] { Condition.Condition }
                : Array.Empty<Condition>();

        (bool Enabled, FacialBlendShapeData Value) ISettingProvider<FacialBlendShapeData>.Setting => (true, FacialBlendShapes);
        (bool Enabled, NonFacialAnimationData Value) ISettingProvider<NonFacialAnimationData>.Setting => (true, NonFacialAnimations);
        (bool Enabled, EyeBlinkSettings Value) ISettingProvider<EyeBlinkSettings>.Setting => (HasEyeBlink, EyeBlink);
        (SettingsReferenceMode Mode, Transform? Source) ISettingProviderWithReference<EyeBlinkSettings>.Reference => (EyeBlinkReference.Mode, EyeBlinkReference.Source);
        (bool Enabled, LipSyncSettings Value) ISettingProvider<LipSyncSettings>.Setting => (HasLipSync, LipSync);
        (SettingsReferenceMode Mode, Transform? Source) ISettingProviderWithReference<LipSyncSettings>.Reference => (LipSyncReference.Mode, LipSyncReference.Source);
        (bool Enabled, ExpressionBehavior Value) ISettingProvider<ExpressionBehavior>.Setting => (true, new(WriteMode, AllowEyeBlink, AllowLipSync));
        (bool Enabled, MultiFrameSettings Value) ISettingProvider<MultiFrameSettings>.Setting => (true, MultiFrame);
        (bool Enabled, TransitionSettings Value) ISettingProvider<TransitionSettings>.Setting => (HasTransition, Transition);
        (bool Enabled, PrioritySettings Value) ISettingProvider<PrioritySettings>.Setting => (HasPriority, Priority);

#endregion

    }
}