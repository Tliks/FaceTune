using System;
using System.Collections.Generic;
using System.Linq;

namespace Aoyon.FaceTune;

internal sealed class DebugConditionVisitor : IConditionVisitor
{
    private readonly Stack<int> _clauseCountStack = new();
    private readonly int _warnThreshold;
    private int _depth;
    private readonly Action<string> _log;

    public DebugConditionVisitor(int warnThreshold = 1000, Action<string>? logger = null)
    {
        _warnThreshold = warnThreshold;
        _log = logger ?? (msg => UnityEngine.Debug.Log(msg));
    }

    private void Log(string message)
    {
        if (_depth <= 0)
        {
            _log(message);
            return;
        }
        _log(new string(' ', _depth * 2) + message);
    }

    public static void Analyze(ICondition condition, int warnThreshold = 1000, Action<string>? logger = null)
    {
        var v = new DebugConditionVisitor(warnThreshold, logger);
        condition.Accept(v);
        var total = v._clauseCountStack.Count > 0 ? v._clauseCountStack.Peek() : 0;
        v.Log($"Total Clauses: {total}");
    }

    public static int CountClauses(ICondition condition)
    {
        // ログなしで節数のみ計算
        var v = new DebugConditionVisitor(int.MaxValue, _ => { });
        condition.Accept(v);
        return v._clauseCountStack.Count > 0 ? v._clauseCountStack.Peek() : 0;
    }

    public void Visit(FloatCondition condition)
    {
        Log($"Float: {condition.ParameterName} {condition.ComparisonType} {condition.Value}");
        _clauseCountStack.Push(1);
    }

    public void Visit(IntCondition condition)
    {
        Log($"Int: {condition.ParameterName} {condition.ComparisonType} {condition.Value}");
        _clauseCountStack.Push(1);
    }

    public void Visit(BoolCondition condition)
    {
        Log($"Bool: {condition.ParameterName} == {condition.Value}");
        _clauseCountStack.Push(1);
    }

    public void Visit(TrueCondition condition)
    {
        Log("True");
        _clauseCountStack.Push(1);
    }

    public void Visit(FalseCondition condition)
    {
        Log("False");
        _clauseCountStack.Push(1);
    }

    public void Visit(OrCondition condition)
    {
        Log($"OR: {condition.Conditions.Count} children");
        _depth++;
        var childCounts = new List<int>(condition.Conditions.Count);
        foreach (var child in condition.Conditions)
        {
            child.Accept(this);
            childCounts.Add(_clauseCountStack.Pop());
        }
        _depth--;
        var sum = 0;
        foreach (var c in childCounts) sum += c;
        Log($"OR clauses = {string.Join(" + ", childCounts)} = {sum}");
        _clauseCountStack.Push(sum);
    }

    public void Visit(AndCondition condition)
    {
        Log($"AND: {condition.Conditions.Count} children");
        _depth++;
        var childCounts = new List<int>(condition.Conditions.Count);
        foreach (var child in condition.Conditions)
        {
            child.Accept(this);
            childCounts.Add(_clauseCountStack.Pop());
        }
        _depth--;
        long product = 1;
        foreach (var c in childCounts)
        {
            if (c == 0)
            {
                product = 0;
                break;
            }
            product *= c;
            if (product > _warnThreshold)
            {
                Log($"WARN: AND product exceeded threshold: {product} > {_warnThreshold} [{string.Join(" * ", childCounts)}]");
            }
        }
        Log($"AND clauses = {string.Join(" * ", childCounts)} = {product}");
        _clauseCountStack.Push((int)Math.Min(product, int.MaxValue));
    }

    public void Visit(AndClause condition)
    {
        Log($"AndClause size: {condition.Conditions.Count}");
        _clauseCountStack.Push(1);
    }

    public void Visit(NormalizedCondition condition)
    {
        Log($"Normalized: clauses = {condition.Clauses.Count}");
        _clauseCountStack.Push(condition.Clauses.Count);
    }
}


