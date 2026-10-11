using Microsoft.Extensions.Options;
using StoreOps.Api.Tests.Infrastructure;

namespace StoreOps.Api.Tests.Activities;

public sealed class SlaOptionsTests
{
    [Theory]
    [InlineData("00:00:00")]
    [InlineData("-00:01:00")]
    public void Non_positive_grace_period_fails_startup(string gracePeriod)
    {
        using var factory = new StoreOpsApiFactory();
        using var configured = factory.WithWebHostBuilder(builder => builder.UseSetting("Sla:EscalationGracePeriod", gracePeriod));

        var failure = Assert.Throws<OptionsValidationException>(() => configured.CreateClient());

        Assert.Contains("EscalationGracePeriod", failure.Message);
    }
}
