// MA0048: the checking descriptors are grouped in one file.
#pragma warning disable MA0048

using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace ModernLibs.FileHelpers.Tests.Engines.Fixtures;

/// <summary>
/// A tokenized descriptor around any descriptor: <see cref="TryParse"/> checks the tokens the runtime passes
/// against <see cref="string.Split(char)"/> of the record (field count, the text and the offset of each value),
/// throws <see cref="InvalidOperationException"/> (<c>token mismatch ...</c>) on a difference, then parses with
/// the inner descriptor. Every other member forwards to the inner descriptor.
/// </summary>
/// <param name="inner">The descriptor that parses.</param>
/// <param name="delimiter">The record delimiter of <paramref name="inner"/>.</param>
/// <param name="checkValues"><see langword="false"/> skips the split check (records with quoted fields: T20).</param>
internal class TokenCheckingDescriptor<T>(IRecordDescriptor<T> inner, char delimiter, bool checkValues = true) : ITokenizedDescriptor<T>
{
    private int _tryParseCalls;
    private int _parseCalls;

    /// <summary>Calls of <see cref="TryParse"/> (thread-safe: parallel reads share one descriptor).</summary>
    public int TryParseCalls => Volatile.Read(ref _tryParseCalls);

    /// <summary>Calls of the legacy <see cref="Parse(ReadOnlySpan{char}, int)"/> by the runtime.</summary>
    public int ParseCalls => Volatile.Read(ref _parseCalls);

    public ReadOnlySpan<FieldDescriptor> Fields => inner.Fields;

    public RecordOptions Options => inner.Options;

    public T Parse(ReadOnlySpan<char> lines, int lineCount)
    {
        Interlocked.Increment(ref _parseCalls);
        return inner.Parse(lines, lineCount);
    }

    public void Format(T record, IBufferWriter<char> writer) => inner.Format(record, writer);

    public T CreateDefaultInstance() => inner.CreateDefaultInstance();

    public bool TryParse(ReadOnlySpan<char> record, in RecordTokens tokens, [MaybeNullWhen(false)] out T result)
    {
        Interlocked.Increment(ref _tryParseCalls);
        Check(record, tokens);
        result = inner.Parse(record, 1);
        return true;
    }

    /// <summary>Throws <see cref="InvalidOperationException"/> when the tokens differ from the split of the record.</summary>
    protected void Check(ReadOnlySpan<char> record, in RecordTokens tokens)
    {
        if (!checkValues)
            return;

        string text = record.ToString();
        string[] parts = text.Split(delimiter);
        if (tokens.Count != parts.Length)
            throw new InvalidOperationException($"token mismatch in '{text}': Count {tokens.Count}, split {parts.Length}");

        int offset = 0;
        for (int i = 0; i < parts.Length; i++)
        {
            ReadOnlySpan<char> value = tokens.GetValue(i);
            int column = RecordParsing.Column(record, value);
            if (!value.SequenceEqual(parts[i]) || column != offset + 1 || tokens.IsQuoted(i))
            {
                throw new InvalidOperationException(
                    $"token mismatch in '{text}': field {i} is '{value}' at column {column}, split '{parts[i]}' at column {offset + 1}");
            }

            offset += parts[i].Length + 1;
        }

        if (!tokens.GetValue(parts.Length).IsEmpty || tokens.IsQuoted(parts.Length))
            throw new InvalidOperationException($"token mismatch in '{text}': a value after the last field");
    }
}

/// <summary>
/// <see cref="TokenCheckingDescriptor{T}"/> for a descriptor that parses into an existing instance:
/// <see cref="TryParseInto"/> checks the tokens the same way, then calls the inner <c>ParseInto</c>.
/// </summary>
internal sealed class RecyclableTokenCheckingDescriptor<T>(IRecyclableDescriptor<T> inner, char delimiter, bool checkValues = true)
    : TokenCheckingDescriptor<T>(inner, delimiter, checkValues), IRecyclableDescriptor<T>, ITokenizedDescriptor<T>
{
    private readonly IRecyclableDescriptor<T> _inner = inner;
    private int _tryParseIntoCalls;

    public int TryParseIntoCalls => Volatile.Read(ref _tryParseIntoCalls);

    public void ParseInto(ReadOnlySpan<char> lines, int lineCount, T existing) => _inner.ParseInto(lines, lineCount, existing);

    public bool TryParseInto(ReadOnlySpan<char> record, in RecordTokens tokens, T existing)
    {
        Interlocked.Increment(ref _tryParseIntoCalls);
        Check(record, tokens);
        _inner.ParseInto(record, 1, existing);
        return true;
    }
}

/// <summary>
/// A recyclable tokenized descriptor that keeps the default <c>TryParseInto</c> (not handled): every parse into
/// an existing instance must fall back to the legacy <c>ParseInto</c>, which it counts.
/// </summary>
internal sealed class NoTryParseIntoDescriptor<T>(IRecyclableDescriptor<T> inner, char delimiter)
    : TokenCheckingDescriptor<T>(inner, delimiter), IRecyclableDescriptor<T>
{
    private readonly IRecyclableDescriptor<T> _inner = inner;
    private int _parseIntoCalls;

    public int ParseIntoCalls => Volatile.Read(ref _parseIntoCalls);

    public void ParseInto(ReadOnlySpan<char> lines, int lineCount, T existing)
    {
        Interlocked.Increment(ref _parseIntoCalls);
        _inner.ParseInto(lines, lineCount, existing);
    }
}

/// <summary>Builds the checking descriptor that fits the registered descriptor of <typeparamref name="T"/>.</summary>
internal static class TokenChecking
{
    public static TokenCheckingDescriptor<T> For<T>(char delimiter, bool checkValues = true)
    {
        var inner = FileHelperRegistry<T>.Descriptor;
        return inner is IRecyclableDescriptor<T> recyclable
            ? new RecyclableTokenCheckingDescriptor<T>(recyclable, delimiter, checkValues)
            : new TokenCheckingDescriptor<T>(inner, delimiter, checkValues);
    }
}
