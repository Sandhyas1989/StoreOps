namespace StoreOps.Api.Tests.Infrastructure;

/// <summary>
/// xUnit creates a new test-class instance per test, so every test gets a fresh API and empty in-memory stores.
/// This lets tests assert exact counts instead of tolerating state left by other tests.
/// </summary>
public abstract class ApiTestBase : IDisposable
{
    protected ApiTestBase()
    {
        Factory = new StoreOpsApiFactory();
        Client = Factory.CreateClient();
    }

    protected StoreOpsApiFactory Factory { get; }

    protected HttpClient Client { get; }

    public void Dispose()
    {
        Client.Dispose();
        Factory.Dispose();
        GC.SuppressFinalize(this);
    }
}
