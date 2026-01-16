using System.Reflection;
using UnityEditor;

namespace Aoyon.FaceTune;

internal static class UndoUtility
{
    private static readonly BindingFlags UndoReflectionFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly MethodInfo? HasUndoMethod = typeof(Undo).GetMethod("HasUndo", UndoReflectionFlags);
    private static readonly MethodInfo? HasRedoMethod = typeof(Undo).GetMethod("HasRedo", UndoReflectionFlags);

    public static bool TryHasUndo(out bool canUndo)
    {
        if (HasUndoMethod?.Invoke(null, null) is bool hasUndo)
        {
            canUndo = hasUndo;
            return true;
        }
        canUndo = false;
        return false;
    }

    public static bool TryHasRedo(out bool canRedo)
    {
        if (HasRedoMethod?.Invoke(null, null) is bool hasRedo)
        {
            canRedo = hasRedo;
            return true;
        }
        canRedo = false;
        return false;
    }
}

