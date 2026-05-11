# Opt-In Performance Features

ModernLibs.FileHelpers provides three opt-in features for scenarios where allocation-free parsing is critical: **streaming APIs**, **record recycling**, and **ref struct (span) records**. All three are zero-cost when not used — the default batch APIs behave exactly as before.

---

## 1. Streaming APIs

### IAsyncEnumerable<T> — ReadAsync

Stream records from a file one at a time without loading the entire file into memory:

```csharp
var engine = new FileHelperEngine<Customer>();

await foreach (var customer in engine.ReadAsync("customers.csv"))
{
    Process(customer);
}
```

**When to use:** Files larger than available memory, or when you want to start processing records before the entire file is read.

**Known limitation:** `IgnoreLast` is not honored by `ReadAsync`. Trailing lines that would be skipped by `[IgnoreLast(N)]` will be parsed as data records. Use `ReadFile` when `IgnoreLast` semantics are required.

**Allocation:** Each record is a new heap allocation for class types. Zero allocations for `record struct` types.

### IEnumerable<T> — ReadSync

Stream records from a `TextReader` or string:

```csharp
// From TextReader (reader is NOT disposed by the engine)
using var reader = new StreamReader("customers.csv");
foreach (var customer in engine.ReadSync(reader))
{
    Process(customer);
}

// From string
foreach (var customer in engine.ReadSync(csvContent))
{
    Process(customer);
}
```

**When to use:** You already have a `TextReader` or string and want lazy enumeration instead of batch materialization.

---

## 2. Callback-Based Reads

Callback methods eliminate the `T[]` result array allocation entirely. Records are passed to your callback as they're parsed:

### Action<T> Callback

```csharp
var engine = new FileHelperEngine<Customer>();

// From span (zero content allocation)
engine.ReadSpan(content.AsSpan(), customer =>
{
    Process(customer);
});

// From string
engine.ReadString(csvContent, customer =>
{
    Process(customer);
});

// From file (streaming from disk)
engine.ReadFile("customers.csv", customer =>
{
    Process(customer);
});

// Async from file
await engine.ReadFileAsync("customers.csv", customer =>
{
    Process(customer);
});
```

**With maxRecords cap:**
```csharp
engine.ReadSpan(content.AsSpan(), customer => Process(customer), maxRecords: 1000);
```

**When to use:** ETL pipelines, database inserts, or any scenario where records are consumed inline and don't need to be held in memory.

### SpanRecordCallback<T> — Ref Callback

Pass records by reference to avoid struct copying:

```csharp
engine.ReadSpan(content.AsSpan(), (ref Customer customer) =>
{
    // Can mutate the record in-place
    customer.Balance *= 1.1m;
    Process(customer);
});
```

**When to use:** You need to modify records in-place or want to avoid copying large struct types.

---

## 3. Record Recycling

When `RecycleRecords` is enabled, callback-based reads reuse a single class instance across all callback invocations:

```csharp
var engine = new FileHelperEngine<Customer> { RecycleRecords = true };

engine.ReadString(csvContent, customer =>
{
    // IMPORTANT: 'customer' is the same reference each time.
    // Copy values if you need to retain them.
    var id = customer.Id;
    var name = customer.Name;  // copy the string
    SaveToDatabase(id, name);
});
```

**When to use:** High-throughput scenarios with class types where you want zero heap allocations per record (beyond the string fields).

**Important:** The callback receives the same reference each time. If you need to retain record data, copy the values before the next callback invocation.

**Struct types:** `RecycleRecords` has no effect for `record struct` types (they're already zero-allocation).

---

## 4. RecordPool<T>

For manual pooling control, use `RecordPool<T>` directly:

```csharp
using var pool = new RecordPool<Customer>(initialCapacity: 64);

var engine = new FileHelperEngine<Customer>();
engine.ReadString(csvContent, customer =>
{
    // Process the record
    Process(customer);
    
    // Return to pool for reuse
    pool.Return(customer);
});

// Rent from pool when you need a new instance
var record = pool.Rent();
```

**When to use:** You need fine-grained control over object pooling, or want to pool records across multiple engine invocations.

**Thread safety:** `RecordPool<T>` is NOT thread-safe. It's designed for single-threaded per-engine-instance use. For concurrent access, wrap with `lock` at the call site.

---

## 5. Ref Struct Records

For maximum performance, define your record as a `ref struct`. This guarantees zero heap allocations and enables stack-only parsing:

```csharp
[DelimitedRecord(",")]
[SpanRecord]
public ref struct FastRecord
{
    [FieldOrder(1)] public int Id;
    [FieldOrder(2)] public int Value;
    [FieldOrder(3)] public decimal Amount;
}
```

**Usage with ref callback:**
```csharp
var engine = new FileHelperEngine<FastRecord>();

engine.ReadSpan(content.AsSpan(), (ref FastRecord record) =>
{
    Process(record.Id, record.Value, record.Amount);
});
```

**Limitations of ref struct records:**
- Cannot be used as generic type arguments (`List<FastRecord>`, `Task<FastRecord>`, etc.)
- Cannot be stored in arrays (`FastRecord[]`)
- Cannot implement interfaces
- Cannot be used with `async/await`
- Cannot be captured by closures that cross await boundaries

**When to use:** Performance-critical hot paths where you can work within the ref struct constraints.

---

## Feature Comparison

| Feature | T[] Allocation | Per-Record Allocation | Best For |
|---------|---------------|----------------------|----------|
| `ReadFile/ReadString` (default) | Yes | Yes (class) / No (struct) | Simple batch processing |
| `ReadSpan` | Yes | Yes (class) / No (struct) | Span-based input, faster parsing |
| `ReadSpan(callback)` | No | Yes (class) / No (struct) | Inline processing, ETL |
| `ReadSpan(ref callback)` | No | Yes (class) / No (struct) | In-place mutation, large structs |
| `RecycleRecords + callback` | No | Near-zero (class) | High-throughput class records |
| `RecordPool<T>` | No | Controlled manually | Cross-invocation pooling |
| Ref struct records | No | Zero (stack-only) | Maximum performance |

---

## Migration Examples

### Before (batch allocation):
```csharp
var engine = new FileHelperEngine<Order>();
Order[] orders = engine.ReadFile("orders.csv");
foreach (var order in orders)
{
    db.Insert(order);
}
```

### After (zero allocation):
```csharp
var engine = new FileHelperEngine<Order> { RecycleRecords = true };
engine.ReadFile("orders.csv", order =>
{
    db.Insert(order.Id, order.Customer, order.Amount);
});
```

### Before (loading entire file):
```csharp
var engine = new FileHelperEngine<LogEntry>();
LogEntry[] entries = engine.ReadFile("huge-log.csv");
var errors = entries.Where(e => e.Level == "ERROR");
```

### After (streaming):
```csharp
var engine = new FileHelperEngine<LogEntry>();
await foreach (var entry in engine.ReadAsync("huge-log.csv"))
{
    if (entry.Level == "ERROR")
        ReportError(entry);
}
```
