using Aoyon.FaceTune.Gui.Components;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal sealed class EyeBlinkPanel : IDisposable
{
    private readonly FacialShapesEditorContext _context;
    private readonly ManagerShapeList[] _lists;
    private readonly UnselectedPanel[] _availablePanels;
    private readonly SimpleToggle[] _buttons;

    public VisualElement SelectedElement { get; } = new ScrollView();
    public VisualElement AvailableElement { get; } = new VisualElement();

    public EyeBlinkPanel(FacialShapesEditorContext context)
    {
        _context = context;
        _lists = context.DataManagers
            .Select(manager => new ManagerShapeList(manager, context.GroupManager))
            .ToArray();
        _availablePanels = context.DataManagers
            .Select(manager => new UnselectedPanel(
                manager,
                context.GroupManager,
                context.PreviewManager))
            .ToArray();
        var labels = new[]
        {
            "previewOverlay.eyeBlink.label".LS(),
            "eyeBlink.simple.conflictBlendShapes.label".LS()
        };
        _buttons = new SimpleToggle[_lists.Length];

        for (var index = 0; index < _lists.Length; index++)
        {
            var listIndex = index;
            var section = new SpacedVerticalElement();
            if (index > 0)
                section.style.marginTop = FacialShapeUI.RowHeight + FacialShapeUI.Spacing;
            var button = new SimpleToggle
            {
                text = labels[index],
                value = index == context.ActiveListIndex
            };
            button.style.alignSelf = Align.FlexStart;
            button.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue)
                    _context.SetActiveList(listIndex);
                else if (_context.ActiveListIndex == listIndex)
                    button.SetValueWithoutNotify(true);
            });
            section.Add(button);
            section.Add(_lists[index].Element);
            SelectedElement.Add(section);
            _buttons[index] = button;
        }

        SelectedElement.style.flexGrow = 1f;
        AvailableElement.style.flexGrow = 1f;
        context.ActiveListChanged += UpdateSelection;
        context.GroupManager.OnGroupSelectionChanged += _ => RebuildLists();
        context.GroupManager.OnLeftSelectionChanged += _ => RebuildLists();
        UpdateSelection();
    }

    private void RebuildLists()
    {
        foreach (var list in _lists) list.Rebuild();
    }

    private void UpdateSelection()
    {
        for (var index = 0; index < _lists.Length; index++)
        {
            var selected = index == _context.ActiveListIndex;
            _buttons[index].SetValueWithoutNotify(selected);
            _lists[index].Element.SetEnabled(selected);
        }
        AvailableElement.Clear();
        AvailableElement.Add(_availablePanels[_context.ActiveListIndex].Element);
    }

    public void Dispose()
    {
        _context.ActiveListChanged -= UpdateSelection;
    }
}
