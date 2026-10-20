# Opt-in features

`FileHelperEngine<T>.ReadFile`, `ReadString` and `ReadStream` work as in FileHelpers 3.x: they read
the whole input and return an array. This page describes the other ways to read, for large inputs or
when allocations matter. They all go through the same record pipeline, so they produce the same
records, `HeaderText`/`FooterText`, errors and line numbers as `ReadString` for the same input.

The examples use these record types:

```csharp
[DelimitedRecord(",")]
public sealed class Customer
{
    public int Id;
    public string Name = "";
    public decimal Balance;
}

[DelimitedRecord(",")]
public record struct Reading
{
    public int SensorId;
    public double Value;
}
```

## Streaming

### `ReadAsync`: `IAsyncEnumerable<T>`

```csharp
var engine = new FileHelperEngine<Customer>();

await foreach (Customer customer in engine.ReadAsync("customers.csv", cancellationToken))
{
    Process(customer);
}
```

The file is read through `System.IO.Pipelines` and only the current record is kept in memory.
`FileHelperAsyncEngine<T>` has the same method. Cancelling the token stops the read with an
`OperationCanceledException`.

### `ReadSync`: `IEnumerable<T>`

```csharp
using var reader = new StreamReader("customers.csv");   // the engine does not dispose the reader
foreach (Customer customer in engine.ReadSync(reader))
{
    Process(customer);
}

foreach (Customer customer in engine.ReadSync(csvText))   // or from a string
{
    Process(customer);
}
```

### Record by record: `FileHelperAsyncEngine<T>`

The FileHelpers 3.x pull API:

```csharp
using var engine = new FileHelperAsyncEngine<Customer>();
engine.BeginReadFile("customers.csv");
while (engine.ReadNext() is { } customer)
{
    Process(customer);
}
```

`BeginReadStream` and `BeginReadString` read from other sources, and `BeginWriteFile`,
`BeginAppendFile`, `BeginWriteStream` and `BeginWriteString` with `WriteNext` write record by record.

One engine instance runs one session at a time: `ReadAsync`, `BeginRead*` and `BeginWrite*` throw
`InvalidOperationException` while another read or write session is open on it. Use a separate
instance for each concurrent read or write.

### Multi-record and master/detail files

`MultiRecordAsyncEngine.ReadAsync` and `MasterDetailAsyncEngine<TMaster, TDetail>.ReadAsync` stream
from a file path or a `Stream`. A `Stream` you pass in stays open; you dispose it.

### Footer text

`[IgnoreLast(n)]` footer lines are known only at the end of the input. `FooterText` is set when a
streaming read reaches the end; if you stop early, it stays `null`.

## Callbacks

The callback overloads of `ReadFile`, `ReadString` and `ReadSpan` pass each record to your code as it
is parsed and return the number of records. No result array is allocated:

```csharp
int count = engine.ReadFile("customers.csv", customer => Process(customer));

engine.ReadString(csvText, customer => Process(customer), maxRecords: 1000);

await engine.ReadFileAsync("customers.csv", customer => Process(customer), cancellationToken);
```

`ReadSpan` reads from a `ReadOnlySpan<char>`, for example a slice of a larger buffer:

```csharp
ReadOnlySpan<char> content = csvText.AsSpan();
Customer[] customers = engine.ReadSpan(content);
```

### By-reference callbacks

`ReadString` and `ReadSpan` also accept a `SpanRecordCallback<T>`, which receives the record by
reference. For large structs this avoids a copy per record:

```csharp
var sensors = new FileHelperEngine<Reading>();

double total = 0;
sensors.ReadString(readingsText, (ref Reading r) => total += r.Value);
```

## Struct records without allocations

A struct or `record struct` whose fields are all value types (numbers, dates, `bool`, `char`, `Guid`,
enums) is parsed straight from the input span. Read through a callback or `ReadSync`, there is no heap
allocation per record or per line; the test suite checks that the allocated bytes do not grow with the
number of records. `string` fields always allocate their string, and the array-returning methods
allocate the array.

## Record recycling for classes

For class records, `RecycleRecords = true` makes callback and streaming reads reuse one instance:

```csharp
var engine = new FileHelperEngine<Customer> { RecycleRecords = true };

engine.ReadFile("customers.csv", customer =>
{
    // 'customer' is the same object every time: copy what you need to keep.
    Save(customer.Id, customer.Name);
});
```

It applies to the `Action<T>` and `SpanRecordCallback<T>` overloads, `ReadSync`, `ReadAsync`,
`ReadFileAsync`, and to `FileHelperAsyncEngine<T>.ReadNext()`, `foreach` and `ReadAsync`.
Array-returning reads always create new instances. It has no effect for struct records, and needs a
class without init-only members (the source generator then emits the recycling support).

## Parallel reads

For large inputs that fit in memory, `ParallelReadFile` and `ParallelReadString` split the input into
chunks (4M characters by default, at least 64K) and parse them on several cores:

```csharp
Customer[] customers = engine.ParallelReadFile("customers.csv");

Customer[] withProgress = engine.ParallelReadFile(
    "customers.csv",
    chunkSize: 1 << 20,
    progress: (chunk, chunks, recordsSoFar) => Console.WriteLine($"{chunk + 1}/{chunks}"),
    cancellationToken: cancellationToken);
```

The result is the same as `ReadString` on the same input: the same records in the same order, header
and footer from the whole input, original line numbers, and the same `ErrorMode` behaviour (in
`ThrowException` mode the earliest failing record is reported). Inputs that cannot be split safely are
read sequentially: records whose quoted fields may span lines, `[FieldInNewLine]` records, engines with
read events or `INotifyRead<T>` records, and inputs smaller than two chunks.

`[FieldQuoted]` allows multiline values by default (as in FileHelpers 3.x), so a record with a quoted
field is read sequentially unless you turn multiline reading off for it:

```csharp
[DelimitedRecord(",", MultilineMode = MultilineMode.NotAllow)]   // or AllowForWrite
public sealed class Product
{
    public int Id;

    [FieldQuoted]
    public string Name = "";
}

Product[] products = new FileHelperEngine<Product>().ParallelReadFile("products.csv");
```

`[FieldQuoted(QuoteMode.OptionalForRead, MultilineMode.NotAllow)]` does the same for one field.

## Choosing a read method

| Method | Result array | Per-record allocation | Use for |
|---|---|---|---|
| `ReadFile` / `ReadString` / `ReadStream` / `ReadSpan` | Yes | class: one object; value-type struct: none | Small and medium inputs |
| Callback overloads | No | class: one object (none with `RecycleRecords`); value-type struct: none | Processing records as they are read |
| `ReadSync` | No | as callbacks | Lazy enumeration with LINQ or `foreach` |
| `ReadAsync` | No | as callbacks | Large files, async code, cancellation |
| `FileHelperAsyncEngine<T>` | No | as callbacks | 3.x-style pull loops, record-by-record writing |
| `ParallelReadFile` / `ParallelReadString` | Yes | class: one object | Large in-memory inputs on multi-core machines |

`string` fields allocate their value in every case.
