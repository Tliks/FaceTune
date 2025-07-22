using nadena.dev.ndmf.preview;

namespace aoyon.facetune.gui;

internal class TogglablePreviewDrawer
{
    public static void Draw(TogglablePreviewNode toggleNode)
    {
        var label = toggleNode.IsEnabled.Value ? "Disable Preview" : "Enable Preview";
        if (GUILayout.Button(label))
        {
            toggleNode.IsEnabled.Value = !toggleNode.IsEnabled.Value;
        }
    }
}

