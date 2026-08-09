namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal sealed class SettingsComponent : FaceTuneTagComponent,
        IHasConditions,
        IReferenceableExpressionSettings<FacialBlendShapeDataSource>,
        IReferenceableExpressionSettings<EyeBlinkSettingsSource>,
        IReferenceableExpressionSettings<LipSyncSettingsSource>,
        IReferenceableExpressionSettings<ParameterDriverSettingsSource>
    {
        internal const string ComponentName = ComponentNamePrefix + "Settings";

        // このGameObject自身と配下のExpressionへ、親側から順に重ねる。
        public bool HasFacialBlendShapes = false;
        public FacialBlendShapeDataSource FacialBlendShapes = new();
        public bool ApplyToRenderer = false;

        // Menuと、選択中だけこのGameObject自身と配下を有効にする条件の組。
        public bool ExpressionSetEnabled = false;
        public ExpressionSetSettings ExpressionSet = new();

        // このGameObject自身と配下にあるExpressionの通常条件へANDする。
        public bool HasCondition = false;
        public Condition Condition = new();

        // このGameObject自身と配下へ追加する。
        public bool HasParameterDriver = false;
        public ParameterDriverSettingsSource ParameterDriver = new();

        // このGameObject自身と配下で、最も近いSettingsの値を使う。
        public bool HasEyeBlink = false;
        public EyeBlinkSettingsSource EyeBlink = new();

        public bool HasLipSync = false;
        public LipSyncSettingsSource LipSync = new();

        public bool HasTransition = false;
        public TransitionSettings Transition = new();

        public bool HasPriority = false;
        public PrioritySettings Priority = new();


        IEnumerable<Condition> IHasConditions.Conditions
            => HasCondition
                ? new[] { Condition }
                : Array.Empty<Condition>();

        FacialBlendShapeDataSource? IReferenceableExpressionSettings<FacialBlendShapeDataSource>.SettingsSource
            => HasFacialBlendShapes ? FacialBlendShapes : null;

        EyeBlinkSettingsSource? IReferenceableExpressionSettings<EyeBlinkSettingsSource>.SettingsSource
            => HasEyeBlink ? EyeBlink : null;

        LipSyncSettingsSource? IReferenceableExpressionSettings<LipSyncSettingsSource>.SettingsSource
            => HasLipSync ? LipSync : null;

        ParameterDriverSettingsSource? IReferenceableExpressionSettings<ParameterDriverSettingsSource>.SettingsSource
            => HasParameterDriver ? ParameterDriver : null;
    }
}
