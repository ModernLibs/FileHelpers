using System.Text;

namespace ModernLibs.FileHelpers;

/// <summary>Builds the <c>GetFileHeader()</c> text (FileHelpers 3.x compatible) from a record descriptor.</summary>
internal static class FileHeaderBuilder
{
    /// <summary>
    /// Joins the caption (<c>[FieldCaption]</c>, else the member name) of every descriptor field with one
    /// separator: the delimiter of the first field for a delimited record, a tab for a fixed-length record.
    /// No quoting and no line terminator.
    /// </summary>
    internal static string Build<T>(IRecordDescriptor<T> descriptor)
    {
        ReadOnlySpan<FieldDescriptor> fields = descriptor.Fields;
        if (fields.Length == 0)
        {
            return string.Empty;
        }

        string recordDelimiter = descriptor.Options.Delimiter ?? string.Empty;
        string separator = recordDelimiter.Length == 0
            ? "\t"
            : fields[0].Delimiter ?? recordDelimiter;

        var sb = new StringBuilder();
        for (int i = 0; i < fields.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(separator);
            }

            sb.Append(fields[i].Caption ?? fields[i].Name);
        }

        return sb.ToString();
    }
}
