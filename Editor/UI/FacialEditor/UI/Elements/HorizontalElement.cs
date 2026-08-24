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

internal class VerticalElement : VisualElement
{
    public new class UxmlFactory : UxmlFactory<VerticalElement, UxmlTraits> { }

    public VerticalElement()
    {
        style.flexDirection = FlexDirection.Column;
    }
}

internal abstract class SpacedElement : VisualElement
{
    private readonly string _gapClass;

    protected SpacedElement(FlexDirection direction, string gapClass)
    {
        _gapClass = gapClass;
        style.flexDirection = direction;
        RegisterCallback<AttachToPanelEvent>(_ => RefreshSpacing());
    }

    internal void RefreshSpacing()
    {
        var content = new List<VisualElement>();
        for (var i = 0; i < childCount; i++)
        {
            var child = ElementAt(i);
            if (!child.ClassListContains(_gapClass) && child.style.display.value != DisplayStyle.None)
                content.Add(child);
        }

        for (var i = childCount - 1; i >= 0; i--)
        {
            var child = ElementAt(i);
            if (child.ClassListContains(_gapClass)) child.RemoveFromHierarchy();
        }

        for (var i = 1; i < content.Count; i++)
        {
            var gap = new VisualElement { pickingMode = PickingMode.Ignore };
            gap.AddToClassList(_gapClass);
            hierarchy.Insert(IndexOf(content[i]), gap);
        }
    }
}

internal class SpacedHorizontalElement : SpacedElement
{
    public new class UxmlFactory : UxmlFactory<SpacedHorizontalElement, UxmlTraits> { }

    public SpacedHorizontalElement() : base(FlexDirection.Row, "horizontal-layout-gap") { }
}

internal class SpacedVerticalElement : SpacedElement
{
    public new class UxmlFactory : UxmlFactory<SpacedVerticalElement, UxmlTraits> { }

    public SpacedVerticalElement() : base(FlexDirection.Column, "vertical-layout-gap") { }
}
