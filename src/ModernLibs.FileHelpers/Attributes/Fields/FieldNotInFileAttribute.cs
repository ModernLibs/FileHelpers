namespace ModernLibs.FileHelpers;

/// <summary>
/// Obsolete alias for <see cref="FieldHiddenAttribute"/>. Use <see cref="FieldHiddenAttribute"/> instead.
/// </summary>
[AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
[Obsolete("Use [FieldHidden] instead.", error: false)]
public sealed class FieldNotInFileAttribute : Attribute { }
