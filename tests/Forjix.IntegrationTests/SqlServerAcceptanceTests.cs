namespace Forjix.IntegrationTests;

public sealed class SqlServerAcceptanceTests
{
    [Fact(Skip = "Requires a real SQL Server: run the documented migrator twice and the HTTP authentication flow.")]
    public void DevelopmentSeedIsIdempotentAgainstSqlServer()
    {
        // Deliberately not replaced by an in-memory provider: migrations and physical
        // tenant isolation must be accepted against the real SQL Server engine.
    }
}
