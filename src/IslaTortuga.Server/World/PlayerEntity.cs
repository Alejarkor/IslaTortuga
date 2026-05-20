namespace IslaTortuga.Server.World;

public sealed class PlayerEntity : NetworkEntity
{
    private const float Speed = 140f;
    private float _moveX;
    private float _moveY;

    public PlayerEntity(string entityId, string userId, string displayName, float x, float y)
        : base(entityId, "player", x, y)
    {
        UserId = userId;
        DisplayName = displayName;
    }

    public string UserId { get; }

    public string DisplayName { get; }

    public string Facing { get; private set; } = "down";

    public void ApplyInput(float moveX, float moveY)
    {
        _moveX = Math.Clamp(moveX, -1f, 1f);
        _moveY = Math.Clamp(moveY, -1f, 1f);

        if (Math.Abs(_moveX) > Math.Abs(_moveY))
        {
            Facing = _moveX >= 0 ? "right" : "left";
        }
        else if (Math.Abs(_moveY) > 0)
        {
            Facing = _moveY >= 0 ? "down" : "up";
        }
    }

    public void Tick(float deltaSeconds)
    {
        X += _moveX * Speed * deltaSeconds;
        Y += _moveY * Speed * deltaSeconds;
    }
}
