// A net10.0 library (Lib10) with generated descriptors, run by a net11.0 app: the app must get lib/net11.0.
var orders = Lib10.Api.Read("1,\"a,b\",01112026\r\n2,c,02112026\r\n");
if (orders.Length != 2 || orders[0].Name != "a,b") return 1;
string text = Lib10.Api.Write(orders);
// [FieldQuoted] always quotes on write, so "c" comes back as "\"c\"".
if (text != "1,\"a,b\",01112026\r\n2,\"c\",02112026\r\n")
{
    Console.Error.WriteLine("unexpected text: " + text.Replace("\r", "\\r").Replace("\n", "\\n"));
    return 2;
}
var asm = typeof(ModernLibs.FileHelpers.FileHelperEngine<>).Assembly;
var tfm = asm.GetCustomAttributes(typeof(System.Runtime.Versioning.TargetFrameworkAttribute), false)
             .Cast<System.Runtime.Versioning.TargetFrameworkAttribute>().Single().FrameworkName;
return tfm == ".NETCoreApp,Version=v11.0" ? 0 : 3;   // the app must have loaded lib/net11.0
