// verify-variant <path-to-ModernLibs.FileHelpers.dll>
//
// Prints "tfm=<net10.0|net11.0> runtime-async=<on|off>" for a built library and exits 0 (T08, design D6).
// The target framework comes from the assembly's [TargetFramework] attribute. Runtime async is the Async
// method-implementation flag (0x2000) of FileHelperEngine<T>.ReadFileAsync(string, Action<T>, int,
// CancellationToken), the overload that is an async method; the compiler sets that flag on async methods
// only when it compiles them with runtime async. The test RuntimeAsyncBuildTests checks the same flag.
// Exit codes: 0 printed, 1 the library is not as expected, 2 usage.
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace ModernLibs.FileHelpers.Tools.VerifyVariant;

internal static class Program
{
    private const MethodImplAttributes AsyncFlag = (MethodImplAttributes)0x2000;
    private const string TargetFrameworkPrefix = ".NETCoreApp,Version=v";

    private static readonly string[] ReadFileAsyncParameters =
        ["System.String", "System.Action`1<!0>", "System.Int32", "System.Threading.CancellationToken"];

    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("usage: verify-variant <path-to-ModernLibs.FileHelpers.dll>");
            return 2;
        }

        try
        {
            using var stream = File.OpenRead(args[0]);
            using var pe = new PEReader(stream);
            var reader = pe.GetMetadataReader();
            string tfm = ReadTargetFramework(reader);
            bool runtimeAsync = FindReadFileAsync(reader).ImplAttributes.HasFlag(AsyncFlag);
            Console.WriteLine($"tfm={tfm} runtime-async={(runtimeAsync ? "on" : "off")}");
            return 0;
        }
        catch (Exception ex) when (ex is InvalidDataException or BadImageFormatException or IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"verify-variant: {args[0]}: {ex.Message}");
            return 1;
        }
    }

    private static string ReadTargetFramework(MetadataReader reader)
    {
        foreach (var handle in reader.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = reader.GetCustomAttribute(handle);
            if (!IsAttribute(reader, attribute, "System.Runtime.Versioning", "TargetFrameworkAttribute"))
                continue;

            var blob = reader.GetBlobReader(attribute.Value);
            if (blob.ReadUInt16() != 1)
                throw new InvalidDataException("TargetFrameworkAttribute has an invalid blob prolog");
            string name = blob.ReadSerializedString() ?? "";
            if (!name.StartsWith(TargetFrameworkPrefix, StringComparison.Ordinal))
                throw new InvalidDataException($"unexpected target framework '{name}'");
            return "net" + name[TargetFrameworkPrefix.Length..];
        }

        throw new InvalidDataException("no TargetFrameworkAttribute");
    }

    private static bool IsAttribute(MetadataReader reader, CustomAttribute attribute, string ns, string name)
    {
        EntityHandle type;
        if (attribute.Constructor.Kind == HandleKind.MemberReference)
            type = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent;
        else if (attribute.Constructor.Kind == HandleKind.MethodDefinition)
            type = reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType();
        else
            return false;

        return type.Kind switch
        {
            HandleKind.TypeReference => reader.GetTypeReference((TypeReferenceHandle)type) is var r
                && Is(reader, r.Namespace, ns) && Is(reader, r.Name, name),
            HandleKind.TypeDefinition => reader.GetTypeDefinition((TypeDefinitionHandle)type) is var d
                && Is(reader, d.Namespace, ns) && Is(reader, d.Name, name),
            _ => false,
        };
    }

    private static bool Is(MetadataReader reader, StringHandle handle, string value) =>
        string.Equals(reader.GetString(handle), value, StringComparison.Ordinal);

    private static MethodDefinition FindReadFileAsync(MetadataReader reader)
    {
        var provider = new SignatureNames();
        foreach (var typeHandle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(typeHandle);
            if (!Is(reader, type.Namespace, "ModernLibs.FileHelpers")
                || !Is(reader, type.Name, "FileHelperEngine`1"))
            {
                continue;
            }

            foreach (var methodHandle in type.GetMethods())
            {
                var method = reader.GetMethodDefinition(methodHandle);
                if (!Is(reader, method.Name, "ReadFileAsync"))
                    continue;
                var signature = method.DecodeSignature(provider, genericContext: null);
                if (signature.ParameterTypes.SequenceEqual(ReadFileAsyncParameters, StringComparer.Ordinal))
                    return method;
            }
        }

        throw new InvalidDataException(
            "no ModernLibs.FileHelpers.FileHelperEngine`1.ReadFileAsync(string, Action<T>, int, CancellationToken)");
    }

    /// <summary>Decodes signature types to names such as <c>System.Action`1&lt;!0&gt;</c>, enough to pick one overload.</summary>
    private sealed class SignatureNames : ISignatureTypeProvider<string, object?>
    {
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
        {
            PrimitiveTypeCode.String => "System.String",
            PrimitiveTypeCode.Int32 => "System.Int32",
            _ => "System." + typeCode,
        };

        public string GetTypeFromDefinition(MetadataReader metadata, TypeDefinitionHandle handle, byte rawTypeKind)
        {
            var type = metadata.GetTypeDefinition(handle);
            return metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name);
        }

        public string GetTypeFromReference(MetadataReader metadata, TypeReferenceHandle handle, byte rawTypeKind)
        {
            var type = metadata.GetTypeReference(handle);
            return metadata.GetString(type.Namespace) + "." + metadata.GetString(type.Name);
        }

        public string GetTypeFromSpecification(MetadataReader metadata, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) =>
            metadata.GetTypeSpecification(handle).DecodeSignature(this, genericContext);

        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) =>
            genericType + "<" + string.Join(",", typeArguments) + ">";

        public string GetGenericTypeParameter(object? genericContext, int index) => "!" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public string GetGenericMethodParameter(object? genericContext, int index) => "!!" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

        public string GetSZArrayType(string elementType) => elementType + "[]";

        public string GetArrayType(string elementType, ArrayShape shape) => elementType + "[" + new string(',', shape.Rank - 1) + "]";

        public string GetByReferenceType(string elementType) => elementType + "&";

        public string GetPointerType(string elementType) => elementType + "*";

        public string GetPinnedType(string elementType) => elementType;

        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => unmodifiedType;

        public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr";
    }
}
