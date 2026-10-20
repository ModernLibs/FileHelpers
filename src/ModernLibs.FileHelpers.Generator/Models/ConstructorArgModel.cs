namespace ModernLibs.FileHelpers.Generator.Models;

/// <summary>
/// One constructor argument. <see cref="FieldIndex"/> is the index into
/// <see cref="RecordModel.Fields"/> whose parsed value is passed, or -1 when
/// <see cref="DefaultExpression"/> is passed instead (hidden or discarded members).
/// </summary>
internal sealed record ConstructorArgModel(string ParameterIdentifier, int FieldIndex, string? DefaultExpression);
