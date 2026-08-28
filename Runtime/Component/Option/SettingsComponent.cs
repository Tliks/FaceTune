namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal sealed class SettingsComponent : FaceTuneTagComponent,
        IHasConditions,
        IReferenceableExpressionSettings<FacialBlendShapeData>,
        IReferenceableExpressionSettings<EyeBlinkSettings>,
        IReferenceableExpressionSettings<LipSyncSettings>
    {
        internal const string ComponentName = ComponentNamePrefix + "Settings";
        internal const bool DefaultApplyToRenderer = false;

        // このGameObjectより下のExpressionへ、親側から順に重ねる。
        public bool HasFacialBlendShapes = false;
        public SettingsReference FacialBlendShapesReference = new();
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


        internal static Condition CreateDefaultCondition()
            => new(new ConditionCase());

        IEnumerable<Condition> IHasConditions.Conditions
            => HasCondition
                ? new[] { Condition }
                : Array.Empty<Condition>();

        ReferenceableExpressionSettings<FacialBlendShapeData> IReferenceableExpressionSettings<FacialBlendShapeData>.Settings
            => new(HasFacialBlendShapes, FacialBlendShapesReference.Mode, FacialBlendShapesReference.Source, FacialBlendShapes);

        ReferenceableExpressionSettings<EyeBlinkSettings> IReferenceableExpressionSettings<EyeBlinkSettings>.Settings
            => new(HasEyeBlink, EyeBlinkReference.Mode, EyeBlinkReference.Source, EyeBlink);

        ReferenceableExpressionSettings<LipSyncSettings> IReferenceableExpressionSettings<LipSyncSettings>.Settings
            => new(HasLipSync, LipSyncReference.Mode, LipSyncReference.Source, LipSync);

    }
}
