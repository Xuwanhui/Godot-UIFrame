using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Godot.Collections;

namespace GodotUIFrame.addons.uiframe_editor_autobind;

internal static class UIFrameEditorBindingUtility
{
    internal record struct BindingMatch(Node UiBase, String MemberName, Node CurrentNode, Node TargetNode);
    private static readonly System.Collections.Generic.Dictionary<string, BindingMatch> _matches = new();

    internal static IReadOnlyList<BindingMatch> CollectBindings(Node uiBase)
    {
        _matches.Clear();
        foreach (Node child in Enumerate(uiBase))
        {
            CollectFields(child);
        }
        return CollectProperties(uiBase);
    }

    private static void CollectFields(Node child)
    {
        string name = child.Name;
        if (string.IsNullOrEmpty(name) || !name.StartsWith('-'))
        {
            return;
        }
        BindingMatch match = new BindingMatch(null, "", null, child);
        string key = name.TrimStart('-').ToLowerInvariant();
        _matches.Add(key, match);
    }

    private static List<BindingMatch> CollectProperties(Node uiBase)
    {
        Godot.Script script = uiBase.GetScript().As<Godot.Script>();
        Array<Dictionary> propertyList = script.GetScriptPropertyList();
        foreach (Dictionary property in propertyList)
        {
            string propertyName = property["name"].AsStringName().ToString();
            if (_matches.TryGetValue(propertyName.ToLowerInvariant(), out var match))
            {
                Variant current = uiBase.Get(propertyName);
                match.UiBase = uiBase;
                match.MemberName = propertyName;
                match.CurrentNode = current.VariantType == Variant.Type.Nil ? null : current.AsGodotObject() as Node;
                _matches[propertyName.ToLowerInvariant()] = match;
            }
        }
        return _matches.Values.ToList();
    }

    private static IEnumerable<Node> Enumerate(Node root)
    {
        yield return root;

        foreach (Node child in root.GetChildren())
        {
            foreach (var item in Enumerate(child))
            {
                yield return item;
            }
        }
    }
}