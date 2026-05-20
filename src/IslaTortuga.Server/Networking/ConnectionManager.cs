using System.Collections.Concurrent;
using System.Net.WebSockets;

namespace IslaTortuga.Server.Networking;

public sealed class ConnectionManager
{
    private readonly ConcurrentDictionary<string, ClientConnection> _connections = new();

    public ClientConnection Add(WebSocket socket)
    {
        var connection = new ClientConnection(socket);
        _connections[connection.ConnectionId] = connection;
        return connection;
    }

    public bool TryGet(string connectionId, out ClientConnection? connection)
    {
        return _connections.TryGetValue(connectionId, out connection);
    }

    public IReadOnlyCollection<ClientConnection> GetAll()
    {
        return _connections.Values.ToArray();
    }

    public bool Remove(string connectionId, out ClientConnection? connection)
    {
        return _connections.TryRemove(connectionId, out connection);
    }
}
