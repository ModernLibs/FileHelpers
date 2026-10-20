// CA1707: Test method names use underscores intentionally.
#pragma warning disable CA1707
// MA0004: ConfigureAwait not needed in test bodies.
#pragma warning disable MA0004
// CA2007: ConfigureAwait not needed in test code.
#pragma warning disable CA2007

using ModernLibs.FileHelpers.Tests.Engine.Fixtures;
using TUnit.Core;

namespace ModernLibs.FileHelpers.Tests.MasterDetail;

/// <summary>
/// CommonSelector tests — factory methods and enum constructor overloads.
/// </summary>
public class CommonSelectorTests
{
    [Test]
    [DisplayName("BeginsWith classifies ORD lines as master")]
    public async Task BeginsWith_ClassifiesMaster()
    {
        // Arrange: selector that marks lines starting with "ORD" as master
        var selector = CommonSelector.BeginsWith("ORD");

        // Act: classify lines
        var masterResult = selector("ORD,1,Acme Corp".AsSpan());
        var detailResult = selector("LIN,1,Widget,9.99".AsSpan());

        // Assert: correct classification
        await Assert.That(masterResult).IsEqualTo(RecordAction.Master);
        await Assert.That(detailResult).IsEqualTo(RecordAction.Detail);
    }

    [Test]
    [DisplayName("EndsWith classifies END lines as master")]
    public async Task EndsWith_ClassifiesMaster()
    {
        // Arrange: selector that marks lines ending with "END" as master
        var selector = CommonSelector.EndsWith("END");

        // Act: classify lines
        var masterResult = selector("ORDER,1,Acme Corp,END".AsSpan());
        var detailResult = selector("LIN,1,Widget,9.99".AsSpan());

        // Assert: correct classification
        await Assert.That(masterResult).IsEqualTo(RecordAction.Master);
        await Assert.That(detailResult).IsEqualTo(RecordAction.Detail);
    }

    [Test]
    [DisplayName("Contains classifies HEADER lines as master")]
    public async Task Contains_ClassifiesMaster()
    {
        // Arrange: selector that marks lines containing "HEADER" as master
        var selector = CommonSelector.Contains("HEADER");

        // Act: classify lines
        var masterResult = selector("HEADER,1,Acme Corp".AsSpan());
        var detailResult = selector("LIN,1,Widget,9.99".AsSpan());

        // Assert: correct classification
        await Assert.That(masterResult).IsEqualTo(RecordAction.Master);
        await Assert.That(detailResult).IsEqualTo(RecordAction.Detail);
    }

    [Test]
    [DisplayName("Equals classifies exact match as master")]
    public async Task Equals_ClassifiesMaster()
    {
        // Arrange: selector that marks exact match lines as master
        var selector = CommonSelector.Equals("SECTION_START");

        // Act: classify lines
        var masterResult = selector("SECTION_START".AsSpan());
        var detailResult = selector("SECTION_START_EXTRA".AsSpan());

        // Assert: correct classification
        await Assert.That(masterResult).IsEqualTo(RecordAction.Master);
        await Assert.That(detailResult).IsEqualTo(RecordAction.Detail);
    }

    [Test]
    [DisplayName("HasLengthLessThan classifies short lines as master")]
    public async Task HasLengthLessThan_ClassifiesMaster()
    {
        // Arrange: selector that marks lines shorter than 20 chars as master
        var selector = CommonSelector.HasLengthLessThan(20);

        // Act: classify lines
        var masterResult = selector("HDR,1,Acme".AsSpan()); // 12 chars
        var detailResult = selector("LIN,1,Widget,9.99,Extra data here".AsSpan()); // 33 chars

        // Assert: correct classification
        await Assert.That(masterResult).IsEqualTo(RecordAction.Master);
        await Assert.That(detailResult).IsEqualTo(RecordAction.Detail);
    }

    [Test]
    [DisplayName("HasLengthMoreThan classifies long lines as master")]
    public async Task HasLengthMoreThan_ClassifiesMaster()
    {
        // Arrange: selector that marks lines longer than 20 chars as master
        var selector = CommonSelector.HasLengthMoreThan(20);

        // Act: classify lines
        var masterResult = selector("LIN,1,Widget,9.99,Extra data here".AsSpan()); // 33 chars
        var detailResult = selector("HDR,1,Acme".AsSpan()); // 12 chars

        // Assert: correct classification
        await Assert.That(masterResult).IsEqualTo(RecordAction.Master);
        await Assert.That(detailResult).IsEqualTo(RecordAction.Detail);
    }

    [Test]
    [DisplayName("MasterIfContains mode works with MasterDetailEngine")]
    public async Task MasterIfContains_Mode_Works()
    {
        // Arrange: engine with MasterIfContains mode
        var engine = new MasterDetailEngine<OrderHeader, OrderLine>(
            CommonSelectorMode.MasterIfContains, "ORD");

        const string input = """
            ORD,1,Acme Corp
            LIN,1,Widget,9.99
            LIN,2,Gadget,19.99
            ORD,2,Globex
            LIN,1,Thingamajig,29.99
            """;

        // Act: read grouped records
        var result = engine.ReadString(input);

        // Assert: two groups with correct masters and details
        await Assert.That(result.Length).IsEqualTo(2);
        await Assert.That(result[0].Master.OrderId).IsEqualTo(1);
        await Assert.That(result[0].Details.Count).IsEqualTo(2);
        await Assert.That(result[1].Master.OrderId).IsEqualTo(2);
        await Assert.That(result[1].Details.Count).IsEqualTo(1);
    }

    [Test]
    [DisplayName("MasterIfBegins mode works with MasterDetailEngine")]
    public async Task MasterIfBegins_Mode_Works()
    {
        // Arrange: engine with MasterIfBegins mode
        var engine = new MasterDetailEngine<OrderHeader, OrderLine>(
            CommonSelectorMode.MasterIfBegins, "ORD");

        const string input = """
            ORD,1,Acme Corp
            LIN,1,Widget,9.99
            ORD,2,Globex
            LIN,1,Gadget,19.99
            """;

        // Act: read grouped records
        var result = engine.ReadString(input);

        // Assert: two groups
        await Assert.That(result.Length).IsEqualTo(2);
        await Assert.That(result[0].Master.OrderId).IsEqualTo(1);
        await Assert.That(result[0].Details.Count).IsEqualTo(1);
        await Assert.That(result[1].Master.OrderId).IsEqualTo(2);
        await Assert.That(result[1].Details.Count).IsEqualTo(1);
    }

    [Test]
    [DisplayName("DetailIfContains mode inverts classification")]
    public async Task DetailIfContains_Mode_Inverts()
    {
        // Arrange: engine with DetailIfContains mode (detail lines contain "LIN")
        var engine = new MasterDetailEngine<OrderHeader, OrderLine>(
            CommonSelectorMode.DetailIfContains, "LIN");

        const string input = """
            ORD,1,Acme Corp
            LIN,1,Widget,9.99
            LIN,2,Gadget,19.99
            """;

        // Act: read grouped records
        var result = engine.ReadString(input);

        // Assert: one group — ORD is master (doesn't contain LIN), LINs are details
        await Assert.That(result.Length).IsEqualTo(1);
        await Assert.That(result[0].Master.OrderId).IsEqualTo(1);
        await Assert.That(result[0].Details.Count).IsEqualTo(2);
    }

    [Test]
    [DisplayName("CommonSelector full integration with MasterDetailEngine")]
    public async Task CommonSelector_FullIntegration()
    {
        // Arrange: engine using factory method selector
        var engine = new MasterDetailEngine<OrderHeader, OrderLine>(
            CommonSelector.BeginsWith("ORD"));

        const string input = """
            ORD,1,Acme Corp
            LIN,1,Widget,9.99
            LIN,2,Gadget,19.99
            ORD,2,Globex
            LIN,1,Thingamajig,29.99
            LIN,2,Doohickey,39.99
            """;

        // Act: read grouped records
        var result = engine.ReadString(input);

        // Assert: two complete groups
        await Assert.That(result.Length).IsEqualTo(2);
        await Assert.That(result[0].Master.OrderId).IsEqualTo(1);
        await Assert.That(result[0].Master.CustomerName).IsEqualTo("Acme Corp");
        await Assert.That(result[0].Details.Count).IsEqualTo(2);
        await Assert.That(result[0].Details[0].Product).IsEqualTo("Widget");
        await Assert.That(result[0].Details[0].Price).IsEqualTo(9.99m);
        await Assert.That(result[0].Details[1].Product).IsEqualTo("Gadget");
        await Assert.That(result[0].Details[1].Price).IsEqualTo(19.99m);
        await Assert.That(result[1].Master.OrderId).IsEqualTo(2);
        await Assert.That(result[1].Master.CustomerName).IsEqualTo("Globex");
        await Assert.That(result[1].Details.Count).IsEqualTo(2);
        await Assert.That(result[1].Details[0].Product).IsEqualTo("Thingamajig");
        await Assert.That(result[1].Details[1].Product).IsEqualTo("Doohickey");
    }
}
