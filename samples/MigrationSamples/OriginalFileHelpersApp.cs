// ============================================================================
// OriginalFileHelpersApp - Example using FileHelpers 3.x
// ============================================================================
// This file demonstrates typical FileHelpers 3.x usage patterns.
// It is NOT compiled in this project - it's documentation-only.
// See ModernLibsApp.cs for the equivalent ModernLibs code.
// ============================================================================

/*
using FileHelpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OriginalFileHelpersApp
{
    // Basic delimited record
    [DelimitedRecord(",")]
    [IgnoreFirst(1)]  // Skip header
    public class Customer
    {
        [FieldOrder(1)] public int CustomerId;
        [FieldOrder(2)] public string Name;
        [FieldOrder(3)] public string Email;
        [FieldOrder(4)] public decimal Balance;
    }

    // Fixed-length record
    [FixedLengthRecord]
    public class Employee
    {
        [FieldOrder(1)] [FieldFixedLength(8)] public int EmployeeId;
        [FieldOrder(2)] [FieldFixedLength(25)] public string Name;
        [FieldOrder(3)] [FieldFixedLength(12)] public decimal Salary;
    }

    // Record with converter
    [DelimitedRecord(",")]
    public class Order
    {
        [FieldOrder(1)] public int OrderId;
        [FieldOrder(2)] [FieldConverter(ConverterKind.Date, "yyyy-MM-dd")] public DateTime OrderDate;
        [FieldOrder(3)] [FieldConverter(ConverterKind.Decimal, ".")] public decimal Total;
    }

    // Record implementing INotifyRead/INotifyWrite. The engine calls these methods on the record
    // itself; the non-generic event args have no Record property, so the record's own fields are used.
    [DelimitedRecord(",")]
    [IgnoreFirst(1)]
    public class ValidatedCustomer : INotifyRead, INotifyWrite
    {
        [FieldOrder(1)] public int CustomerId;
        [FieldOrder(2)] public string Name;
        [FieldOrder(3)] public string Email;
        [FieldOrder(4)] public decimal Balance;

        public void BeforeRead(BeforeReadEventArgs e)
        {
            // Skip comment lines
            if (e.RecordLine.StartsWith("#"))
                e.SkipThisRecord = true;
        }

        public void AfterRead(AfterReadEventArgs e)
        {
            // Skip records with negative balance
            if (Balance < 0)
                e.SkipThisRecord = true;
        }

        public void BeforeWrite(BeforeWriteEventArgs e)
        {
            // Validation before write
            if (string.IsNullOrEmpty(Email))
                e.SkipThisRecord = true;
        }

        public void AfterWrite(AfterWriteEventArgs e)
        {
            // Post-write action
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== FileHelpers 3.x Examples ===\n");

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
                Console.WriteLine($"   - Line {err.LineNumber}: {err.ExceptionInfo.Message}");
            Console.WriteLine();
        }

        static void StreamingLargeFile()
        {
            Console.WriteLine("5. Streaming large file:");

            var engine = new FileHelperAsyncEngine<Customer>();

            // Normally: engine.BeginReadFile("largefile.csv");
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
    }
}
*/
