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
        public bool HasFacialBlendShapes;
        public FacialBlendShapeData FacialBlendShapes = new();
        public bool ApplyToRenderer;

        // Menuと、選択中だけこのGameObjectより下を有効にする条件の組。
        public bool ExpressionSetEnabled;
        public ExpressionSetSettings ExpressionSet = new();

        // このGameObjectより下にあるExpressionの通常条件へANDする。
        public bool HasCondition;
        public Condition Condition = new();

        // このGameObject自身と配下で、最も近いSettingsの値を使う。
        public bool HasEyeBlink;
        public SettingsReference EyeBlinkReference = new();
        public EyeBlinkSettings EyeBlink = new();

        public bool HasLipSync;
        public SettingsReference LipSyncReference = new();
        public LipSyncSettings LipSync = new();

        public bool HasTransition;
        public TransitionSettings Transition = new();

        public bool HasPriority;
        public PrioritySettings Priority = new();


#region Defaults

        private void Reset()
        {
            Condition = CreateDefaultCondition();
        }

        internal static Condition CreateDefaultCondition()
            => new(new ConditionCase());

#endregion

#region Interfaces

        (bool Enabled, FacialBlendShapeData Value) ISettingProvider<FacialBlendShapeData>.Setting => (HasFacialBlendShapes, FacialBlendShapes);
        (bool Enabled, EyeBlinkSettings Value) ISettingProvider<EyeBlinkSettings>.Setting => (HasEyeBlink, EyeBlink);
        (SettingsReferenceMode Mode, FaceTuneTagComponent? Source) ISettingProviderWithReference<EyeBlinkSettings>.Reference => (EyeBlinkReference.Mode, EyeBlinkReference.ComponentSource);
        (bool Enabled, LipSyncSettings Value) ISettingProvider<LipSyncSettings>.Setting => (HasLipSync, LipSync);
        (SettingsReferenceMode Mode, FaceTuneTagComponent? Source) ISettingProviderWithReference<LipSyncSettings>.Reference => (LipSyncReference.Mode, LipSyncReference.ComponentSource);
        (bool Enabled, TransitionSettings Value) ISettingProvider<TransitionSettings>.Setting => (HasTransition, Transition);
        (bool Enabled, PrioritySettings Value) ISettingProvider<PrioritySettings>.Setting => (HasPriority, Priority);

#endregion

    }
}
