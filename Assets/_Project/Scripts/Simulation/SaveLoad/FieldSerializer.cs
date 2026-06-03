using System;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace FoundersLands.Simulation.SaveLoad
{
    /// <summary>
    /// Reflection serializer for plain "settings bag" objects whose public fields are
    /// int / float / bool / enum (WorldGenSettings, SettlementConfig). Emitting fields
    /// sorted by name keeps the output stable, and going through reflection means adding a
    /// balance field does not require touching the save format by hand. Unknown fields on
    /// load are ignored, so older saves still load against a newer build.
    /// </summary>
    internal static class FieldSerializer
    {
        public static void Write(StringBuilder sb, string tag, object obj)
        {
            FieldInfo[] fields = obj.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            Array.Sort(fields, (a, b) => string.CompareOrdinal(a.Name, b.Name));
            for (int i = 0; i < fields.Length; i++)
            {
                FieldInfo f = fields[i];
                if (!IsSupported(f.FieldType)) continue;
                sb.Append(tag).Append(' ').Append(f.Name).Append(' ')
                  .Append(Format(f.GetValue(obj), f.FieldType)).Append('\n');
            }
        }

        public static void Apply(object obj, string fieldName, string raw)
        {
            FieldInfo f = obj.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (f == null || !IsSupported(f.FieldType)) return;
            f.SetValue(obj, Parse(raw, f.FieldType));
        }

        private static bool IsSupported(Type t)
        {
            return t == typeof(int) || t == typeof(float) || t == typeof(bool) || t.IsEnum;
        }

        private static string Format(object v, Type t)
        {
            if (t == typeof(float)) return ((float)v).ToString("R", CultureInfo.InvariantCulture);
            if (t == typeof(bool)) return ((bool)v) ? "1" : "0";
            if (t.IsEnum) return ((int)v).ToString(CultureInfo.InvariantCulture);
            return ((int)v).ToString(CultureInfo.InvariantCulture);
        }

        private static object Parse(string s, Type t)
        {
            if (t == typeof(float)) return float.Parse(s, CultureInfo.InvariantCulture);
            if (t == typeof(bool)) return s == "1";
            if (t.IsEnum) return Enum.ToObject(t, int.Parse(s, CultureInfo.InvariantCulture));
            return int.Parse(s, CultureInfo.InvariantCulture);
        }
    }
}
