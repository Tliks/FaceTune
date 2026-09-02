namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal sealed class SettingsComponent : FaceTuneTagComponent,
        ISettingProvider<FacialBlendShapeData>,
        ISettingProviderWithReference<EyeBlinkSettings>,
        ISettingProviderWithReference<LipSyncSettings>,
        ISettingProvider<TransitionSettings>,
        ISettingProvider<PrioritySettings>
    {
        internal const string ComponentName = ComponentNamePrefix + "Settings";

        // このGameObjectより下のExpressionへ、親側から順に重ねる。
        public bool HasFacialBlendShapes = false;
        public FacialBlendShapeData FacialBlendShapes = new();
        public bool ApplyToRenderer = DefaultApplyToRenderer;

        // Menuと、選択中だけこのGameObjectより下を有効にする条件の組。
        public bool ExpressionSetEnabled = false;
        public ExpressionSetSettings ExpressionSet = new();

        // このGameObjectより下にあるExpressionの通常条件へANDする。
        public bool HasCondition = false;
        public Condition Condition = CreateDefaultCondition();

        // このGameObject自身と配下で、最も近いSettingsの値を使う。
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


#region Defaults

        internal const bool DefaultApplyToRenderer = false;

        internal static Condition CreateDefaultCondition()
            => new(new ConditionCase());

#endregion

#region Interfaces

        (bool Enabled, FacialBlendShapeData Value) ISettingProvider<FacialBlendShapeData>.Setting => (HasFacialBlendShapes, FacialBlendShapes);
        (bool Enabled, EyeBlinkSettings Value) ISettingProvider<EyeBlinkSettings>.Setting => (HasEyeBlink, EyeBlink);
        (SettingsReferenceMode Mode, Transform? Source) ISettingProviderWithReference<EyeBlinkSettings>.Reference => (EyeBlinkReference.Mode, EyeBlinkReference.Source);
        (bool Enabled, LipSyncSettings Value) ISettingProvider<LipSyncSettings>.Setting => (HasLipSync, LipSync);
        (SettingsReferenceMode Mode, Transform? Source) ISettingProviderWithReference<LipSyncSettings>.Reference => (LipSyncReference.Mode, LipSyncReference.Source);
        (bool Enabled, TransitionSettings Value) ISettingProvider<TransitionSettings>.Setting => (HasTransition, Transition);
        (bool Enabled, PrioritySettings Value) ISettingProvider<PrioritySettings>.Setting => (HasPriority, Priority);

#endregion

    }
}
