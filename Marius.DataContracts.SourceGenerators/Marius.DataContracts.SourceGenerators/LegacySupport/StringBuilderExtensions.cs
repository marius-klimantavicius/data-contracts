using System.Text;

namespace System;

public static class StringBuilderExtensions
{
    public static unsafe StringBuilder Append(this StringBuilder sb, ReadOnlySpan<char> value)
    {
        fixed (char* ptr = value)
            return sb.Append(ptr, value.Length);
    }
}