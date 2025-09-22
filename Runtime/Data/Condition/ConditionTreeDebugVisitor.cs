using System;
using System.Collections.Generic;
using System.Text;

namespace Aoyon.FaceTune;

internal sealed class ConditionTreeDebugVisitor : IConditionVisitor
{
    private readonly StringBuilder _sb = new StringBuilder(1024);
    private int _depth;

    private void Line(string text)
    {
        _sb.Append(' ', _depth * 2);
        _sb.AppendLine(text);
    }

    public static string Dump(ICondition condition)
    {
        var v = new ConditionTreeDebugVisitor();
        condition.Accept(v);
        return v._sb.ToString();
    }

    public void Visit(FloatCondition condition)
    {
        Line($"Float({condition.ParameterName} {condition.ComparisonType} {condition.Value})");
    }

    public void Visit(IntCondition condition)
    {
        Line($"Int({condition.ParameterName} {condition.ComparisonType} {condition.Value})");
    }

    public void Visit(BoolCondition condition)
    {
        Line($"Bool({condition.ParameterName} == {condition.Value})");
    }

    public void Visit(TrueCondition condition)
    {
        Line("True");
    }

    public void Visit(FalseCondition condition)
    {
        Line("False");
    }

    public void Visit(OrCondition condition)
    {
        Line($"OR[{condition.Conditions.Count}]  // sum of child clauses");
        _depth++;
        foreach (var child in condition.Conditions)
        {
            child.Accept(this);
        }
        _depth--;
    }

    public void Visit(AndCondition condition)
    {
        Line($"AND[{condition.Conditions.Count}] // product of child clauses");
        _depth++;
        foreach (var child in condition.Conditions)
        {
            child.Accept(this);
        }
        _depth--;
    }

    public void Visit(AndClause condition)
    {
        Line($"AndClause size={condition.Conditions.Count}");
        _depth++;
        foreach (var c in condition.Conditions)
        {
            c.Accept(this);
        }
        _depth--;
    }

    public void Visit(NormalizedCondition condition)
    {
        Line($"Normalized clauses={condition.Clauses.Count}");
        _depth++;
        foreach (var clause in condition.Clauses)
        {
            clause.Accept(this);
        }
        _depth--;
    }
}
