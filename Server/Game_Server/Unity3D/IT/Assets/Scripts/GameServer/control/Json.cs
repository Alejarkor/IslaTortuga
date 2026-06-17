using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace IslaTortuga.GameServer.Control
{
    /// <summary>
    /// Mini-serializador JSON sin dependencias externas. La ControlApi solo necesita
    /// emitir objetos planos pequeños (health, capacity), así que evitamos arrastrar
    /// Newtonsoft o depender de JsonUtility de Unity (que no serializa diccionarios).
    /// No es un serializador de propósito general; cubre strings, números y bools.
    /// </summary>
    internal static class Json
    {
        public static string Object(IEnumerable<KeyValuePair<string, string>> rawFields)
        {
            var sb = new StringBuilder();
            sb.Append('{');
            var first = true;
            foreach (var field in rawFields)
            {
                if (!first)
                {
                    sb.Append(',');
                }
                first = false;
                sb.Append(EscapeString(field.Key));
                sb.Append(':');
                sb.Append(field.Value); // value already formatted as JSON token
            }
            sb.Append('}');
            return sb.ToString();
        }

        public static string Str(string value) => EscapeString(value);

        public static string Num(long value) => value.ToString(CultureInfo.InvariantCulture);

        public static string Num(double value) =>
            value.ToString("R", CultureInfo.InvariantCulture);

        public static string Bool(bool value) => value ? "true" : "false";

        private static string EscapeString(string value)
        {
            if (value == null)
            {
                return "null";
            }

            var sb = new StringBuilder(value.Length + 2);
            sb.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20)
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.Append(c);
                        }
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }
    }
}
