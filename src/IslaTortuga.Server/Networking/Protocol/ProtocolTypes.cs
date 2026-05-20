namespace IslaTortuga.Server.Networking.Protocol;

public static class ProtocolTypes
{
    public const string AuthJoin = "auth.join";
    public const string AuthReconnect = "auth.reconnect";
    public const string AuthAccepted = "auth.accepted";
    public const string AuthRejected = "auth.rejected";
    public const string PlayerInput = "player.input";
    public const string WorldSnapshot = "world.snapshot";
    public const string Error = "error";
    public const string Ping = "ping";
    public const string Pong = "pong";
}
