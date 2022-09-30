// Original: https://github.com/zymlabs/nswag-fluentvalidation
// MIT License
// Copyright (c) 2019 Zym Labs LLC
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
//         of this software and associated documentation files (the "Software"), to deal
//         in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
//         furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
//         copies or substantial portions of the Software.
//
//         THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//         IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//                                                                 FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//         AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//         LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace FastEndpoints.Swagger.ValidationProcessor.Extensions;

internal static class StringExtensions
{
    /// <summary>
    /// Converts string to lowerCamelCase.
    /// </summary>
    /// <param name="inputString">Input string.</param>
    /// <returns>lowerCamelCase string.</returns>
    internal static string? ToLowerCamelCase(this string? inputString)
    {
        return inputString switch
        {
            null => null,
            "" => string.Empty,
            _ => char.IsLower(inputString[0]) ? inputString : inputString[..1].ToLower() + inputString[1..],
        };
    }

    /// <summary>
    /// Returns string equality only by symbols ignore case.
    /// It can be used for comparing camelCase, PascalCase, snake_case, kebab-case identifiers.
    /// </summary>
    /// <param name="left">Left string to compare.</param>
    /// <param name="right">Right string to compare.</param>
    /// <returns><c>true</c> if input strings are equals in terms of identifier formatting.</returns>
    internal static bool EqualsIgnoreAll(this string left, string right) => IgnoreAllStringComparer.Instance.Equals(left, right);
}

/// <summary>
/// Returns string equality only by symbols ignore case.
/// It can be used for comparing camelCase, PascalCase, snake_case, kebab-case identifiers.
/// </summary>
internal class IgnoreAllStringComparer : StringComparer
{
    /// <summary>
    /// Instance of StringComparer
    /// </summary>
    public static readonly StringComparer Instance = new IgnoreAllStringComparer();

    /// <inheritdoc />
    public override int Compare(string? left, string? right)
    {
        var leftIndex = 0;
        var rightIndex = 0;
        int compare;
        do
        {
            GetNextSymbol(left, ref leftIndex, out var leftSymbol);
            GetNextSymbol(right, ref rightIndex, out var rightSymbol);

            compare = leftSymbol.CompareTo(rightSymbol);
        }
        while (compare == 0 && leftIndex >= 0 && rightIndex >= 0);

        return compare;
    }

    /// <inheritdoc />
    public override bool Equals(string? left, string? right)
    {
        if (left == null || right == null)
            return false;

        var leftIndex = 0;
        var rightIndex = 0;
        bool equals;

        while (true)
        {
            var hasLeftSymbol = GetNextSymbol(left, ref leftIndex, out var leftSymbol);
            var hasRightSymbol = GetNextSymbol(right, ref rightIndex, out var rightSymbol);

            equals = leftSymbol == rightSymbol;
            if (!equals || !hasLeftSymbol || !hasRightSymbol)
                break;
        }

        return equals;
    }

    /// <inheritdoc />
    public override int GetHashCode(string obj)
    {
        unchecked
        {
            var index = 0;
            var hash = 0;
            while (GetNextSymbol(obj, ref index, out var symbol))
                hash = (31 * hash) + char.ToUpperInvariant(symbol).GetHashCode();

            return hash;
        }
    }

    internal static bool GetNextSymbol(string? value, ref int startIndex, out char symbol)
    {
        while (startIndex >= 0 && startIndex < value?.Length)
        {
            var current = value[startIndex++];
            if (char.IsLetterOrDigit(current))
            {
                symbol = char.ToUpperInvariant(current);
                return true;
            }
        }

        startIndex = -1;
        symbol = char.MinValue;
        return false;
    }
}