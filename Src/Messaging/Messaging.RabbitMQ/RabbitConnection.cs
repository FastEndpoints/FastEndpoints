using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace FastEndpoints.Messaging.RabbitMQ;

sealed class RabbitConnection(RabbitMQOptions options, IServiceProvider provider) : IAsyncDisposable
{
    readonly SemaphoreSlim _lock = new(1, 1);
    IConnection? _connection;
    bool _ownsConnection;

    public async Task<IConnection> GetAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true })
            return _connection;

        await _lock.WaitAsync(ct);
        try
        {
            if (_connection is { IsOpen: true })
                return _connection;

            if (_connection is not null && _ownsConnection)
                await _connection.DisposeAsync();

            var registeredConnection = provider.GetService<IConnection>();
            if (registeredConnection is { IsOpen: true })
            {
                _connection = registeredConnection;
                _ownsConnection = false;

                return _connection;
            }

            var factory = new ConnectionFactory
            {
                Uri = new(options.ConnectionString),
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true
            };
            _connection = await factory.CreateConnectionAsync(options.ClientProvidedName, ct);
            _ownsConnection = true;

            return _connection;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null && _ownsConnection)
            await _connection.DisposeAsync();
        _lock.Dispose();
    }
}
