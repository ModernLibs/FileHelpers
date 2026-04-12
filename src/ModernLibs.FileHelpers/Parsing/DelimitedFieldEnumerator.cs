// MA0051: DelimitedFieldEnumerator contains MoveNextQuoted which exceeds 60 lines due to
// RFC 4180 quoted-field unescaping logic (fast path, no-escape path, escape path, lenient path).
// All paths are part of a single coherent RFC 4180 parsing concern — extraction justified.
#pragma warning disable MA0051

namespace ModernLibs.FileHelpers;

/// <summary>
/// Foreach-compatible ref struct enumerator that splits a single record line into fields
/// by delimiter. Implements RFC 4180 single-line quoted field handling (D-02) with lenient
/// behavior for malformed input (D-03).
/// </summary>
/// <remarks>
/// Loop invariant: each MoveNext call either sets _atEnd = true or advances _remaining by
/// at least _delimiter.Length + 1 chars, preventing infinite loops (T-05-05).
/// </remarks>
public ref struct DelimitedFieldEnumerator
{
    private ReadOnlySpan<char> _remaining;
    private ReadOnlySpan<char> _current;
    private readonly ReadOnlySpan<char> _delimiter;
    private bool _atEnd;

    /// <summary>
    /// Initializes a new <see cref="DelimitedFieldEnumerator"/> over the given line span.
    /// </summary>
    /// <param name="line">The record line to split (must not include line terminator).</param>
    /// <param name="delimiter">The field delimiter (single- or multi-char).</param>
    public DelimitedFieldEnumerator(ReadOnlySpan<char> line, ReadOnlySpan<char> delimiter)
    {
        _remaining = line;
        _delimiter = delimiter;
        _current = default;
        _atEnd = false;
    }

    /// <summary>Returns this enumerator to support the foreach duck-type pattern.</summary>
    public DelimitedFieldEnumerator GetEnumerator() => this;

    /// <summary>The current field span. Valid until the next <see cref="MoveNext"/> call.</summary>
    public ReadOnlySpan<char> Current => _current;

    /// <summary>
    /// Advances to the next field. Returns <see langword="true"/> if a field was yielded;
    /// <see langword="false"/> when all fields have been emitted.
    /// </summary>
    public bool MoveNext()
    {
        // Once _atEnd is set, we are done. Handles trailing empty field case.
        if (_atEnd)
            return false;

        var span = _remaining;

        // Empty remaining: emit one final (possibly empty) field and set done.
        if (span.IsEmpty)
        {
            _current = default;
            _atEnd = true;
            return true;
        }

        // Fast path: field does not start with a quote.
        if (span[0] != '"')
        {
            int delimIdx = _delimiter.Length == 1
                ? span.IndexOf(_delimiter[0])
                : span.IndexOf(_delimiter, StringComparison.Ordinal);

            if (delimIdx < 0)
            {
                _current = span;
                _remaining = default;
                _atEnd = true;
            }
            else
            {
                _current = span[..delimIdx];
                _remaining = span[(delimIdx + _delimiter.Length)..];
            }
            return true;
        }

        // Quoted path (D-02, D-03): field starts with '"'.
        return MoveNextQuoted(span);
    }

    /// <summary>
    /// Handles RFC 4180 quoted field parsing: fast no-escape path and full "" unescape path.
    /// Lenient: never throws on malformed/unclosed quoted fields (D-03).
    /// </summary>
    private bool MoveNextQuoted(ReadOnlySpan<char> span)
    {
        // span[0] == '"' guaranteed by caller.
        var inner = span[1..]; // skip opening quote

        // Find the first '"' inside the quoted content.
        int firstQuote = inner.IndexOf('"');
        if (firstQuote < 0)
        {
            // Lenient: no closing quote found (D-03) — treat entire remainder as the field.
            _current = inner;
            _remaining = default;
            _atEnd = true;
            return true;
        }

        // Check if it's a simple closing quote (no "" escape at this position).
        bool hasEscapes = firstQuote + 1 < inner.Length && inner[firstQuote + 1] == '"';

        if (!hasEscapes)
        {
            // No escaped quotes — slice directly to the closing quote (zero-alloc fast path).
            _current = inner[..firstQuote];
            var afterClose = inner[(firstQuote + 1)..]; // skip closing quote

            // Skip delimiter after closing quote (RFC 4180: quoted field ends before delimiter).
            if (!afterClose.IsEmpty)
            {
                int delimIdx = _delimiter.Length == 1
                    ? afterClose.IndexOf(_delimiter[0])
                    : afterClose.IndexOf(_delimiter, StringComparison.Ordinal);
                if (delimIdx == 0)
                {
                    _remaining = afterClose[_delimiter.Length..];
                }
                else if (delimIdx < 0)
                {
                    _remaining = default;
                    _atEnd = true;
                }
                else
                {
                    // Lenient: content between closing quote and delimiter — advance past delimiter.
                    _remaining = afterClose[(delimIdx + _delimiter.Length)..];
                }
            }
            else
            {
                _remaining = default;
                _atEnd = true;
            }
            return true;
        }

        // Has "" escape sequences — must build unescaped copy.
        // Always use heap for escape buffer (rare path; string fields allocate anyway per T-05-08).
        char[] escapeBuf = new char[inner.Length];
        Span<char> buf = escapeBuf.AsSpan();

        int writePos = 0;
        var remaining = inner;
        bool foundClose = false;

        while (!remaining.IsEmpty)
        {
            int q = remaining.IndexOf('"');
            if (q < 0)
            {
                // Lenient: no closing quote (D-03) — copy rest as-is.
                remaining.CopyTo(buf[writePos..]);
                writePos += remaining.Length;
                remaining = default;
                foundClose = false;
                break;
            }

            // Copy up to the quote.
            remaining[..q].CopyTo(buf[writePos..]);
            writePos += q;
            remaining = remaining[(q + 1)..]; // skip the '"'

            if (!remaining.IsEmpty && remaining[0] == '"')
            {
                // "" escape sequence — unescape to single '"'.
                buf[writePos++] = '"';
                remaining = remaining[1..];
            }
            else
            {
                // Closing quote found.
                foundClose = true;
                break;
            }
        }

        _current = buf[..writePos];

        if (foundClose)
        {
            // Skip delimiter after closing quote.
            if (!remaining.IsEmpty)
            {
                int delimIdx = _delimiter.Length == 1
                    ? remaining.IndexOf(_delimiter[0])
                    : remaining.IndexOf(_delimiter, StringComparison.Ordinal);
                if (delimIdx == 0)
                {
                    _remaining = remaining[_delimiter.Length..];
                }
                else if (delimIdx < 0)
                {
                    _remaining = default;
                    _atEnd = true;
                }
                else
                {
                    _remaining = remaining[(delimIdx + _delimiter.Length)..];
                }
            }
            else
            {
                _remaining = default;
                _atEnd = true;
            }
        }
        else
        {
            _remaining = default;
            _atEnd = true;
        }
        return true;
    }
}

#pragma warning restore MA0051
