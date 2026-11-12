// MA0048: Fixture types grouped in one file.
#pragma warning disable MA0048
// MLFH001: these record types have no record attributes on purpose: their descriptors are the checked-in
// 4.9.1 output in Generated49, so the current generator must not make a second one.
#pragma warning disable MLFH001

namespace ModernLibs.FileHelpers.Tests.Compat;

// Each type has the members of its `...Twin` in Compat49Twins.cs, in the same order, without attributes.

/// <summary>The members of <see cref="Compat49Plain25Twin"/>; descriptor from 4.9.1.</summary>
public sealed class Compat49Plain25
{
    public int Id { get; set; }
    public string Version { get; set; } = string.Empty;
    public string Authors { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public int DownloadCount { get; set; }
    public string ProjectUrl { get; set; } = string.Empty;
    public string LicenseUrl { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Framework { get; set; } = string.Empty;
    public string MinClientVersion { get; set; } = string.Empty;
    public int Type { get; set; }
    public bool IsPrerelease { get; set; }
    public DateTime Published { get; set; }
    public DateTime LastUpdated { get; set; }
    public long PackageSize { get; set; }
    public int DependencyCount { get; set; }
    public bool HasReadme { get; set; }
    public bool HasLicense { get; set; }
    public bool HasIcon { get; set; }
    public bool IsSigned { get; set; }
    public string RepositoryType { get; set; } = string.Empty;
    public string RepositoryUrl { get; set; } = string.Empty;
    public bool RequireLicenseAcceptance { get; set; }
}

/// <summary>The members of <see cref="Compat49OptionalTwin"/>; descriptor from 4.9.1.</summary>
public sealed class Compat49Optional
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Note { get; set; }
    public int? Extra { get; set; }
}

/// <summary>The members of <see cref="Compat49PerFieldDelimiterTwin"/>; descriptor from 4.9.1.</summary>
public sealed class Compat49PerFieldDelimiter
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Tail { get; set; } = string.Empty;
}

/// <summary>The members of <see cref="Compat49NewLineTwin"/>; descriptor from 4.9.1.</summary>
public sealed class Compat49NewLine
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public int Zip { get; set; }
}

/// <summary>The members of <see cref="Compat49TrimTwin"/>; descriptor from 4.9.1.</summary>
public sealed class Compat49Trim
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int Count { get; set; }
}

/// <summary>The members of <see cref="Compat49QuoteAlwaysTwin"/>; descriptor from 4.9.1.</summary>
public sealed class Compat49QuoteAlways
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Tail { get; set; } = string.Empty;
}

/// <summary>The members of <see cref="Compat49QuoteOptionalReadTwin"/>; descriptor from 4.9.1.</summary>
public sealed class Compat49QuoteOptionalRead
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Tail { get; set; } = string.Empty;
}

/// <summary>The members of <see cref="Compat49QuoteOptionalWriteTwin"/>; descriptor from 4.9.1.</summary>
public sealed class Compat49QuoteOptionalWrite
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Tail { get; set; } = string.Empty;
}

/// <summary>The members of <see cref="Compat49QuoteOptionalBothTwin"/>; descriptor from 4.9.1.</summary>
public sealed class Compat49QuoteOptionalBoth
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Tail { get; set; } = string.Empty;
}

/// <summary>The members of <see cref="Compat49MultilineNotAllowTwin"/>; descriptor from 4.9.1.</summary>
public sealed class Compat49MultilineNotAllow
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Tail { get; set; } = string.Empty;
}

/// <summary>The members of <see cref="Compat49RecyclableTwin"/>; descriptor from 4.9.1.</summary>
public sealed class Compat49Recyclable
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public bool Flag { get; set; }
}

/// <summary>The members of <see cref="Compat49StructTwin"/>; descriptor from 4.9.1.</summary>
public struct Compat49Struct
{
    public int Id { get; set; }
    public string Name { get; set; }
    public DateOnly Day { get; set; }
    public double Ratio { get; set; }
}

/// <summary>The members of <see cref="Compat49CtorTwin"/>; descriptor from 4.9.1.</summary>
public sealed class Compat49Ctor
{
    public Compat49Ctor(int id, string name, DateTime day)
    {
        Id = id;
        Name = name;
        Day = day;
    }

    public int Id { get; }
    public string Name { get; }
    public DateTime Day { get; }
}

/// <summary>The members of <see cref="Compat49FixedExactTwin"/>; descriptor from 4.9.1.</summary>
public sealed class Compat49FixedExact
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

/// <summary>The members of <see cref="Compat49FixedMoreTwin"/>; descriptor from 4.9.1.</summary>
public sealed class Compat49FixedMore
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}

/// <summary>The members of <see cref="Compat49FixedLessTwin"/>; descriptor from 4.9.1.</summary>
public sealed class Compat49FixedLess
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
}

/// <summary>The members of <see cref="Compat49FixedVariableTwin"/>; descriptor from 4.9.1.</summary>
public sealed class Compat49FixedVariable
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
}

/// <summary>The members of <see cref="Compat49ConverterBaseTwin"/>; descriptor from 4.9.1.</summary>
public sealed class Compat49ConverterBase
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Hex { get; set; }
    public string? Code { get; set; }
}

/// <summary>The members of <see cref="Compat49SpanConverterTwin"/>; descriptor from 4.9.1.</summary>
public sealed class Compat49SpanConverter
{
    public int Id { get; set; }
    public decimal Price { get; set; }
    public decimal? Discount { get; set; }
}
