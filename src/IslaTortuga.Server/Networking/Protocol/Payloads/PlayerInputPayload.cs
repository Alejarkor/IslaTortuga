namespace IslaTortuga.Server.Networking.Protocol.Payloads;

public sealed record PlayerInputPayload(float MoveX, float MoveY, int Sequence = 0);
