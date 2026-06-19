using System.Collections.Generic;
using System.Numerics;

namespace IslaTortuga.GameServer.Runtime
{
    /// <summary>
    /// De quién se aceptan las intenciones sobre la entidad. El servidor valida
    /// siempre; este enum solo dice "a quién se le hace caso". Se usa de verdad a
    /// partir de las Fases 4 (spawn) y 7 (autoridades).
    /// </summary>
    public enum Authority
    {
        Server = 0, // mundo, reglas, items
        Owner = 1,  // la entidad del propio jugador
        Master = 2  // rol con acciones reservadas
    }

    /// <summary>
    /// Entidad de red en un mundo 3D. El servidor solo maneja identificadores y
    /// estado (nunca binarios de asset): el cliente resuelve el prefabId por su
    /// manifest. Posición en X,Y,Z y rotación en cuaternión.
    /// </summary>
    public sealed class NetworkEntity
    {
        public string Id { get; }
        public string PrefabId { get; }

        public Vector3 Position { get; set; }
        public Quaternion Rotation { get; set; }

        public Authority Authority { get; set; }
        public string OwnerId { get; set; }

        /// <summary>Estado lógico arbitrario (p. ej. { "opened": false, "locked": true }).</summary>
        public Dictionary<string, object> State { get; }

        public NetworkEntity(
            string id,
            string prefabId,
            Authority authority = Authority.Server,
            string ownerId = null)
        {
            Id = id;
            PrefabId = prefabId;
            Authority = authority;
            OwnerId = ownerId;
            Position = Vector3.Zero;
            Rotation = Quaternion.Identity; // (0,0,0,1), no el default (0,0,0,0)
            State = new Dictionary<string, object>();
        }
    }
}
