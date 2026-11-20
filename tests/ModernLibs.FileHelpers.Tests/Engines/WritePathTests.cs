// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007
// CA1849: Synchronous file I/O is appropriate in tests.
#pragma warning disable CA1849
// MLFH001: Compat49NewLine and Compat48* are 4.9.1 and 4.8.0 snapshot types without record attributes on purpose.
#pragma warning disable MLFH001

using System.Buffers;
using System.Text;
using ModernLibs.FileHelpers.Tests.Compat;
using ModernLibs.FileHelpers.Tests.Engines.Fixtures;
using ModernLibs.FileHelpers.Tests.Events.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.Engines;

/// <summary>
/// The write-path choice of the engines (T22, design 5.2): a descriptor that implements
/// <see cref="IRecordWriterDescriptor{T}"/> is formatted through <see cref="RecordWriter"/> unless
/// <see cref="EnginePaths.LegacyWriter"/> is set; 4.8 and 4.9 descriptors keep the 4.9 path. Every engine writes the
/// same text on both paths. The counting tests wrap the descriptors of the current generator in a
/// <see cref="WriterCheckingDescriptor{T}"/>; since T23 those descriptors implement the interface themselves, and
/// <see cref="Generated_Descriptors_Take_The_Record_Writer_On_Every_Engine"/> checks that the engines made by the public
/// constructors choose their record writer.
/// </summary>
[Category("Engines")]
public sealed class WritePathTests
{
    private static readonly EnginePaths NewPaths = new(LegacyParse: false, ScalarTokenizer: false, LegacyWriter: false);
    private static readonly EnginePaths LegacyWriterPaths = new(LegacyParse: false, ScalarTokenizer: false, LegacyWriter: true);

    private static RecordAction MasterFirst(ReadOnlySpan<char> line) => RecordAction.Master;

    [Test]
    public async Task New_Format_Is_Used_Without_Events()
    {
        var records = ValidRecords<Compat49NewLineTwin>("Compat49NewLine");
        var descriptor = Checking<Compat49NewLineTwin>();

        foreach (var (operation, write) in Operations(descriptor, NewPaths, "\r\n"))
        {
            int before = descriptor.WriterFormatCalls;
            write(records);
            await Assert.That(descriptor.WriterFormatCalls - before).IsEqualTo(records.Length).Because(operation);
        }

        await Assert.That(descriptor.LegacyFormatCalls).IsEqualTo(0);
    }

    [Test]
    public async Task LegacyWriter_Option_Uses_The_Old_Format()
    {
        var records = ValidRecords<Compat49NewLineTwin>("Compat49NewLine");
        var descriptor = Checking<Compat49NewLineTwin>();

        foreach (var (operation, write) in Operations(descriptor, LegacyWriterPaths, "\r\n"))
        {
            int before = descriptor.LegacyFormatCalls;
            write(records);
            await Assert.That(descriptor.LegacyFormatCalls - before).IsEqualTo(records.Length).Because(operation);
        }

        await Assert.That(descriptor.WriterFormatCalls).IsEqualTo(0);
    }

    /// <summary>
    /// T23 (preflight F14): for every <c>Compat49Matrix</c> shape the generated descriptor implements
    /// <see cref="IRecordWriterDescriptor{T}"/>, and every engine made by a public constructor or factory over it (plain,
    /// asynchronous, multi-record, master-detail) chose the record writer; with <see cref="EnginePaths.LegacyWriter"/>
    /// none did. The write tests above count the calls through a <see cref="WriterCheckingDescriptor{T}"/>; here no
    /// fixture stands between the engine and the generated code, so <see cref="WritePathProbe"/> reads the choice.
    /// </summary>
    [Test]
    public async Task Generated_Descriptors_Take_The_Record_Writer_On_Every_Engine()
    {
        var results = Compat49Matrix.Twins(new PublicEnginesChoice()).ToList();

        await Assert.That(results.Count).IsEqualTo(19);
        await Assert.That(string.Join("\n", results.SelectMany(r => r))).IsEqualTo(string.Empty);
    }

    /// <summary>
    /// Each event row of design 5.2 (none, <c>AfterWriteRecord</c> that reads or replaces the line,
    /// <c>BeforeWriteRecord</c> that skips, <see cref="INotifyWrite{T}"/>): the text and the lines the events saw are
    /// those of the <see cref="EnginePaths.LegacyWriter"/> engine, on the synchronous, the asynchronous and the
    /// append paths.
    /// </summary>
    [Test]
    [Arguments("none")]
    [Arguments("after-reads")]
    [Arguments("after-replaces")]
    [Arguments("before-skips")]
    [Arguments("before-skips-after-replaces")]
    public async Task With_AfterWriteRecord_Or_INotifyWrite_Text_And_Event_Line_Are_The_Same(string events)
    {
        var records = ValidRecords<Compat49NewLineTwin>("Compat49NewLine");
        records = [.. records, .. records];
        var newDescriptor = Checking<Compat49NewLineTwin>();
        var legacyDescriptor = Checking<Compat49NewLineTwin>();

        string expected = WriteWithEvents(legacyDescriptor, LegacyWriterPaths, records, events);
        string actual = WriteWithEvents(newDescriptor, NewPaths, records, events);

        await Assert.That(actual).IsEqualTo(expected);
        await Assert.That(newDescriptor.LegacyFormatCalls).IsEqualTo(0);
        await Assert.That(newDescriptor.WriterFormatCalls).IsEqualTo(legacyDescriptor.LegacyFormatCalls);
        await Assert.That(newDescriptor.WriterFormatCalls).IsGreaterThan(0);
        if (events.Contains("after", StringComparison.Ordinal))
            await Assert.That(actual).Contains("event:");
    }

    [Test]
    public async Task With_INotifyWrite_Text_And_Event_Line_Are_The_Same()
    {
        static NotifiableWriteRecord[] Records() =>
        [
            new() { Id = 1, Name = "a" },
            new() { Id = 42, Name = "special" },
            new() { Id = -1, Name = "skipped" },
            new() { Id = 3, Name = "b, c" },
        ];

        var newDescriptor = Checking<NotifiableWriteRecord>();
        var legacyDescriptor = Checking<NotifiableWriteRecord>();

        string expected = WriteWithEvents(legacyDescriptor, LegacyWriterPaths, Records(), "none");
        string actual = WriteWithEvents(newDescriptor, NewPaths, Records(), "none");

        await Assert.That(actual).IsEqualTo(expected);
        await Assert.That(actual).Contains("42,special # Special record");
        await Assert.That(actual).DoesNotContain("skipped");
        await Assert.That(newDescriptor.LegacyFormatCalls).IsEqualTo(0);
        await Assert.That(newDescriptor.WriterFormatCalls).IsEqualTo(legacyDescriptor.LegacyFormatCalls);
    }

    /// <summary>
    /// For every <c>Compat49Matrix</c> shape (the descriptor of the current generator in a
    /// <see cref="WriterCheckingDescriptor{T}"/>), the valid records and each hand-made record of
    /// <see cref="Compat49WriteCases"/>, written by every engine and write method: the new path, and the legacy
    /// <c>Format</c> through the adapter <see cref="RecordFormatting.Format{T}"/>, write what the
    /// <see cref="EnginePaths.LegacyWriter"/> engine writes, exceptions and partial output included.
    /// </summary>
    [Test]
    [Arguments("\r\n")]
    [Arguments("\n")]
    [Arguments("|\n")]
    public async Task Same_Text_On_Every_Engine(string newLine)
    {
        string directory = Path.Combine(Path.GetTempPath(), "fh-t22-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var results = Compat49Matrix.Twins(new SameTextOnEveryEngine(newLine, directory)).ToList();

            await Assert.That(results.Count).IsEqualTo(19);
            await Assert.That(string.Join("\n", results.SelectMany(r => r))).IsEqualTo(string.Empty);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>
    /// Review Focus 5: the 4.9.1 <c>[FieldInNewLine]</c> descriptor (Generated49) still gets a
    /// <see cref="RentedBufferWriter"/> whose <c>NewLine</c> is the engine's <c>NewLineForWrite</c>, with and without
    /// <c>AfterWriteRecord</c>, through <c>WriteString</c>, <c>WriteStreamAsync</c> and
    /// <see cref="FileHelperAsyncEngine{T}.WriteNext(T)"/>: with <c>"|\n"</c> it writes the Golden49 text with
    /// <c>"\r\n"</c> replaced by <c>"|\n"</c>.
    /// </summary>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Old_Descriptors_Still_Get_A_RentedBufferWriter_With_NewLine_Set(bool afterWriteRecord)
    {
        const string name = "Compat49NewLine";
        var descriptor = new LegacyCheckingDescriptor<Compat49NewLine>(FileHelperRegistry<Compat49NewLine>.Descriptor, "|\n");
        var engine = new FileHelperEngine<Compat49NewLine>(descriptor, Encoding.UTF8, NewPaths) { NewLineForWrite = "|\n" };
        if (afterWriteRecord)
            engine.AfterWriteRecord += (_, e) => e.RecordLine = e.RecordLine;

        int index = 0;
        int written = 0;
        foreach (string input in Compat49Inputs.For(name))
        {
            string? golden = Compat49Golden.Expected(name, index, "round-trip");
            await Assert.That(golden).IsNotNull();
            string goldenWritten = golden!.Split('\n').First(l => l.StartsWith("written:", StringComparison.Ordinal))["written:".Length..];

            var records = new FileHelperEngine<Compat49NewLine> { ErrorMode = ErrorMode.SaveAndContinue }.ReadString(input);
            string expected = goldenWritten.Replace("\\r\\n", "|\\n", StringComparison.Ordinal);
            string because = $"input {index} ({Compat49Inputs.Kinds[index]})";

            string text = engine.WriteString(records);
            written += engine.TotalRecords;
            await Assert.That(Escape(text)).IsEqualTo(expected).Because("WriteString, " + because);

            // WriteStreamAsync: the second branch of AppendPendingRecord (through the session buffer, not the pending block).
            var asyncWriter = new StringWriter();
            await engine.WriteStreamAsync(asyncWriter, records);
            written += engine.TotalRecords;
            await Assert.That(Escape(asyncWriter.ToString())).IsEqualTo(expected).Because("WriteStreamAsync, " + because);

            using (var asyncEngine = new FileHelperAsyncEngine<Compat49NewLine>(descriptor, NewPaths) { NewLineForWrite = "|\n" })
            {
                if (afterWriteRecord)
                    asyncEngine.AfterWriteRecord += (_, e) => e.RecordLine = e.RecordLine;
                asyncEngine.BeginWriteString();
                asyncEngine.WriteNext(records);
                written += asyncEngine.TotalRecords;
                await Assert.That(Escape(asyncEngine.EndWriteString())).IsEqualTo(expected).Because("FileHelperAsyncEngine.WriteNext, " + because);
            }

            index++;
        }

        await Assert.That(descriptor.FormatCalls).IsEqualTo(written);
        await Assert.That(written).IsGreaterThan(0);
        await Assert.That(descriptor.WrongWriters).IsEqualTo(0);
    }

    /// <summary>
    /// The two 4.8.0 descriptors (Generated48, no <c>[FieldInNewLine]</c> snapshot there) get a
    /// <see cref="RentedBufferWriter"/> whose <c>NewLine</c> is the engine's <c>NewLineForWrite</c>, with and without
    /// <c>AfterWriteRecord</c>.
    /// </summary>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Old_4_8_Descriptors_Still_Get_A_RentedBufferWriter_With_NewLine_Set(bool afterWriteRecord)
    {
        var quoted =new LegacyCheckingDescriptor<Compat48Quoted>(FileHelperRegistry<Compat48Quoted>.Descriptor, "|\n");
        var fixedLength = new LegacyCheckingDescriptor<Compat48Fixed>(FileHelperRegistry<Compat48Fixed>.Descriptor, "|\n");
        var (quotedText, quotedCrLf) = WriteOld(quoted, "id,name,amount,day,code,count,note\r\n1,\"Smith, John\",12.50,2026-11-08,A1,3,\"first\"\r\n2,plain,0.1,2024-02-29,B2,,\r\n", afterWriteRecord);
        var (fixedText, fixedCrLf) = WriteOld(fixedLength, "000001Alice     08112026000012.50\r\n000002Bob       29022024000000.10\r\n", afterWriteRecord);

        // These records hold no line break, so "|\n" only replaces the record terminators.
        await Assert.That(quotedText).IsEqualTo(quotedCrLf.Replace("\r\n", "|\n", StringComparison.Ordinal));
        await Assert.That(fixedText).IsEqualTo(fixedCrLf.Replace("\r\n", "|\n", StringComparison.Ordinal));
        await Assert.That(quotedText).StartsWith("1,");
        await Assert.That(fixedText).StartsWith("000001Alice");
        await Assert.That(quoted.FormatCalls).IsEqualTo(2);
        await Assert.That(fixedLength.FormatCalls).IsEqualTo(2);
        await Assert.That(quoted.WrongWriters + fixedLength.WrongWriters).IsEqualTo(0);
    }

    [Test]
    public async Task Legacy_Adapter_Into_A_Foreign_BufferWriter_Writes_CrLf()
    {
        var records = ValidRecords<Compat49NewLineTwin>("Compat49NewLine");
        var descriptor = Checking<Compat49NewLineTwin>();
        var engine = new FileHelperEngine<Compat49NewLineTwin>(descriptor, Encoding.UTF8, NewPaths) { NewLineForWrite = "\r\n" };

        foreach (var record in records)
        {
            var foreign = new ArrayBufferWriter<char>();
            RecordFormatting.Format(descriptor, record, foreign);
            string engineText = engine.WriteString([record]);

            await Assert.That(foreign.WrittenSpan.ToString() + "\r\n").IsEqualTo(engineText);
            await Assert.That(foreign.WrittenSpan.ToString()).Contains(",\r\n");

            // Into an engine buffer the adapter takes its NewLine (a 4.10 descriptor on the LegacyWriter path).
            using var engineBuffer = new RentedBufferWriter(16) { NewLine = "|\n" };
            engineBuffer.Write("x".AsSpan());
            RecordFormatting.Format(descriptor, record, engineBuffer);
            await Assert.That(engineBuffer.WrittenSpan.ToString()).IsEqualTo("x" + foreign.WrittenSpan.ToString().Replace(",\r\n", ",|\n", StringComparison.Ordinal));
        }

        // A record that throws leaves the foreign writer as it was.
        var throwing = Checking<Compat49MultilineNotAllowTwin>();
        var target = new ArrayBufferWriter<char>();
        target.Write("kept".AsSpan());
        var bad = new Compat49MultilineNotAllowTwin { Id = 1, Name = "a\nb", Tail = "t" };
        await Assert.That(() => RecordFormatting.Format(throwing, bad, target)).ThrowsExactly<BadUsageException>();
        await Assert.That(target.WrittenSpan.ToString()).IsEqualTo("kept");

        await Assert.That(() => RecordFormatting.Format<Compat49NewLineTwin>(null!, records[0], new ArrayBufferWriter<char>())).ThrowsExactly<ArgumentNullException>();
        await Assert.That(() => RecordFormatting.Format(descriptor, records[0], null!)).ThrowsExactly<ArgumentNullException>();
    }

    private static WriterCheckingDescriptor<T> Checking<T>(bool legacyThroughAdapter = false) =>
        new(FileHelperRegistry<T>.Descriptor, legacyThroughAdapter);

    private static T[] ValidRecords<T>(string name) =>
        new FileHelperEngine<T> { ErrorMode = ErrorMode.SaveAndContinue }.ReadString(Compat49Inputs.For(name).First());

    /// <summary>The records of <paramref name="input"/> written through <paramref name="descriptor"/> with <c>"|\n"</c>, and by the plain engine with <c>"\r\n"</c>.</summary>
    private static (string Text, string CrLfText) WriteOld<T>(LegacyCheckingDescriptor<T> descriptor, string input, bool afterWriteRecord)
    {
        var records = new FileHelperEngine<T> { ErrorMode = ErrorMode.SaveAndContinue }.ReadString(input);
        var engine = new FileHelperEngine<T>(descriptor, Encoding.UTF8, NewPaths) { NewLineForWrite = "|\n" };
        if (afterWriteRecord)
            engine.AfterWriteRecord += (_, e) => e.RecordLine = e.RecordLine;
        return (engine.WriteString(records), new FileHelperEngine<T> { NewLineForWrite = "\r\n" }.WriteString(records));
    }

    /// <summary>
    /// The write operations that format each record once, with <paramref name="paths"/>: the synchronous engine
    /// (string, stream, file, append), the asynchronous writes, <see cref="FileHelperAsyncEngine{T}.WriteNext(T)"/>,
    /// the multi-record and the master-detail engine.
    /// </summary>
    private static IEnumerable<(string Operation, Action<T[]> Write)> Operations<T>(IRecordDescriptor<T> descriptor, EnginePaths paths, string newLine)
    {
        FileHelperEngine<T> Engine() => new(descriptor, Encoding.UTF8, paths) { NewLineForWrite = newLine };
        string file = Path.Combine(Path.GetTempPath(), "fh-t22-" + Guid.NewGuid().ToString("N") + ".txt");

        yield return ("WriteString", records => Engine().WriteString(records));
        yield return ("WriteStream", records => Engine().WriteStream(new StringWriter(), records));
        yield return ("WriteFile", records => Engine().WriteFile(file, records));
        yield return ("AppendToFile(record)", records =>
        {
            var engine = Engine();
            foreach (var record in records)
                engine.AppendToFile(file, record);
        });
        yield return ("AppendToFile(records)", records => Engine().AppendToFile(file, records));
        yield return ("WriteStreamAsync(TextWriter)", records => Engine().WriteStreamAsync(new StringWriter(), records).GetAwaiter().GetResult());
        yield return ("WriteStreamAsync(Stream)", records => Engine().WriteStreamAsync(new MemoryStream(), records).GetAwaiter().GetResult());
        yield return ("FileHelperAsyncEngine.WriteNext", records =>
        {
            using var engine = new FileHelperAsyncEngine<T>(descriptor, paths) { NewLineForWrite = newLine };
            engine.BeginWriteString();
            foreach (var record in records)
                engine.WriteNext(record);
            engine.EndWriteString();
        });
        yield return ("MultiRecordEngine.WriteStream", records =>
            new MultiRecordEngine(new MultiRecordType<T>(static _ => true, descriptor, paths)) { NewLineForWrite = newLine }
                .WriteStream(new StringWriter(), records.Cast<object>()));
        yield return ("MasterDetailEngine.WriteStream", records =>
            new MasterDetailEngine<T, T>(MasterFirst, descriptor, descriptor, paths) { NewLineForWrite = newLine }
                .WriteStream(new StringWriter(), [new MasterDetails<T, T>(records[0], records[1..])]));
        File.Delete(file);
    }

    /// <summary>
    /// Writes <paramref name="records"/> with the given write events through <c>WriteString</c>, <c>WriteStream</c>,
    /// <c>WriteStreamAsync</c>, <see cref="FileHelperAsyncEngine{T}.WriteNext(T)"/> and <c>AppendToFile</c>; returns
    /// each text followed by the lines the <c>AfterWriteRecord</c> handlers saw (<c>event:</c>).
    /// </summary>
    private static string WriteWithEvents<T>(IRecordDescriptor<T> descriptor, EnginePaths paths, T[] records, string events)
    {
        var text = new StringBuilder();
        void Hook(Action<BeforeWriteHandler<T>> before, Action<AfterWriteHandler<T>> after)
        {
            if (events.Contains("before-skips", StringComparison.Ordinal))
                before((_, e) => e.SkipThisRecord = e.RecordNumber % 2 == 0);
            if (events.Contains("after-reads", StringComparison.Ordinal))
                after((_, e) => text.Append("event:").Append(Escape(e.RecordLine)).Append('\n'));
            if (events.Contains("after-replaces", StringComparison.Ordinal))
            {
                after((_, e) =>
                {
                    text.Append("event:").Append(Escape(e.RecordLine)).Append('\n');
                    e.RecordLine = e.RecordNumber == 3 ? null! : "<" + e.RecordLine + ">";
                });
            }
        }

        FileHelperEngine<T> Engine()
        {
            var engine = new FileHelperEngine<T>(descriptor, Encoding.UTF8, paths) { NewLineForWrite = "|\n" };
            Hook(h => engine.BeforeWriteRecord += h, h => engine.AfterWriteRecord += h);
            return engine;
        }

        text.Append("WriteString:").Append(Escape(Engine().WriteString(records))).Append('\n');

        var writer = new StringWriter();
        Engine().WriteStream(writer, records);
        text.Append("WriteStream:").Append(Escape(writer.ToString())).Append('\n');

        writer = new StringWriter();
        Engine().WriteStreamAsync(writer, records).GetAwaiter().GetResult();
        text.Append("WriteStreamAsync:").Append(Escape(writer.ToString())).Append('\n');

        using (var asyncEngine = new FileHelperAsyncEngine<T>(descriptor, paths) { NewLineForWrite = "|\n" })
        {
            Hook(h => asyncEngine.BeforeWriteRecord += h, h => asyncEngine.AfterWriteRecord += h);
            asyncEngine.BeginWriteString();
            foreach (var record in records)
                asyncEngine.WriteNext(record);
            text.Append("WriteNext:").Append(Escape(asyncEngine.EndWriteString())).Append('\n');
        }

        string file = Path.Combine(Path.GetTempPath(), "fh-t22-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var appending = Engine();
            foreach (var record in records)
                appending.AppendToFile(file, record);
            text.Append("AppendToFile:").Append(Escape(File.ReadAllText(file))).Append('\n');
        }
        finally
        {
            File.Delete(file);
        }

        return text.ToString();
    }

    private static string Escape(string? s) =>
        s is null ? "<null>" : s.Replace("\r", "\\r", StringComparison.Ordinal).Replace("\n", "\\n", StringComparison.Ordinal);

    /// <summary>One shape of <see cref="Generated_Descriptors_Take_The_Record_Writer_On_Every_Engine"/>: returns the failed checks.</summary>
    private sealed class PublicEnginesChoice : Compat49Matrix.ITwinVisitor<string[]>
    {
        public string[] Visit<T>(string name)
        {
            var failures = new List<string>();
            void Require(bool condition, string what)
            {
                if (!condition)
                    failures.Add($"{name}: {what}");
            }

            var descriptor = FileHelperRegistry<T>.Descriptor;
            Require(descriptor is IRecordWriterDescriptor<T>, "the generated descriptor does not implement IRecordWriterDescriptor<T>");

            using var asyncEngine = new FileHelperAsyncEngine<T>();
            Require(WritePathProbe.UsesRecordWriter(new FileHelperEngine<T>()), "new FileHelperEngine<T>() has no record writer");
            Require(WritePathProbe.UsesRecordWriter(asyncEngine), "new FileHelperAsyncEngine<T>() has no record writer");
            Require(WritePathProbe.UsesRecordWriter((MultiRecordType<T>)MultiRecordType.For<T>(static _ => true)), "MultiRecordType.For<T> has no record writer");
            Require(WritePathProbe.UsesRecordWriter(new MasterDetailEngine<T, T>(MasterFirst)), "new MasterDetailEngine<T, T>() has no record writer");

            using var legacyAsync = new FileHelperAsyncEngine<T>(descriptor, LegacyWriterPaths);
            Require(!WritePathProbe.UsesRecordWriter(new FileHelperEngine<T>(descriptor, Encoding.UTF8, LegacyWriterPaths)), "the LegacyWriter FileHelperEngine has a record writer");
            Require(!WritePathProbe.UsesRecordWriter(legacyAsync), "the LegacyWriter FileHelperAsyncEngine has a record writer");
            Require(!WritePathProbe.UsesRecordWriter((MultiRecordType<T>)MultiRecordType.For<T>(static _ => true, LegacyWriterPaths)), "the LegacyWriter MultiRecordType has a record writer");
            Require(WritePathProbe.UsesLegacyWriter(new MasterDetailEngine<T, T>(MasterFirst, LegacyWriterPaths)), "the LegacyWriter MasterDetailEngine has a record writer");
            return [.. failures];
        }
    }

    /// <summary>One shape of <see cref="Same_Text_On_Every_Engine"/>: returns the differences found.</summary>
    private sealed class SameTextOnEveryEngine(string newLine, string directory) : Compat49Matrix.ITwinVisitor<string[]>
    {
        public string[] Visit<T>(string name)
        {
            var sets = new List<(string Label, T[] Records)> { ("valid", ValidRecords<T>(name)) };
            foreach (var (label, create) in Compat49WriteCases.For<T>(name))
                sets.Add((label, [create()]));

            var newDescriptor = Checking<T>();
            var legacyDescriptor = Checking<T>();
            var adapterDescriptor = Checking<T>(legacyThroughAdapter: true);
            var failures = new List<string>();
            foreach (var (label, records) in sets)
            {
                string expected = WriteEverywhere(legacyDescriptor, LegacyWriterPaths, records);
                string actual = WriteEverywhere(newDescriptor, NewPaths, records);
                string adapter = WriteEverywhere(adapterDescriptor, LegacyWriterPaths, records);
                if (!string.Equals(expected, actual, StringComparison.Ordinal))
                    failures.Add($"{name} {label} new writer:\n  expected {expected}\n  actual   {actual}");
                if (!string.Equals(expected, adapter, StringComparison.Ordinal))
                    failures.Add($"{name} {label} adapter:\n  expected {expected}\n  actual   {adapter}");
            }

            if (newDescriptor.LegacyFormatCalls != 0 || newDescriptor.WriterFormatCalls == 0)
                failures.Add($"{name}: new path calls {newDescriptor.WriterFormatCalls} new, {newDescriptor.LegacyFormatCalls} legacy");
            if (legacyDescriptor.WriterFormatCalls != 0 || legacyDescriptor.LegacyFormatCalls == 0)
                failures.Add($"{name}: legacy path calls {legacyDescriptor.WriterFormatCalls} new, {legacyDescriptor.LegacyFormatCalls} legacy");
            if (adapterDescriptor.WriterFormatCalls != adapterDescriptor.LegacyFormatCalls)
                failures.Add($"{name}: adapter calls {adapterDescriptor.WriterFormatCalls} new, {adapterDescriptor.LegacyFormatCalls} legacy");
            return [.. failures];
        }

        /// <summary>The text of every write method and engine, each with its exception (<c>throw:</c>) after the text written before it.</summary>
        // MA0051: one section per write method, kept in one list.
#pragma warning disable MA0051
        private string WriteEverywhere<T>(IRecordDescriptor<T> descriptor, EnginePaths paths, T[] records)
        {
            var text = new StringBuilder();
            FileHelperEngine<T> Engine() => new(descriptor, Encoding.UTF8, paths) { NewLineForWrite = newLine };
            string file = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".txt");

            void Section(string label, Func<string> partial, Action write)
            {
                string error = string.Empty;
                try
                {
                    write();
                }
#pragma warning disable CA1031 // The exception is part of the compared text.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    error = "|throw:" + ex.GetType().Name + ":" + ex.Message;
                }

                text.Append(label).Append(':').Append(Escape(partial())).Append(Escape(error)).Append('\n');
            }

            string Bytes() => File.Exists(file) ? Convert.ToHexString(File.ReadAllBytes(file)) : "<no file>";

            string result = string.Empty;
            Section("WriteString", () => result, () => result = Engine().WriteString(records));

            var writer = new StringWriter();
            Section("WriteStream", () => writer.ToString(), () => Engine().WriteStream(writer, records));

            Section("WriteFile", Bytes, () => Engine().WriteFile(file, records));

            File.WriteAllText(file, "seed");
            Section("AppendToFile(record)", Bytes, () =>
            {
                var engine = Engine();
                foreach (var record in records)
                    engine.AppendToFile(file, record);
            });
            Section("AppendToFile(records)", Bytes, () => Engine().AppendToFile(file, records));

            var asyncWriter = new StringWriter();
            Section("WriteStreamAsync(TextWriter)", () => asyncWriter.ToString(), () => Engine().WriteStreamAsync(asyncWriter, records).GetAwaiter().GetResult());

            var stream = new MemoryStream();
            Section("WriteStreamAsync(Stream)", () => Convert.ToHexString(stream.ToArray()), () => Engine().WriteStreamAsync(stream, records).GetAwaiter().GetResult());

            result = string.Empty;
            Section("WriteNext", () => result, () =>
            {
                using var engine = new FileHelperAsyncEngine<T>(descriptor, paths) { NewLineForWrite = newLine };
                engine.BeginWriteString();
                foreach (var record in records)
                    engine.WriteNext(record);
                result = engine.EndWriteString();
            });

            var multiWriter = new StringWriter();
            Section("MultiRecordEngine", () => multiWriter.ToString(), () =>
                new MultiRecordEngine(new MultiRecordType<T>(static _ => true, descriptor, paths)) { NewLineForWrite = newLine }
                    .WriteStream(multiWriter, records.Cast<object>()));

            if (records.Length > 0)
            {
                var masterDetailWriter = new StringWriter();
                Section("MasterDetailEngine", () => masterDetailWriter.ToString(), () =>
                    new MasterDetailEngine<T, T>(MasterFirst, descriptor, descriptor, paths) { NewLineForWrite = newLine }
                        .WriteStream(masterDetailWriter, [new MasterDetails<T, T>(records[0], records[1..])]));
            }

            File.Delete(file);
            return text.ToString();
        }
#pragma warning restore MA0051
    }
}
