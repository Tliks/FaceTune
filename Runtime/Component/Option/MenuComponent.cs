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

        public enum ToggleParameterType
        {
            Bool = 0,
            Int = 10
        }

        public Kind MenuKind;
        public MenuSettings Menu = new();

        // ParameterNameで既存Parameterを参照する。Folderでは使用しない。
        public bool UseExistingParameter;
        public ToggleParameterType ExistingToggleParameterType;

        // 同じGroupNameのToggleで一つのInt Parameterを共有する。
        public bool GenerateParameterGroup;

        // Groupで共有するParameterNameの生成元。
        public string GroupName = string.Empty;

        // 生成時に空なら自動生成する。
        public string ParameterName = string.Empty;

        // 生成Parameter用。Groupでは両方true固定。
        public bool Synced;
        public bool Saved;

        // 生成Parameter用。Menuのデフォルト状態。Toggleでは0以外を選択状態とする。
        [Range(0f, 1f)]
        public float DefaultValue;

        // Groupでは自動割り当てする。
        public float SelectedValue;

#region Defaults

        private void Reset()
        {
            Synced = DefaultSynced;
            Saved = DefaultSaved;
            SelectedValue = DefaultSelectedValue;
        }

        internal const bool DefaultSynced = true;
        internal const bool DefaultSaved = true;
        internal const float DefaultSelectedValue = 1f;

#endregion

    }
}
