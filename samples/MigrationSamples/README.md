# Migration Samples

Side-by-side examples of FileHelpers 3.x and ModernLibs.FileHelpers 4.x code.

## Files

- **OriginalFileHelpersApp.cs** - FileHelpers 3.x version (commented out, not compiled)
- **ModernLibsApp.cs** - the same program written for ModernLibs.FileHelpers, plus two 4.x-only examples
- **MigrationSamples.csproj** - project file that compiles and runs ModernLibsApp

## Running the Sample

```bash
cd samples/MigrationSamples
dotnet run
```

## Differences shown in this sample

| Aspect | FileHelpers 3.x | ModernLibs.FileHelpers 4.x |
|--------|-----------------|---------------------------|
| Namespace | `using FileHelpers;` | `using ModernLibs.FileHelpers;` |
| INotify interfaces | `INotifyRead`, `INotifyWrite` (non-generic; the record uses its own fields) | `INotifyRead<T>`, `INotifyWrite<T>` (generic; `e.Record` is typed) |
| Record descriptors | Built with reflection at run time | Generated at compile time (trimming- and Native AOT-compatible) |

Examples 1 to 6 exist in both files. Examples 7 (`IAsyncEnumerable` streaming) and 8 (generic
`INotifyRead<T>` validation) use 4.x APIs.

The full list of API differences is in [docs/MIGRATION.md](../../docs/MIGRATION.md).

## Migration Checklist

When migrating your own code:

1. Update `using FileHelpers;` to `using ModernLibs.FileHelpers;`
2. If implementing `INotifyRead`/`INotifyWrite`, change to the generic versions
3. Check that every record type has `[DelimitedRecord]` or `[FixedLengthRecord]` (the build reports
   [MLFH001](../../docs/diagnostics.md#mlfh001) otherwise)
4. Go through [docs/MIGRATION.md](../../docs/MIGRATION.md) for the APIs your code uses
5. Test with sample data before deploying

## Expected Output

```
=== ModernLibs.FileHelpers 4.x Examples ===

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
