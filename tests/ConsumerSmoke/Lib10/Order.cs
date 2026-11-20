using ModernLibs.FileHelpers;

namespace Lib10;

[DelimitedRecord(",")]
public sealed class Order
{
    public int Id;

    [FieldQuoted]
    public string Name = "";

    public DateTime Day;
}

public static class Api
{
    public static Order[] Read(string s) => new FileHelperEngine<Order>().ReadString(s);

    public static string Write(Order[] o) => new FileHelperEngine<Order>().WriteString(o);
}
