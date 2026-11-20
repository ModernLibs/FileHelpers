using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace ModernLibs.FileHelpers;

/// <summary>
/// Optional extension of <see cref="IRecordDescriptor{T}"/> that parses a record from field boundaries the runtime
/// has already found (<see cref="RecordTokens"/>). Infrastructure for source-generated code; not intended for
/// direct use.
/// </summary>
/// <remarks>
/// <para>The source generator implements it, from 4.10 on, for delimited records with one line per record. The
/// runtime calls it only for record types and records it can tokenize, and only when the record has between the
/// mandatory and the total number of fields; every other record, and every record for which a method returns
/// <see langword="false"/>, goes through <see cref="IRecordDescriptor{T}.Parse(ReadOnlySpan{char}, int)"/> as in
/// 4.9.</para>
/// <para>The tokens and every span they return are valid only during the call; an implementation must not store
/// them.</para>
/// </remarks>
/// <typeparam name="T">The annotated record type.</typeparam>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface ITokenizedDescriptor<T> : IRecordDescriptor<T>
{
    /// <summary>Parses a record from its tokens.</summary>
    /// <param name="record">The record text, as the runtime tokenized it.</param>
    /// <param name="tokens">The fields of <paramref name="record"/>.</param>
    /// <param name="result">The parsed record, when the method returns <see langword="true"/>.</param>
    /// <returns>
    /// <see langword="true"/> when the record was parsed. <see langword="false"/> means "not handled", never
    /// "invalid input": the runtime then parses the same record with
    /// <see cref="IRecordDescriptor{T}.Parse(ReadOnlySpan{char}, int)"/>. An implementation returns
    /// <see langword="false"/> only before it runs any converter.
    /// </returns>
    /// <exception cref="ConvertException">A field value cannot be converted to the field type; thrown as from
    /// <see cref="IRecordDescriptor{T}.Parse(ReadOnlySpan{char}, int)"/>, not reported by returning
    /// <see langword="false"/>.</exception>
    /// <exception cref="FileHelpersException">A field fails its validation (for example an empty
    /// <see cref="FieldNotEmptyAttribute"/> value), as from <see cref="IRecordDescriptor{T}.Parse(ReadOnlySpan{char}, int)"/>.</exception>
    bool TryParse(ReadOnlySpan<char> record, in RecordTokens tokens, [MaybeNullWhen(false)] out T result);

    /// <summary>
    /// Parses a record from its tokens into an existing instance, as
    /// <see cref="IRecyclableDescriptor{T}.ParseInto(ReadOnlySpan{char}, int, T)"/> does. The default implementation
    /// returns <see langword="false"/> (not handled).
    /// </summary>
    /// <param name="record">The record text, as the runtime tokenized it.</param>
    /// <param name="tokens">The fields of <paramref name="record"/>.</param>
    /// <param name="existing">The instance to overwrite in place.</param>
    /// <returns>
    /// <see langword="true"/> when every member of <paramref name="existing"/> was assigned. <see langword="false"/>
    /// means "not handled", never "invalid input": the runtime then calls
    /// <see cref="IRecyclableDescriptor{T}.ParseInto(ReadOnlySpan{char}, int, T)"/> for the same record. An
    /// implementation returns <see langword="false"/> only before it runs any converter or assigns any member.
    /// </returns>
    /// <exception cref="ConvertException">A field value cannot be converted to the field type; thrown, not reported
    /// by returning <see langword="false"/>.</exception>
    /// <exception cref="FileHelpersException">A field fails its validation, as from
    /// <see cref="IRecyclableDescriptor{T}.ParseInto(ReadOnlySpan{char}, int, T)"/>.</exception>
    bool TryParseInto(ReadOnlySpan<char> record, in RecordTokens tokens, T existing) => false;
}
