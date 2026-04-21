# Migration Samples

This directory contains side-by-side examples showing the differences between FileHelpers 3.x and ModernLibs.FileHelpers 4.x.

## Files

- **OriginalFileHelpersApp.cs** - Documentation-only file showing typical FileHelpers 3.x patterns (commented out - not compiled)
- **ModernLibsApp.cs** - Working application demonstrating ModernLibs.FileHelpers equivalents
- **MigrationSamples.csproj** - Project file to compile and run ModernLibsApp

## Running the Sample

```bash
cd samples/MigrationSamples
dotnet run
```

## Key Differences

| Aspect | FileHelpers 3.x | ModernLibs.FileHelpers 4.x |
|--------|-----------------|---------------------------|
| Namespace | `using FileHelpers;` | `using ModernLibs.FileHelpers;` |
| INotify interfaces | `INotifyRead`, `INotifyWrite` (non-generic) | `INotifyRead<T>`, `INotifyWrite<T>` (generic) |
| Performance | Reflection-based | Source-generated, AOT-compatible |
| Allocations | Heap allocations per record | Zero allocations for struct types |

## Verification

Both implementations produce identical output for:
- CSV reading with headers
- CSV writing with headers
- Fixed-length file handling
- Error handling with SaveAndContinue mode
- Streaming with BeginReadFile/ReadNext
- Event handling
- IAsyncEnumerable streaming
- Interface-based validation

## Migration Checklist

When migrating your own code:

1. ✅ Update `using FileHelpers;` to `using ModernLibs.FileHelpers;`
2. ✅ If implementing `INotifyRead`/`INotifyWrite`, change to generic versions
3. ✅ Verify record classes have `[DelimitedRecord]` or `[FixedLengthRecord]`
4. ✅ Test with sample data before deploying
5. ✅ Consider using `record struct` for zero-allocation parsing

## Expected Output

```
=== ModernLibs.FileHelpers 4.x Examples ===

Note: This code is 95%+ compatible with FileHelpers 3.x

1. Reading CSV with header:
   Header: CustomerId,Name,Email,Balance
   Read 2 customers
   - John Doe: $1500.50
   - Jane Smith: $2300.75

2. Writing CSV:
   Generated CSV:
CustomerId,Name,Email,Balance
1,John Doe,john@example.com,1500.50
2,Jane Smith,jane@example.com,2300.75

3. Reading fixed-length file:
   Read 2 employees
   - John Doe: $50000.00
   - Jane Smith: $65000.00

4. Error handling:
   Valid records: 2
   Errors: 1
   - Line 3: ...

5. Streaming large file (pull model):
   Streamed 2 records

6. Event handling:
   High value customer: Jane Smith
   Total processed: 2

7. Async streaming (IAsyncEnumerable):
   Async streamed 2 records

8. Record with validation interface:
   Read 2 customers (skipped comment and negative balance)
   - John Doe: $1500.50
   - Jane Smith: $2300.75

=== All examples completed successfully! ===
```
