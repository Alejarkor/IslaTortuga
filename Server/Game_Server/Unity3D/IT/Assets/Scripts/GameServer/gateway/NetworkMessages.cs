using System.Collections.Generic;
using System.Text;
using IslaTortuga.GameServer.Control;
using IslaTortuga.GameServer.Runtime;

namespace IslaTortuga.GameServer.Gateway
{
    /// <summary>
    /// Construye los mensajes de red de entidades (Fase 4) a partir de una
    /// NetworkEntity. Mundo 3D: posición (x,y,z) y rotación en cuaternión (x,y,z,w).
    /// Nunca incluye binarios de asset: solo ids y estado lógico (el cliente resuelve
    /// el prefab por su manifest).
    /// </summary>
    public static class NetworkMessages
    {
        public const string Spawn = "SPAWN_ENTITY";
        public const string Despawn = "DESPAWN_ENTITY";

        public static string SpawnEntity(NetworkEntity e)
        {
            var payload =
                "{" +
                "\"networkEntityId\":" + Json.Str(e.Id) + "," +
                "\"networkPrefabId\":" + Json.Str(e.PrefabId) + "," +
                "\"position\":{" +
                    "\"x\":" + Json.Num((double)e.Position.X) + "," +
                    "\"y\":" + Json.Num((double)e.Position.Y) + "," +
                    "\"z\":" + Json.Num((double)e.Position.Z) + "}," +
                "\"rotation\":{" +
                    "\"x\":" + Json.Num((double)e.Rotation.X) + "," +
                    "\"y\":" + Json.Num((double)e.Rotation.Y) + "," +
                    "\"z\":" + Json.Num((double)e.Rotation.Z) + "," +
                    "\"w\":" + Json.Num((double)e.Rotation.W) + "}," +
                "\"authority\":" + Json.Str(AuthorityName(e.Authority)) + "," +
                "\"ownerId\":" + Json.Str(e.OwnerId) + "," +
                "\"initialState\":" + SerializeState(e.State) +
                "}";
            return MessageCodec.Encode(Spawn, payload);
        }

        public static string DespawnEntity(string entityId)
        {
            return MessageCodec.Encode(Despawn, "{\"networkEntityId\":" + Json.Str(entityId) + "}");
        }

        private static string AuthorityName(Authority a)
        {
            switch (a)
            {
                case Authority.Owner: return "owner";
                case Authority.Master: return "master";
                default: return "server";
            }
        }

        private static string SerializeState(IDictionary<string, object> state)
        {
            if (state == null || state.Count == 0)
            {
                return "{}";
            }
            var sb = new StringBuilder("{");
            var first = true;
            foreach (var kv in state)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(Json.Str(kv.Key)).Append(':');
                sb.Append(SerializeValue(kv.Value));
            }
            sb.Append('}');
            return sb.ToString();
        }

        private static string SerializeValue(object v)
        {
            switch (v)
            {
                case null: return "null";
                case bool b: return Json.Bool(b);
                case string s: return Json.Str(s);
                case float f: return Json.Num((double)f);
                case double d: return Json.Num(d);
                case int i: return Json.Num((long)i);
                case long l: return Json.Num(l);
                default: return Json.Str(v.ToString());
            }
        }
    }
}
