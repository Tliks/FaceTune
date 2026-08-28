#pragma warning disable CS0618

namespace Aoyon.FaceTune.Gui
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(LegacyFaceTuneTagComponent), true)]
    internal sealed class LegacyFaceTuneTagComponentEditor
        : FaceTuneEditorBase<LegacyFaceTuneTagComponent>
    {
        private const string WarningMessageKey = "legacy.component.deprecation.warning";

        protected override float GetInspectorHeight()
        {
            var warning = WarningMessageKey.LS();
            var warningHeight = GUIHelper.GetHelpBoxHeight(warning, MessageType.Warning);
            var contentHeight = base.GetInspectorHeight();
            return contentHeight > 0f
                ? warningHeight + GUIHelper.VerticalSpacing + contentHeight
                : warningHeight;
        }

        protected override void DrawInspector(Rect position)
        {
            var warning = WarningMessageKey.LS();
            var warningHeight = GUIHelper.GetHelpBoxHeight(warning, position.width, MessageType.Warning);
            position.height = warningHeight;
            GUIHelper.HelpBox(position, warning, MessageType.Warning);

            position.y += warningHeight + GUIHelper.VerticalSpacing;
            base.DrawInspector(position);
        }
    }
}

#pragma warning restore CS0618
