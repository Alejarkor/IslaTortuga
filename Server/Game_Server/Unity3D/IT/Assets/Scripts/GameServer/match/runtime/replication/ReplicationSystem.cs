using System.Collections.Generic;
using System.Text;
using IslaTortuga.GameServer.Control;

namespace IslaTortuga.GameServer.Runtime
{
    /// <summary>
    /// Construye el STATE_DELTA del tick: solo las entidades cuya posición ha cambiado
    /// respecto al último envío (delta, no snapshot completo). Mundo 3D (x,y,z). El
    /// filtro por interés (AOI) llegará después; de momento incluye a todos.
    /// </summary>
    public sealed class ReplicationSystem
    {
        private readonly Dictionary<string, Vector3Key> _lastSent = new Dictionary<string, Vector3Key>();
        private const float Epsilon = 1e-4f;

        private readonly struct Vector3Key
        {
            public readonly float X, Y, Z;
            public Vector3Key(float x, float y, float z) { X = x; Y = y; Z = z; }
        }

        /// <summary>
        /// Devuelve el mensaje STATE_DELTA con las entidades cambiadas, o null si no
        /// cambió nada (para no enviar tráfico inútil).
        /// </summary>
        public string BuildDelta(NetworkWorld world, long serverTick)
        {
            var changed = new List<NetworkEntity>();
            foreach (var e in world.All())
            {
                var p = e.Position;
                if (!_lastSent.TryGetValue(e.Id, out var last) ||
                    Diff(last.X, p.X) || Diff(last.Y, p.Y) || Diff(last.Z, p.Z))
                {
                    changed.Add(e);
                    _lastSent[e.Id] = new Vector3Key(p.X, p.Y, p.Z);
                }
            }

            if (changed.Count == 0)
            {
                return null;
            }

            var sb = new StringBuilder();
            sb.Append("{\"type\":\"STATE_DELTA\",\"payload\":{\"serverTick\":")
              .Append(serverTick)
              .Append(",\"entities\":[");
            for (int i = 0; i < changed.Count; i++)
            {
                if (i > 0) sb.Append(',');
                var e = changed[i];
                sb.Append("{\"id\":").Append(Json.Str(e.Id))
                  .Append(",\"x\":").Append(Json.Num((double)e.Position.X))
                  .Append(",\"y\":").Append(Json.Num((double)e.Position.Y))
                  .Append(",\"z\":").Append(Json.Num((double)e.Position.Z))
                  .Append('}');
            }
            sb.Append("],\"events\":[]}}");
            return sb.ToString();
        }

        private static bool Diff(float a, float b)
        {
            var d = a - b;
            return d > Epsilon || d < -Epsilon;
        }
    }
}
