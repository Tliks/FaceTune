using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.Components;

internal class HorizontalElement : VisualElement
{
    public new class UxmlFactory : UxmlFactory<HorizontalElement, UxmlTraits> { }

    public HorizontalElement()
    {
        style.flexDirection = FlexDirection.Row;
    }
}
