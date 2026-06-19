using System;
using System.Numerics;

namespace IslaTortuga.GameServer.Runtime
{
    /// <summary>
    /// Aplica los inputs a las entidades de tipo OWNER, de forma autoritativa: ignora
    /// cualquier posición que declare el cliente y recalcula la suya a partir de la
    /// intención (dirección normalizada * velocidad * dt). Mundo 3D: movimiento en el
    /// plano X/Z (Y es la vertical).
    /// </summary>
    public sealed class MovementSystem
    {
        public float Speed { get; set; } = 5f; // unidades por segundo

        public void Apply(NetworkWorld world, InputSystem inputs, float dt)
        {
            foreach (var e in world.All())
            {
                if (e.Authority != Authority.Owner || string.IsNullOrEmpty(e.OwnerId))
                {
                    continue;
                }
                var input = inputs.Get(e.OwnerId);
                if (input == null)
                {
                    continue;
                }

                float dx = input.MoveX;
                float dz = input.MoveZ;
                float len = (float)Math.Sqrt(dx * dx + dz * dz);
                if (len <= 1e-4f)
                {
                    continue; // sin intención de movimiento
                }
                dx /= len;
                dz /= len;

                e.Position = new Vector3(
                    e.Position.X + dx * Speed * dt,
                    e.Position.Y,
                    e.Position.Z + dz * Speed * dt);
            }
        }
    }
}
