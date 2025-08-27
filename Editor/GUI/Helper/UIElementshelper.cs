using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui;

internal static class UIElementsHelper
{
    public static void SetVisible(this VisualElement element, bool visible)
    {
        element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
    }
}