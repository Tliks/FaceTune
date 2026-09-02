namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class MenuComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = ComponentNamePrefix + "Menu";

        public enum Kind
        {
            Toggle = 0,
            Radial = 10,
            Folder = 20
        }

        public Kind MenuKind = DefaultMenuKind;
        public MenuSettings Menu = new();

        // ParameterNameで既存Parameterを参照する。Folderでは使用しない。
        public bool UseExistingParameter = DefaultUseExistingParameter;

        // 同じGroupNameのToggleで一つのInt Parameterを共有する。
        public bool GenerateParameterGroup = DefaultGenerateParameterGroup;

        // Groupで共有するParameterNameの生成元。
        public string GroupName = DefaultGroupName;

        // 生成時に空なら自動生成する。
        public string ParameterName = DefaultParameterName;

        // 生成Parameter用。Groupでは両方true固定。
        public bool Synced = DefaultSynced;
        public bool Saved = DefaultSaved;

        // 生成Parameter用。Menuのデフォルト状態。Toggleでは0以外を選択状態とする。
        [Range(0f, 1f)]
        public float DefaultValue = DefaultParameterValue;

        // Groupでは自動割り当てする。
        public float SelectedValue = DefaultSelectedValue;

#region Defaults

        internal const Kind DefaultMenuKind = Kind.Toggle;
        internal const bool DefaultUseExistingParameter = false;
        internal const bool DefaultGenerateParameterGroup = false;
        internal const bool DefaultSynced = true;
        internal const bool DefaultSaved = true;
        internal const string DefaultGroupName = "";
        internal const string DefaultParameterName = "";
        internal const float DefaultParameterValue = 0f;
        internal const float DefaultSelectedValue = 1f;

#endregion

    }
}
