using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DarkVelocity.Tests.Shared.Fixtures;

public class ServiceTestFixture<TProgram, TContext> : WebApplicationFactory<TProgram>, IAsyncLifetime
    where TProgram : class
    where TContext : DbContext
{
    private SqliteConnection? _connection;

    public HttpClient Client { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureTestServices(services =>
        {
            // Remove existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<TContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Remove the DbContext itself
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(TContext));

            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // Create SQLite in-memory connection
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Add SQLite DbContext
            services.AddDbContext<TContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // Ensure database is created
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TContext>();
            db.Database.EnsureCreated();
        });

        builder.UseEnvironment("Test");
    }

    public Task InitializeAsync()
    {
        Client = CreateClient();
        return Task.CompletedTask;
    }

    public new Task DisposeAsync()
    {
        Client?.Dispose();
        _connection?.Dispose();
        return Task.CompletedTask;
    }

    public TContext GetDbContext()
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<TContext>();
    }

    public T GetService<T>() where T : notnull
    {
        var scope = Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<T>();
    }
}
