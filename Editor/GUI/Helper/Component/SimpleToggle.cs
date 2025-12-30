using UnityEngine;
using UnityEngine.UIElements;

namespace Aoyon.FaceTune.Gui.Components;

internal class SimpleToggle : Button, INotifyValueChanged<bool>
{
    public new class UxmlFactory : UxmlFactory<SimpleToggle, UxmlTraits> { }

    public new class UxmlTraits : VisualElement.UxmlTraits
    {
        readonly UxmlBoolAttributeDescription valueAttribute = new() { name = "value", defaultValue = false };
        readonly UxmlStringAttributeDescription textAttribute = new() { name = "text", defaultValue = "" };

        public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
        {
            base.Init(ve, bag, cc);
            var element = (ve as SimpleToggle)!;
            element.SetValueWithoutNotify(valueAttribute.GetValueFromBag(bag, cc));
            element.text = textAttribute.GetValueFromBag(bag, cc);
        }
    }

    private bool _value;

    public bool value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            var previousValue = _value;
            _value = value;
            UpdateVisualState();

            using var changeEvent = ChangeEvent<bool>.GetPooled(previousValue, _value);
            changeEvent.target = this;
            SendEvent(changeEvent);
        }
    }

    public SimpleToggle()
    {
        clicked += () =>
        {
            value = !value;
        };

        UpdateVisualState();
    }

    private void UpdateVisualState()
    {
        if (_value)
        {
            style.color = new StyleColor(new Color(0.55f, 0.75f, 0.9f));
        }
        else
        {
            style.color = new StyleColor(new Color(1f, 1f, 1f, 0.7f));
        }
    }

    public void SetValueWithoutNotify(bool newValue)
    {
        _value = newValue;
        UpdateVisualState();
    }

    public void RegisterValueChangedCallback(EventCallback<ChangeEvent<bool>> callback)
    {
        RegisterCallback(callback);
    }

    public void UnregisterValueChangedCallback(EventCallback<ChangeEvent<bool>> callback)
    {
        UnregisterCallback(callback);
    }
}
