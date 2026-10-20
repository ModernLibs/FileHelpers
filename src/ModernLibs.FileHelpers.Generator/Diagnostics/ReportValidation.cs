using Microsoft.CodeAnalysis;
using ModernLibs.FileHelpers.Generator.Models;

namespace ModernLibs.FileHelpers.Generator.Diagnostics;

/// <summary>Receives one validation diagnostic from <see cref="RecordValidator"/>.</summary>
internal delegate void ReportValidation(DiagnosticDescriptor descriptor, LocationInfo? location, params object[] args);
