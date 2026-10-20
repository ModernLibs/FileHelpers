// ============================================================================
// ModernLibsApp - Example using ModernLibs.FileHelpers 4.x
// ============================================================================
// This file is the ModernLibs.FileHelpers version of OriginalFileHelpersApp.cs (FileHelpers 3.x).
// The differences visible in this sample are:
// 1. Namespace: ModernLibs.FileHelpers instead of FileHelpers
// 2. Generic interfaces: INotifyRead<T> / INotifyWrite<T> instead of INotifyRead / INotifyWrite
// 3. Examples 7 and 8 use features 3.x does not have (IAsyncEnumerable streaming).
// docs/MIGRATION.md lists all API differences.
//
// Compile this with: dotnet run
// ============================================================================

using ModernLibs.FileHelpers;

namespace ModernLibsApp;

// Basic delimited record - IDENTICAL to FileHelpers 3.x
[DelimitedRecord(",")]
[IgnoreFirst(1)]  // Skip header
public class Customer
{
    [FieldOrder(1)] public int CustomerId;
    [FieldOrder(2)] public string Name = string.Empty;
    [FieldOrder(3)] public string Email = string.Empty;
    [FieldOrder(4)] public decimal Balance;
}

// Fixed-length record - IDENTICAL to FileHelpers 3.x
[FixedLengthRecord]
public class Employee
{
    [FieldOrder(1)] [FieldFixedLength(8)] public int EmployeeId;
    [FieldOrder(2)] [FieldFixedLength(25)] public string Name = string.Empty;
    [FieldOrder(3)] [FieldFixedLength(12)] public decimal Salary;
}

// Record with converter - IDENTICAL to FileHelpers 3.x
[DelimitedRecord(",")]
public class Order
{
    [FieldOrder(1)] public int OrderId;
    [FieldOrder(2)] [FieldConverter(ConverterKind.Date, "yyyy-MM-dd")] public DateTime OrderDate;
    [FieldOrder(3)] [FieldConverter(ConverterKind.Decimal, ".")] public decimal Total;
}

// Record implementing INotifyRead/INotifyWrite - USES GENERIC INTERFACES
[DelimitedRecord(",")]
[IgnoreFirst(1)]
public class ValidatedCustomer : INotifyRead<ValidatedCustomer>, INotifyWrite<ValidatedCustomer>
{
    [FieldOrder(1)] public int CustomerId;
    [FieldOrder(2)] public string Name = string.Empty;
    [FieldOrder(3)] public string Email = string.Empty;
    [FieldOrder(4)] public decimal Balance;

    public void BeforeRead(BeforeReadEventArgs<ValidatedCustomer> e)
    {
        // Skip comment lines
        if (e.RecordLine.StartsWith("#"))
            e.SkipThisRecord = true;
    }

    public void AfterRead(AfterReadEventArgs<ValidatedCustomer> e)
    {
        // Skip records with negative balance
        if (e.Record.Balance < 0)
            e.SkipThisRecord = true;
    }

    public void BeforeWrite(BeforeWriteEventArgs<ValidatedCustomer> e)
    {
        // Validation before write
        if (string.IsNullOrEmpty(e.Record.Email))
            e.SkipThisRecord = true;
    }

    public void AfterWrite(AfterWriteEventArgs<ValidatedCustomer> e)
    {
        // Post-write action
    }
}

static class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== ModernLibs.FileHelpers 4.x Examples ===\n");

        // Example 1: Read CSV with header
        ReadCsvWithHeader();

        // Example 2: Write CSV
        WriteCsv();

        // Example 3: Read fixed-length file
        ReadFixedLength();

        // Example 4: Error handling
        ErrorHandling();

        // Example 5: Streaming large files
        StreamingLargeFile();

        // Example 6: Event handling
        EventHandling();

        // Example 7: Async streaming (IAsyncEnumerable)
        AsyncStreaming().Wait();

        // Example 8: Record with validation interface
        InterfaceValidation();

        Console.WriteLine("=== All examples completed successfully! ===");
    }

    static void ReadCsvWithHeader()
    {
        Console.WriteLine("1. Reading CSV with header:");

        var engine = new FileHelperEngine<Customer>();

        // Sample data - normally from file
        string csv = "CustomerId,Name,Email,Balance\r\n" +
                    "1,John Doe,john@example.com,1500.50\r\n" +
                    "2,Jane Smith,jane@example.com,2300.75\r\n";

        var customers = engine.ReadString(csv);

        Console.WriteLine($"   Header: {engine.HeaderText}");
        Console.WriteLine($"   Read {customers.Length} customers");
        foreach (var c in customers)
            Console.WriteLine($"   - {c.Name}: ${c.Balance}");
        Console.WriteLine();
    }

    static void WriteCsv()
    {
        Console.WriteLine("2. Writing CSV:");

        var engine = new FileHelperEngine<Customer>();
        engine.HeaderText = "CustomerId,Name,Email,Balance";

        var customers = new List<Customer>
        {
            new Customer { CustomerId = 1, Name = "John Doe", Email = "john@example.com", Balance = 1500.50m },
            new Customer { CustomerId = 2, Name = "Jane Smith", Email = "jane@example.com", Balance = 2300.75m }
        };

        string csv = engine.WriteString(customers);
        Console.WriteLine($"   Generated CSV:\n{csv}");
    }

    static void ReadFixedLength()
    {
        Console.WriteLine("3. Reading fixed-length file:");

        var engine = new FileHelperEngine<Employee>();

        // Fixed-width data
        string fixedData = "100     John Doe                 50000.00    \r\n" +
                          "101     Jane Smith               65000.00    ";

        var employees = engine.ReadString(fixedData);

        Console.WriteLine($"   Read {employees.Length} employees");
        foreach (var e in employees)
            Console.WriteLine($"   - {e.Name.Trim()}: ${e.Salary}");
        Console.WriteLine();
    }

    static void ErrorHandling()
    {
        Console.WriteLine("4. Error handling:");

        var engine = new FileHelperEngine<Customer>();
        engine.ErrorMode = ErrorMode.SaveAndContinue;

        // CSV with one bad record
        string csv = "CustomerId,Name,Email,Balance\r\n" +
                    "1,John Doe,john@example.com,1500.50\r\n" +
                    "BAD_DATA_HERE\r\n" +
                    "2,Jane Smith,jane@example.com,2300.75";

        var customers = engine.ReadString(csv);

        Console.WriteLine($"   Valid records: {customers.Length}");
        Console.WriteLine($"   Errors: {engine.ErrorManager.ErrorCount}");
        foreach (var err in engine.ErrorManager.Errors)
            Console.WriteLine($"   - Line {err.LineNumber}: {err.ExceptionInfo?.Message}");
        Console.WriteLine();
    }

    static void StreamingLargeFile()
    {
        Console.WriteLine("5. Streaming large file (pull model):");

        var engine = new FileHelperAsyncEngine<Customer>();

        engine.BeginReadString("CustomerId,Name,Email,Balance\r\n" +
                               "1,John Doe,john@example.com,1500.50\r\n" +
                               "2,Jane Smith,jane@example.com,2300.75");

        int count = 0;
        while (engine.ReadNext() is { } customer)
        {
            count++;
            // Process customer without loading all into memory
        }
        engine.Close();

        Console.WriteLine($"   Streamed {count} records");
        Console.WriteLine();
    }

    static void EventHandling()
    {
        Console.WriteLine("6. Event handling:");

        var engine = new FileHelperEngine<Customer>();

        int readCount = 0;
        engine.AfterReadRecord += (sender, e) =>
        {
            readCount++;
            if (e.Record.Balance > 2000)
                Console.WriteLine($"   High value customer: {e.Record.Name}");
        };

        string csv = "CustomerId,Name,Email,Balance\r\n" +
                    "1,John Doe,john@example.com,1500.50\r\n" +
                    "2,Jane Smith,jane@example.com,2300.75";

        var customers = engine.ReadString(csv);
        Console.WriteLine($"   Total processed: {readCount}");
        Console.WriteLine();
    }

    static async Task AsyncStreaming()
    {
        Console.WriteLine("7. Async streaming (IAsyncEnumerable):");

        // Create temp file for demo
        string tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile,
            "CustomerId,Name,Email,Balance\r\n" +
            "1,John Doe,john@example.com,1500.50\r\n" +
            "2,Jane Smith,jane@example.com,2300.75").ConfigureAwait(false);

        var engine = new FileHelperAsyncEngine<Customer>();

        int count = 0;
        await foreach (var customer in engine.ReadAsync(tempFile).ConfigureAwait(false))
        {
            count++;
        }

        Console.WriteLine($"   Async streamed {count} records");
        Console.WriteLine();

        File.Delete(tempFile);
    }

    static void InterfaceValidation()
    {
        Console.WriteLine("8. Record with validation interface:");

        var engine = new FileHelperEngine<ValidatedCustomer>();
        engine.ErrorMode = ErrorMode.SaveAndContinue;

        // CSV with comment line (should be skipped) and negative balance
        string csv = "CustomerId,Name,Email,Balance\r\n" +
                    "# This is a comment\r\n" +
                    "1,John Doe,john@example.com,1500.50\r\n" +
                    "2,Bad Customer,bad@example.com,-500.00\r\n" +  // Will be skipped (negative)
                    "3,Jane Smith,jane@example.com,2300.75";

        var customers = engine.ReadString(csv);

        Console.WriteLine($"   Read {customers.Length} customers (skipped comment and negative balance)");
        foreach (var c in customers)
            Console.WriteLine($"   - {c.Name}: ${c.Balance}");
        Console.WriteLine();
    }
}
