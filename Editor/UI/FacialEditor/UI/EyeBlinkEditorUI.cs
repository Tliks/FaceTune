using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.ShapesEditor;

internal sealed class EyeBlinkEditorUI : IDisposable
{
    private readonly FacialShapesEditorContext _context;
    private readonly EyeBlinkModeSession _session;
    private readonly VisualElement _selectedContainer;
    private readonly VisualElement _availableContainer;
    private UnselectedPanel? _builtInAvailable;
    private readonly PopupField<string> _modeField;
    private readonly string[] _labels;
    private readonly PreviewTimelineElement _timeline;
    private EyeBlinkPanel? _simplePanel;
    private EyeBlinkBuiltInPanel? _builtInPanel;
    private SelectedPanel? _customSelected;
    private UnselectedPanel? _customAvailable;

    public EyeBlinkEditorUI(
        FacialShapesEditorContext context,
        EyeBlinkModeSession session,
        VisualElement selectedContainer,
        VisualElement availableContainer,
        VisualElement timelineContainer)
    {
        _context = context;
        _session = session;
        _selectedContainer = selectedContainer;
        _availableContainer = availableContainer;
        _labels = new[]
        {
            "eyeBlinkMode.option.builtIn".LS(),
            "eyeBlinkMode.option.simpleAnimation".LS(),
            "eyeBlinkMode.option.customAnimation".LS()
        };
        _modeField = new PopupField<string>(
            "shapesEditor.mode.label".LS(), _labels.ToList(), ModeIndex(session.Mode));
        _modeField.RegisterValueChangedCallback(evt =>
        {
            var mode = Array.IndexOf(_labels, evt.newValue) switch
            {
                1 => EyeBlinkSettings.Kind.SimpleAnimation,
                2 => EyeBlinkSettings.Kind.CustomAnimation,
                _ => EyeBlinkSettings.Kind.BuiltIn
            };
            _session.SetMode(mode);
        });

        _timeline = new PreviewTimelineElement(
            context.InitialPreviewTime,
            context.PreviewManager.SetNormalizedTime,
            () => session.PlaybackDurationSeconds);
        timelineContainer.Add(_timeline.Element);
        _session.ModeChanged += OnModeChanged;
        _context.ActiveListChanged += ShowMode;
        ShowMode();
    }

    private static int ModeIndex(EyeBlinkSettings.Kind mode)
        => mode switch
        {
            EyeBlinkSettings.Kind.SimpleAnimation => 1,
            EyeBlinkSettings.Kind.CustomAnimation => 2,
            _ => 0
        };

    private void OnModeChanged()
    {
        _context.PreviewManager.CurrentHoveredIndex = -1;
        if (_session.Mode == EyeBlinkSettings.Kind.BuiltIn)
            _timeline.Seek(1f);
        ShowMode();
    }

    private void ShowMode()
    {
        if (_session.Mode == EyeBlinkSettings.Kind.CustomAnimation
            && _context.ActiveListIndex != 2)
        {
            _context.SetActiveList(2);
            return;
        }
        if (_session.Mode != EyeBlinkSettings.Kind.CustomAnimation
            && (_context.ActiveListIndex == 2
                || _session.Mode == EyeBlinkSettings.Kind.BuiltIn
                   && _context.ActiveListIndex != 0))
        {
            _context.SetActiveList(0);
            return;
        }

        _modeField.SetValueWithoutNotify(_labels[ModeIndex(_session.Mode)]);
        _selectedContainer.Clear();
        _availableContainer.Clear();
        _selectedContainer.Add(_modeField);

        switch (_session.Mode)
        {
            case EyeBlinkSettings.Kind.BuiltIn:
                _builtInPanel ??= new EyeBlinkBuiltInPanel(_context, _session.BuiltIn);
                _builtInAvailable ??= new UnselectedPanel(
                    _context.DataManagers[0], _context.GroupManager, _context.PreviewManager);
                _selectedContainer.Add(_builtInPanel.Element);
                _availableContainer.Add(_builtInAvailable.Element);
                _builtInAvailable.Element.SetEnabled(false);
                break;
            case EyeBlinkSettings.Kind.SimpleAnimation:
                _simplePanel ??= new EyeBlinkPanel(_context);
                _selectedContainer.Add(_simplePanel.SelectedElement);
                _availableContainer.Add(_simplePanel.AvailableElement);
                break;
            case EyeBlinkSettings.Kind.CustomAnimation:
                ShowCustom();
                break;
        }
    }

    private void ShowCustom()
    {
        if (_customSelected == null)
        {
            var manager = _context.DataManagers[2];
            _customSelected = new SelectedPanel(manager, _context.GroupManager, false);
            var available = new UnselectedPanel(
                manager, _context.GroupManager, _context.PreviewManager);
            _customAvailable = available;
            _customSelected.OnSelectedItemNameClicked += index =>
                available.Element.schedule.Execute(() =>
                    available.ScrollToNearestKeyIndex(index, true, false));
        }
        _selectedContainer.Add(_customSelected.Element);
        _availableContainer.Add(_customAvailable!.Element);
    }

    public void Dispose()
    {
        _session.ModeChanged -= OnModeChanged;
        _context.ActiveListChanged -= ShowMode;
        _simplePanel?.Dispose();
        _timeline.Dispose();
    }
}
