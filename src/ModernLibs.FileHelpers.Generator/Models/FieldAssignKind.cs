namespace ModernLibs.FileHelpers.Generator.Models;

/// <summary>How a parsed field value is stored in the record instance.</summary>
internal enum FieldAssignKind
{
    /// <summary>Regular field or property setter — object initializer and ParseInto.</summary>
    Setter = 0,

    /// <summary>Init-only property — object initializer only.</summary>
    Init,

    /// <summary>Bound to a constructor parameter.</summary>
    Constructor,

    /// <summary>Not stored ([FieldValueDiscarded] member that cannot be assigned).</summary>
    None,
}
