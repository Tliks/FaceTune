using System.Reflection;

namespace Aoyon.FaceTune.Gui;

internal static class SerializedPropertyExtensions
{
    internal static void CopyFrom(this SerializedProperty property, object? source)
    {
        if (source != null)
        {
            CopyValue(property, source);
            return;
        }

        if (property.propertyType == SerializedPropertyType.ObjectReference)
            property.objectReferenceValue = null;
        else if (property.propertyType == SerializedPropertyType.ManagedReference)
            property.managedReferenceValue = null;
    }

    private static void CopyValue(SerializedProperty property, object source)
    {
        if (property.isArray && property.propertyType != SerializedPropertyType.String)
        {
            var values = (IList)source;
            property.arraySize = values.Count;
            for (var i = 0; i < values.Count; i++) CopyValue(property.GetArrayElementAtIndex(i), values[i]);
            return;
        }
        if (property.propertyType != SerializedPropertyType.Generic)
        {
            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer: property.intValue = Convert.ToInt32(source); break;
                case SerializedPropertyType.Boolean: property.boolValue = (bool)source; break;
                case SerializedPropertyType.Float: property.floatValue = Convert.ToSingle(source); break;
                case SerializedPropertyType.String: property.stringValue = source.ToString(); break;
                case SerializedPropertyType.Enum: property.enumValueFlag = Convert.ToInt32(source); break;
                case SerializedPropertyType.ObjectReference: property.objectReferenceValue = source as Object; break;
                case SerializedPropertyType.ManagedReference: property.managedReferenceValue = source; break;
                case SerializedPropertyType.AnimationCurve: property.animationCurveValue = (AnimationCurve)source; break;
                case SerializedPropertyType.Vector2: property.vector2Value = (Vector2)source; break;
            }
            return;
        }

        var child = property.Copy();
        if (!child.Next(true)) return;
        var depth = child.depth;
        var type = source.GetType();
        do
        {
            if (child.depth != depth) break;
            var field = type.GetField(child.name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var value = field?.GetValue(source);
            if (field == null) continue;
            if (value == null)
            {
                if (child.isArray && child.propertyType != SerializedPropertyType.String) child.ClearArray();
                else if (child.propertyType == SerializedPropertyType.ObjectReference) child.objectReferenceValue = null;
                else if (child.propertyType == SerializedPropertyType.ManagedReference) child.managedReferenceValue = null;
                continue;
            }
            CopyValue(child, value);
        } while (child.Next(false));
    }
}
