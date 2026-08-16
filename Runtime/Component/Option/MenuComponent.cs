namespace Aoyon.FaceTune
{
    [DisallowMultipleComponent]
    [AddComponentMenu(OptionMenuPathPrefix + ComponentName)]
    internal class MenuComponent : FaceTuneTagComponent
    {
        internal const string ComponentName = ComponentNamePrefix + "Menu";

        public enum Kind
        {
            Toggle,
            Radial,
            Folder
        }

        public Kind MenuKind = Kind.Toggle;
        public MenuSettings Menu = new();

        // Folder以外で使用する。
        public bool UseExistingParameter;

        // GenerateかつToggleの場合に、同名groupで一つのparameterを生成する。
        public bool GenerateParameterGroup;
        public string GroupName = string.Empty;

        // Generate: 空なら自動命名、非空なら生成parameter名。
        // Existing: 既存parameter名。
        public string Name = string.Empty;

        public bool Synced = true;
        public bool Saved = true;

        [Range(0f, 1f)]
        public float InitialValue;

        // Toggleの選択状態を表すparameter値。
        // GroupではNormalizeで割り当てる。
        public float SelectedValue = 1f;
    }
}
