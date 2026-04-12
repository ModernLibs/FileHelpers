namespace ModernLibs.FileHelpers.Generator.Emission;

/// <summary>
/// Placeholder for registration-specific emission variants in Phase 4+.
/// In Phase 3 the registration code is combined into the descriptor file via
/// <see cref="DescriptorEmitter.Emit"/> — a single file avoids file-ordering non-determinism.
/// This class becomes the home for FixedLength or multi-path registration in Phase 4+.
/// </summary>
internal static class RegistrationEmitter
{
    // Phase 3: registration emitted inline by DescriptorEmitter.Emit.
    // This class exists to keep the emission architecture extensible without restructuring.
}
