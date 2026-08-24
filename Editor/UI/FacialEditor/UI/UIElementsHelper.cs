using UnityEngine.UIElements;
using Aoyon.FaceTune.Gui.Components;

namespace Aoyon.FaceTune.Gui;

internal static class UIElementsHelper
{
    public static void SetVisible(this VisualElement element, bool visible)
    {
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        for (var parent = element.parent; parent != null; parent = parent.parent)
        {
            if (parent is SpacedElement spaced) spaced.RefreshSpacing();
        }
    }
}
