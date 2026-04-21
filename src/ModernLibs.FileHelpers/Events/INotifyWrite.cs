// CA1711: Name kept for FileHelpers migration compatibility.
#pragma warning disable CA1711
// CA1000: Generic interface is intentional for type-safe record notifications.
#pragma warning disable CA1000

namespace ModernLibs.FileHelpers;

/// <summary>
/// INTF-03, INTF-04: Interface for record types that want to receive write notifications.
/// When a record type T implements this interface, the engine will call BeforeWrite/AfterWrite
/// methods for each record, alongside the engine-level BeforeWriteRecord/AfterWriteRecord events.
///
/// <para>
/// Both interface methods and engine events fire for each record. The interface methods
/// receive the same <see cref="BeforeWriteEventArgs{T}"/> and <see cref="AfterWriteEventArgs{T}"/>
/// instances as the engine events, allowing the record to modify behavior via
/// <see cref="BeforeWriteEventArgs{T}.SkipThisRecord"/> or
/// <see cref="AfterWriteEventArgs{T}.RecordLine"/>.
/// </para>
///
/// <para>
/// This interface provides an alternative to subscribing to engine events, allowing
/// record types to encapsulate their own validation, transformation, and post-formatting logic.
/// </para>
/// </summary>
/// <typeparam name="T">The record type implementing this interface.</typeparam>
public interface INotifyWrite<T>
{
    /// <summary>
    /// INTF-03: Called before a record is formatted and written. Allows skipping the record
    /// or performing pre-write validation.
    /// </summary>
    /// <param name="e">Event args containing the record, record number, and control flags.</param>
    /// <remarks>
    /// Setting <see cref="BeforeWriteEventArgs{T}.SkipThisRecord"/> to true excludes this record
    /// from the output without formatting it.
    /// </remarks>
    void BeforeWrite(BeforeWriteEventArgs<T> e);

    /// <summary>
    /// INTF-04: Called after a record is formatted into a line. Allows modification of the
    /// formatted output before it is written.
    /// </summary>
    /// <param name="e">Event args containing the record, formatted line, and record number.</param>
    /// <remarks>
    /// Modifying <see cref="AfterWriteEventArgs{T}.RecordLine"/> changes the content that will be
    /// written to the output. This allows post-formatting transformations, adding suffixes/prefixes,
    /// or custom encoding.
    /// </remarks>
    void AfterWrite(AfterWriteEventArgs<T> e);
}
