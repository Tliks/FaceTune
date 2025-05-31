using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace com.aoyon.facetune;

internal static class LinqExtensions
{
    public static TSource? FirstOrNull<TSource>(this IEnumerable<TSource> source)
    {
        try
        {
            return source.First();
        }
        catch
        {
            // nullを返す
            // return nullだと以下のエラー
            // Null 非許容の値型である可能性があるため、Null を型パラメーター 'TSource' に変換できません。
            // Nullable<T>と参照のnullを区別する必要があるとか多分そんな感じ
            // これは値型か参照型か定まらないGenericで返り値をT?にする場合の問題
            // ref1 https://www.reddit.com/r/csharp/comments/w7w13s/return_null_from_a_generic_function/
            // ref2 https://ufcpp.net/study/csharp/sp2_nullable.html
            return default(TSource?);
        }
    }

    public static TSource? FirstOrNull<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
    {
        try
        {
            return source.First(predicate);
        }
        catch
        {
            return default(TSource?);
        }
    }

    public static TSource? LastOrNull<TSource>(this IEnumerable<TSource> source)
    {
        try
        {
            return source.Last();
        }
        catch
        {
            return default(TSource?);
        }
    }
    
    public static TSource? LastOrNull<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
    {
        try
        {
            return source.Last(predicate);
        }
        catch
        {
            return default(TSource?);
        }
    }

    public static bool TryGetFirst<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate, [NotNullWhen(true)] out TSource? result)
    {
        try
        {
            result = source.First(predicate)!;
            return true;
        }
        catch
        {
            result = default(TSource?);
            return false;
        }
    }
}