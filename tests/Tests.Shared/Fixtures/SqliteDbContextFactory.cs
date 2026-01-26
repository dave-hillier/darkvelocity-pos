using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace DarkVelocity.Tests.Shared.Fixtures;

public class SqliteDbContextFactory<TContext> : IDisposable where TContext : DbContext
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<TContext> _options;

    public SqliteDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<TContext>()
            .UseSqlite(_connection)
            .Options;
    }

    public DbContextOptions<TContext> Options => _options;

    public TContext CreateContext()
    {
        var context = (TContext)Activator.CreateInstance(typeof(TContext), _options)!;
        context.Database.EnsureCreated();
        return context;
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
