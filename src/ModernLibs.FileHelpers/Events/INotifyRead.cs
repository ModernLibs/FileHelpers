// CA1711: Name kept for FileHelpers migration compatibility.
#pragma warning disable CA1711
// CA1000: Generic interface is intentional for type-safe record notifications.
#pragma warning disable CA1000

namespace ModernLibs.FileHelpers;

/// <summary>
/// INTF-01, INTF-02: Interface for record types that want to receive read notifications.
/// When a record type T implements this interface, the engine will call BeforeRead/AfterRead
/// methods for each record, alongside the engine-level BeforeReadRecord/AfterReadRecord events.
///
/// <para>
/// Both interface methods and engine events fire for each record. The interface methods
/// receive the same <see cref="BeforeReadEventArgs{T}"/> and <see cref="AfterReadEventArgs{T}"/>
/// instances as the engine events, allowing the record to modify behavior via
/// <see cref="BeforeReadEventArgs{T}.SkipThisRecord"/>,
/// <see cref="BeforeReadEventArgs{T}.StopReading"/>, or
/// <see cref="AfterReadEventArgs{T}.SkipThisRecord"/>.
/// </para>
///
/// <para>
/// This interface provides an alternative to subscribing to engine events, allowing
/// record types to encapsulate their own validation, transformation, and filtering logic.
/// </para>
///
/// <para>
/// For write operations, both <see cref="INotifyWrite{T}.BeforeWrite"/> and
/// <see cref="INotifyWrite{T}.AfterWrite"/> are fully supported.
/// </para>
/// </summary>
/// <typeparam name="T">The record type implementing this interface.</typeparam>
public interface INotifyRead<T>
{
    /// <summary>
    /// INTF-01: Called before a line is parsed into a record. Allows modification of the raw line,
    /// skipping the line, or stopping the read operation entirely.
    /// </summary>
    /// <param name="e">Event args containing the raw line, line number, and control flags.</param>
    /// <remarks>
    /// <para>
    /// Setting <see cref="BeforeReadEventArgs{T}.SkipThisRecord"/> to true excludes this line from parsing.
    /// Setting <see cref="BeforeReadEventArgs{T}.StopReading"/> to true terminates the read operation.
    /// Modifying <see cref="BeforeReadEventArgs{T}.RecordLine"/> changes the content that will be parsed.
    /// </para>
    /// <para>
    /// <strong>Note:</strong> The record instance passed to this method is a default instance
    /// (<c>default(T)</c> for structs, <c>new T()</c> for classes) created before parsing.
    /// Field values are not yet populated.
    /// </para>
    /// </remarks>
    void BeforeRead(BeforeReadEventArgs<T> e);

    /// <summary>
    /// INTF-02: Called after a line is successfully parsed into a record. Allows filtering
    /// the parsed record from the results.
    /// </summary>
    /// <param name="e">Event args containing the parsed record, raw line, and control flags.</param>
    /// <remarks>
    /// Setting <see cref="AfterReadEventArgs{T}.SkipThisRecord"/> to true excludes this record
    /// from the results returned by the read operation.
    /// </remarks>
    void AfterRead(AfterReadEventArgs<T> e);
}
