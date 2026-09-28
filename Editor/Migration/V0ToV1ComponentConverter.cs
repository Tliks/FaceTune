#pragma warning disable CS0618 // Read the serialized fields removed by the new schema.

namespace Aoyon.FaceTune.Migration;

internal static class V0ToV1ComponentConverter
{
    internal static List<Action> Prepare(IEnumerable<GameObject> roots)
    {
        var components = roots.SelectMany(root => root.GetComponentsInChildren<FaceTuneTagComponent>(true)).ToArray();
        var modifications = new Dictionary<GameObject, HashSet<Object>>();
        var childrenByExpression = new Dictionary<ExpressionComponent, ExpressionDataComponent[]>();
        var targets = components.Where(component => ShouldConvert(component, modifications, childrenByExpression)).ToHashSet();
        var plan = new List<Action>();
        if (targets.Count == 0) return plan;

        // Resolve everything against the old hierarchy before changing any component. In
        // particular, an old reference to an Expression reads its LOCAL data, not its children.
        var facial = new Dictionary<FaceTuneTagComponent, FacialBlendShapeData?>();
        var nonFacial = new Dictionary<FaceTuneTagComponent, NonFacialAnimationData?>();
        foreach (var component in components)
        {
            if (!facial.ContainsKey(component)) ResolveFacial(component, new HashSet<Component>(), facial);
            if (!nonFacial.ContainsKey(component)) ResolveNonFacial(component, new HashSet<Component>(), nonFacial);
        }
        foreach (var component in targets)
        {
            var eye = ResolveReference(component, typeof(EyeBlinkSettings));
            var lip = ResolveReference(component, typeof(LipSyncSettings));
            switch (component)
            {
                case SettingsComponent settings:
                {
                    var shapes = facial[settings];
                    var convertedShapes = shapes == null ? null : PreserveFacialDataReference(settings, shapes);
                    plan.Add(() =>
                    {
                        ApplyReference(settings.EyeBlinkReference, eye);
                        ApplyReference(settings.LipSyncReference, lip);
                        if (convertedShapes != null) settings.FacialBlendShapes = convertedShapes;
                        else settings.HasFacialBlendShapes = false;
                        Mark(settings);
                    });
                    break;
                }
                case ExpressionDataComponent data:
                {
                    var shapes = facial[data];
                    var convertedShapes = shapes == null ? null : PreserveFacialDataReference(data, shapes);
                    var animations = nonFacial[data];
                    var convertedAnimations = animations == null ? null : PreserveNonFacialDataReference(data, animations);
                    plan.Add(() =>
                    {
                        data.HasFacialBlendShapes = shapes != null;
                        data.FacialBlendShapes = convertedShapes ?? new FacialBlendShapeData();
                        data.HasNonFacialAnimations = animations != null;
                        data.NonFacialAnimations = convertedAnimations ?? new NonFacialAnimationData();
                        Mark(data);
                    });
                    break;
                }
                case ExpressionComponent expression:
                {
                    var entries = new List<FacialBlendShapeData.CompositeEntry>();
                    AddFacial(entries, expression, facial[expression]);
                    var localAnimations = nonFacial[expression];
                    var animations = localAnimations == null ? new NonFacialAnimationData()
                        : PreserveNonFacialDataReference(expression, localAnimations);
                    var children = GetChildren(expression, childrenByExpression);
                    foreach (var child in children)
                    {
                        AddChildFacial(entries, child, facial[child]);
                        if (nonFacial[child] != null)
                            animations.ComponentReferences.Add(child);
                    }
                    var shapes = new FacialBlendShapeData();
                    if (entries.Skip(1).Any(entry => entry.EntryKind != FacialBlendShapeData.CompositeEntry.Kind.Direct))
                    {
                        shapes.BlendShapeMode = FacialBlendShapeData.Mode.Composite;
                        shapes.CompositeEntries = entries;
                    }
                    else
                    {
                        if (entries.Count > 0)
                        {
                            if (entries[0].EntryKind == FacialBlendShapeData.CompositeEntry.Kind.Clip)
                            {
                                shapes.Clip = entries[0].Clip;
                                shapes.ClipOption = entries[0].ClipOption;
                            }
                            else if (entries[0].EntryKind == FacialBlendShapeData.CompositeEntry.Kind.Reference)
                            {
                                shapes.BaseSource = FacialBlendShapeData.SimpleBaseSource.Reference;
                                shapes.ReferenceSource = entries[0].ReferenceSource;
                            }
                        }
                        foreach (var entry in entries)
                            shapes.BlendShapeAnimations.AddRange(entry.BlendShapeAnimations);
                    }
                    plan.Add(() =>
                    {
                        ApplyReference(expression.EyeBlinkReference, eye);
                        ApplyReference(expression.LipSyncReference, lip);
                        expression.FacialBlendShapes = shapes;
                        expression.NonFacialAnimations = animations;
                        Mark(expression);
                    });
                    break;
                }
            }
        }
        return plan;
    }

    private static ExpressionDataComponent[] GetChildren(
        ExpressionComponent expression,
        Dictionary<ExpressionComponent, ExpressionDataComponent[]> cache)
    {
        if (!cache.TryGetValue(expression, out var children))
            cache[expression] = children = expression.GetComponentsInChildren<ExpressionDataComponent>(true);
        return children;
    }

    private static bool ShouldConvert(
        FaceTuneTagComponent component,
        Dictionary<GameObject, HashSet<Object>> modifications,
        Dictionary<ExpressionComponent, ExpressionDataComponent[]> children)
    {
        if (component is not (ExpressionComponent or SettingsComponent or ExpressionDataComponent))
            return false;
        if (!PrefabUtility.IsPartOfPrefabInstance(component)) return true;
        if (HasLegacyOverride(component, modifications) || HasChangedReferenceSource(component, modifications)) return true;
        // An overridden child affects the implicit composition of its inherited Expression.
        return component is ExpressionComponent expression
            && ChildDataDiffersFromSource(expression, modifications, children);
    }

    private static bool HasChangedReferenceSource(
        FaceTuneTagComponent component, Dictionary<GameObject, HashSet<Object>> modifications)
    {
        IEnumerable<(SettingsReference Reference, Type Kind)> references = component switch
        {
            SettingsComponent settings => new[]
            {
                (settings.FacialBlendShapesReference, typeof(FacialBlendShapeData)),
                (settings.EyeBlinkReference, typeof(EyeBlinkSettings)),
                (settings.LipSyncReference, typeof(LipSyncSettings))
            },
            ExpressionComponent expression => new[]
            {
                (expression.FacialBlendShapesReference, typeof(FacialBlendShapeData)),
                (expression.NonFacialAnimationsReference, typeof(NonFacialAnimationData)),
                (expression.EyeBlinkReference, typeof(EyeBlinkSettings)),
                (expression.LipSyncReference, typeof(LipSyncSettings))
            },
            ExpressionDataComponent data => new[]
            {
                (data.FacialBlendShapesReference, typeof(FacialBlendShapeData)),
                (data.NonFacialAnimationsReference, typeof(NonFacialAnimationData))
            },
            _ => Array.Empty<(SettingsReference, Type)>()
        };
        return references.Any(pair => ReferenceSourceDiffers(pair.Reference, pair.Kind, new HashSet<Component>(), modifications));
    }

    private static bool ReferenceSourceDiffers(SettingsReference reference, Type kind,
        HashSet<Component> visited, Dictionary<GameObject, HashSet<Object>> modifications)
    {
        if (reference.Mode != SettingsReferenceMode.Reference || reference.Source == null
            || !PrefabUtility.IsPartOfPrefabInstance(reference.Source)) return false;
        var source = Select(reference.Source, kind);
        var originalTransform = PrefabUtility.GetCorrespondingObjectFromSource(reference.Source);
        var originalSource = Select(originalTransform, kind);
        if (originalTransform == null || (source == null ? originalSource != null
                : originalSource == null || !SourceChainContains(source, originalSource))) return true;
        if (source == null || !visited.Add(source)) return false;
        if (HasLegacyOverride(source, modifications)) return true;
        var next = source switch
        {
            SettingsComponent settings when kind == typeof(FacialBlendShapeData) => settings.FacialBlendShapesReference,
            SettingsComponent settings when kind == typeof(EyeBlinkSettings) => settings.EyeBlinkReference,
            SettingsComponent settings => settings.LipSyncReference,
            ExpressionComponent expression when kind == typeof(FacialBlendShapeData) => expression.FacialBlendShapesReference,
            ExpressionComponent expression when kind == typeof(NonFacialAnimationData) => expression.NonFacialAnimationsReference,
            ExpressionComponent expression when kind == typeof(EyeBlinkSettings) => expression.EyeBlinkReference,
            ExpressionComponent expression => expression.LipSyncReference,
            ExpressionDataComponent data when kind == typeof(FacialBlendShapeData) => data.FacialBlendShapesReference,
            ExpressionDataComponent data => data.NonFacialAnimationsReference,
            _ => null
        };
        return next != null && ReferenceSourceDiffers(next, kind, visited, modifications);
    }

    private static bool ChildDataDiffersFromSource(ExpressionComponent expression,
        Dictionary<GameObject, HashSet<Object>> modifications,
        Dictionary<ExpressionComponent, ExpressionDataComponent[]> childCache)
    {
        var children = GetChildren(expression, childCache);
        if (children.Any(child => HasLegacyOverride(child, modifications)
                || HasChangedReferenceSource(child, modifications))) return true;
        var source = PrefabUtility.GetCorrespondingObjectFromSource(expression);
        if (source == null) return true;
        var inherited = GetChildren(source, childCache);
        return children.Length != inherited.Length || children.Where((child, index) =>
            !SourceChainContains(child, inherited[index])).Any();
    }

    private static bool HasLegacyOverride(FaceTuneTagComponent component,
        Dictionary<GameObject, HashSet<Object>> modifications)
    {
        if (!PrefabUtility.IsPartOfPrefabInstance(component)) return false;
        if (PrefabUtility.GetCorrespondingObjectFromSource(component) == null) return true;
        var root = PrefabUtility.GetNearestPrefabInstanceRoot(component.gameObject);
        if (root == null) return false;
        if (!modifications.TryGetValue(root, out var modified))
        {
            modified = (PrefabUtility.GetPropertyModifications(root) ?? Array.Empty<PropertyModification>())
                .Where(mod => mod.target != null && IsLegacyProperty(mod.propertyPath))
                .Select(mod => mod.target!)
                .ToHashSet();
            modifications[root] = modified;
        }
        for (Object? source = component; source != null;
             source = PrefabUtility.GetCorrespondingObjectFromSource(source))
            if (modified.Contains(source)) return true;
        return false;
    }

    private static bool IsLegacyProperty(string? path)
        => path != null && (path.StartsWith("FacialBlendShapes", StringComparison.Ordinal)
            || path.StartsWith("NonFacialAnimations", StringComparison.Ordinal)
            || path.StartsWith("EyeBlinkReference", StringComparison.Ordinal)
            || path.StartsWith("LipSyncReference", StringComparison.Ordinal)
            || path is "HasFacialBlendShapes" or "HasEyeBlink" or "HasLipSync");

    private static bool SourceChainContains(Object component, Object source)
    {
        for (var current = component; current != null;
             current = PrefabUtility.GetCorrespondingObjectFromSource(current))
            if (current == source) return true;
        return false;
    }

    private static SettingsReference? FacialReference(FaceTuneTagComponent owner)
        => owner switch
        {
            SettingsComponent settings when settings.HasFacialBlendShapes => settings.FacialBlendShapesReference,
            ExpressionComponent expression => expression.FacialBlendShapesReference,
            ExpressionDataComponent data => data.FacialBlendShapesReference,
            _ => null
        };

    private static FaceTuneTagComponent? ReferencedFacialProvider(FaceTuneTagComponent owner)
    {
        var reference = FacialReference(owner);
        return reference?.Mode == SettingsReferenceMode.Reference
            ? Select(reference.Source, typeof(FacialBlendShapeData))
            : null;
    }

    private static FacialBlendShapeData PreserveFacialDataReference(
        FaceTuneTagComponent owner, FacialBlendShapeData value)
    {
        var source = ReferencedFacialProvider(owner);
        return source == null ? value : new FacialBlendShapeData
        {
            BaseSource = FacialBlendShapeData.SimpleBaseSource.Reference,
            ReferenceSource = source
        };
    }

    private static FacialBlendShapeData? ResolveFacial(FaceTuneTagComponent owner, HashSet<Component> path,
        Dictionary<FaceTuneTagComponent, FacialBlendShapeData?> cache)
    {
        if (cache.TryGetValue(owner, out var cached)) return cached;
        if (!path.Add(owner)) return null;
        try
        {
            var reference = FacialReference(owner);
            if (reference == null) return cache[owner] = null;
            if (reference.Mode == SettingsReferenceMode.Reference)
            {
                var source = Select(reference.Source, typeof(FacialBlendShapeData));
                return cache[owner] = source == null ? null : ResolveFacial(source, path, cache);
            }
            var value = owner switch
            {
                SettingsComponent settings => settings.FacialBlendShapes,
                ExpressionComponent expression => expression.FacialBlendShapes,
                ExpressionDataComponent data => data.FacialBlendShapes,
                _ => throw new ArgumentOutOfRangeException(nameof(owner))
            };
            return cache[owner] = new FacialBlendShapeData
            {
                BlendShapeMode = FacialBlendShapeData.Mode.Simple,
                BaseSource = FacialBlendShapeData.SimpleBaseSource.Clip,
                Clip = value.Clip,
                ClipOption = value.ClipOption,
                BlendShapeAnimations = value.BlendShapeAnimations.ToList()
            };
        }
        finally { path.Remove(owner); }
    }

    private static NonFacialAnimationData? ResolveNonFacial(FaceTuneTagComponent owner, HashSet<Component> path,
        Dictionary<FaceTuneTagComponent, NonFacialAnimationData?> cache)
    {
        if (cache.TryGetValue(owner, out var cached)) return cached;
        if (!path.Add(owner)) return null;
        try
        {
            var reference = owner switch
            {
                ExpressionComponent expression => expression.NonFacialAnimationsReference,
                ExpressionDataComponent data => data.NonFacialAnimationsReference,
                _ => null
            };
            if (reference == null) return cache[owner] = null;
            if (reference.Mode == SettingsReferenceMode.Reference)
            {
                var source = Select(reference.Source, typeof(NonFacialAnimationData));
                return cache[owner] = source == null ? null : ResolveNonFacial(source, path, cache);
            }
            return cache[owner] = CopyNonFacial(owner switch
            {
                ExpressionComponent expression => expression.NonFacialAnimations,
                ExpressionDataComponent data => data.NonFacialAnimations,
                _ => null
            });
        }
        finally { path.Remove(owner); }
    }

    private static void AddFacial(List<FacialBlendShapeData.CompositeEntry> entries,
        FaceTuneTagComponent owner, FacialBlendShapeData? value)
    {
        if (value == null) return;
        if (ReferencedFacialProvider(owner) is { } source)
        {
            if (value.Clip != null || value.BlendShapeAnimations.Count > 0)
                entries.Add(new FacialBlendShapeData.CompositeEntry
                {
                    EntryKind = FacialBlendShapeData.CompositeEntry.Kind.Reference,
                    ReferenceSource = source
                });
            return;
        }
        if (value.Clip != null)
            entries.Add(new FacialBlendShapeData.CompositeEntry
            {
                EntryKind = FacialBlendShapeData.CompositeEntry.Kind.Clip,
                Clip = value.Clip,
                ClipOption = value.ClipOption
            });
        if (value.BlendShapeAnimations.Count > 0)
            entries.Add(new FacialBlendShapeData.CompositeEntry
            {
                EntryKind = FacialBlendShapeData.CompositeEntry.Kind.Direct,
                BlendShapeAnimations = value.BlendShapeAnimations.ToList()
            });
    }

    private static void AddChildFacial(List<FacialBlendShapeData.CompositeEntry> entries,
        ExpressionDataComponent child, FacialBlendShapeData? value)
    {
        if (value == null || (value.Clip == null && value.BlendShapeAnimations.Count == 0)) return;
        entries.Add(new FacialBlendShapeData.CompositeEntry
        {
            EntryKind = FacialBlendShapeData.CompositeEntry.Kind.Reference,
            ReferenceSource = child
        });
    }

    private static NonFacialAnimationData PreserveNonFacialDataReference(
        FaceTuneTagComponent owner, NonFacialAnimationData value)
    {
        var reference = owner switch
        {
            ExpressionComponent expression => expression.NonFacialAnimationsReference,
            ExpressionDataComponent data => data.NonFacialAnimationsReference,
            _ => null
        };
        var source = reference?.Mode == SettingsReferenceMode.Reference
            ? Select(reference.Source, typeof(NonFacialAnimationData))
            : null;
        return source == null ? CopyNonFacial(value) : new NonFacialAnimationData
        {
            ComponentReferences = new List<FaceTuneTagComponent> { source }
        };
    }

    private static NonFacialAnimationData CopyNonFacial(NonFacialAnimationData? value)
        => value == null ? new NonFacialAnimationData() : new NonFacialAnimationData
        {
            ComponentReferences = (value.ReferenceAnimations ?? new List<Transform>())
                .Select(reference => Select(reference, typeof(NonFacialAnimationData)))
                .OfType<FaceTuneTagComponent>()
                .Concat(value.ComponentReferences ?? new List<FaceTuneTagComponent>())
                .ToList(),
            AnimationClips = value.AnimationClips.ToList(),
            TransformAnimations = value.TransformAnimations.ToList()
        };

    private static FaceTuneTagComponent? ResolveReference(FaceTuneTagComponent component, Type kind)
    {
        var reference = component switch
        {
            SettingsComponent settings when kind == typeof(EyeBlinkSettings) => settings.EyeBlinkReference,
            SettingsComponent settings => settings.LipSyncReference,
            ExpressionComponent expression when kind == typeof(EyeBlinkSettings) => expression.EyeBlinkReference,
            ExpressionComponent expression => expression.LipSyncReference,
            _ => null
        };
        return reference?.Mode == SettingsReferenceMode.Reference ? Select(reference.Source, kind) : null;
    }

    private static void ApplyReference(SettingsReference reference, FaceTuneTagComponent? source)
    {
        if (reference.Mode == SettingsReferenceMode.Reference)
            reference.ComponentSource = source;
    }

    // The old resolver selected the last ENABLED provider on the Transform's GameObject.
    private static FaceTuneTagComponent? Select(Transform? source, Type kind)
    {
        if (source == null) return null;
        FaceTuneTagComponent? selected = null;
        foreach (var candidate in source.GetComponents<FaceTuneTagComponent>())
        {
            var enabled = kind == typeof(FacialBlendShapeData) ? candidate switch
            {
                SettingsComponent settings => settings.HasFacialBlendShapes,
                ExpressionComponent or ExpressionDataComponent => true,
                _ => false
            } : kind == typeof(NonFacialAnimationData) ? candidate is ExpressionComponent or ExpressionDataComponent
                : kind == typeof(EyeBlinkSettings) ? candidate switch
                {
                    SettingsComponent settings => settings.HasEyeBlink,
                    ExpressionComponent expression => expression.HasEyeBlink,
                    _ => false
                } : candidate switch
                {
                    SettingsComponent settings => settings.HasLipSync,
                    ExpressionComponent expression => expression.HasLipSync,
                    _ => false
                };
            if (enabled) selected = candidate;
        }
        return selected;
    }

    private static void Mark(FaceTuneTagComponent component)
    {
        EditorUtility.SetDirty(component);
        if (PrefabUtility.IsPartOfPrefabInstance(component))
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
    }
}
