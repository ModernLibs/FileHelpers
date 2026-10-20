namespace ModernLibs.FileHelpers.Generator.Models;

/// <summary>
/// A C# 11 <c>required</c> member the object initializer must set in addition to the mapped setters:
/// a constructor-bound member gets the parsed value of <see cref="FieldIndex"/> again, a hidden, discarded
/// or unmapped one gets <c>default!</c> (<see cref="FieldIndex"/> is -1).
/// </summary>
internal sealed record RequiredMemberModel(string Identifier, int FieldIndex);
