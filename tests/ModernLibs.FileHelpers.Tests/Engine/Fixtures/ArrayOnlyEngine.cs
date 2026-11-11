// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// CA1822, CA1062: Test double; members are stubs.
#pragma warning disable CA1822, CA1062
// CS0067: Events of the test double are never raised.
#pragma warning disable CS0067

using System.Text;

namespace ModernLibs.FileHelpers.Tests.Engine.Fixtures;

/// <summary>
/// Foreign <see cref="IFileHelperEngine{T}"/> implementation with only the pre-4.10 members, used to prove that
/// members added to the interface later have working default implementations. <see cref="ReadString(string)"/>
/// returns two fixed records; <see cref="ReadFile(string, int)"/> returns <c>maxRecords</c> of them (at most two).
/// Everything else throws <see cref="NotSupportedException"/>.
/// </summary>
internal sealed class ArrayOnlyEngine : IFileHelperEngine<CsvOrder>
{
    public string? HeaderText { get; set; }

    public string? FooterText { get; set; }

    public Encoding Encoding { get; set; } = Encoding.UTF8;

    public string NewLineForWrite { get; set; } = "\r\n";

    public ErrorMode ErrorMode { get; set; }

    public ErrorManager ErrorManager { get; } = new();

    public int LineNumber => 0;

    public int TotalRecords => 0;

    public bool RecycleRecords { get; set; }

    public event BeforeReadHandler<CsvOrder>? BeforeReadRecord;

    public event AfterReadHandler<CsvOrder>? AfterReadRecord;

    public event BeforeWriteHandler<CsvOrder>? BeforeWriteRecord;

    public event AfterWriteHandler<CsvOrder>? AfterWriteRecord;

    public event ProgressChangeHandler? Progress;

    private static CsvOrder[] Fixed(int count) =>
        new[]
        {
            new CsvOrder { OrderId = 1, CustomerName = "a", Amount = 1.5m },
            new CsvOrder { OrderId = 2, CustomerName = "b", Amount = 2.5m },
        }[..Math.Clamp(count, 0, 2)];

    public CsvOrder[] ReadFile(string filePath) => Fixed(2);

    public CsvOrder[] ReadFile(string filePath, int maxRecords) => Fixed(maxRecords);

    public CsvOrder[] ReadString(string content) => Fixed(2);

    public CsvOrder[] ReadString(string content, int maxRecords) => Fixed(maxRecords);

    public CsvOrder[] ReadStream(TextReader reader) => Fixed(2);

    public CsvOrder[] ReadStream(TextReader reader, int maxRecords) => Fixed(maxRecords);

    public IAsyncEnumerable<CsvOrder> ReadAsync(string filePath, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public IEnumerable<CsvOrder> ReadSync(TextReader reader) => throw new NotSupportedException();

    public IEnumerable<CsvOrder> ReadSync(TextReader reader, int maxRecords) => throw new NotSupportedException();

    public IEnumerable<CsvOrder> ReadSync(string content) => throw new NotSupportedException();

    public IEnumerable<CsvOrder> ReadSync(string content, int maxRecords) => throw new NotSupportedException();

    public void WriteFile(string filePath, IEnumerable<CsvOrder> records) => throw new NotSupportedException();

    public string WriteString(IEnumerable<CsvOrder> records) => throw new NotSupportedException();

    public void WriteStream(TextWriter writer, IEnumerable<CsvOrder> records) => throw new NotSupportedException();

    public void AppendToFile(string filePath, CsvOrder record) => throw new NotSupportedException();

    public void AppendToFile(string filePath, IEnumerable<CsvOrder> records) => throw new NotSupportedException();
}
