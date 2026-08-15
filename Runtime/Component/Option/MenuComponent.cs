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

        public enum ParameterBinding
        {
            Generate,
            GenerateGroup,
            Existing
        }

        public Kind MenuKind = Kind.Toggle;
        public MenuSettings Menu = new();

        // Folder以外で使用する。
        public ParameterBinding Binding = ParameterBinding.Generate;

        // Generate: 空なら自動命名、非空なら生成parameter名。
        // Existing: 既存parameter名。
        public string Name = string.Empty;

        // GenerateGroupで使用するauthoring上のgroup名。
        public string GroupName = string.Empty;

        [Range(0f, 1f)]
        public float InitialValue;

        // Toggleの選択状態を表すparameter値。
        // GenerateGroupではNormalizeで割り当てる。
        public float SelectedValue = 1f;
    }
}
