using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

namespace SupplierPortal.Web.Services;

/// <summary>Reflection helper that lets the config-driven customer form read/write any field by its key
/// (the entity property name) without hand-wiring 133 bindings. Handles the field kinds used by the sheet:
/// text/lookup (string), date (DateTime?) and yes/no (bool?).</summary>
public static class CustomerFieldAccessor
{
    private static readonly ConcurrentDictionary<string, PropertyInfo?> Cache = new();

    private static PropertyInfo? Prop(object entity, string key) =>
        Cache.GetOrAdd($"{entity.GetType().FullName}.{key}",
            _ => entity.GetType().GetProperty(key, BindingFlags.Public | BindingFlags.Instance));

    /// <summary>Current value as a form-ready string: dates as yyyy-MM-dd, bools as "true"/"false".</summary>
    public static string? GetString(object entity, string key)
    {
        var value = Prop(entity, key)?.GetValue(entity);
        return value switch
        {
            null => null,
            DateTime d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            bool b => b ? "true" : "false",
            _ => value.ToString()
        };
    }

    /// <summary>A human-friendly display string for read-only rendering.</summary>
    public static string GetDisplay(object entity, string key)
    {
        var value = Prop(entity, key)?.GetValue(entity);
        return value switch
        {
            null => "—",
            DateTime d => d.ToString("dd MMM yyyy", CultureInfo.InvariantCulture),
            bool b => b ? "Yes" : "No",
            _ => string.IsNullOrWhiteSpace(value.ToString()) ? "—" : value.ToString()!
        };
    }

    /// <summary>Writes a raw string from the form into the property, converting to the target type.</summary>
    public static void SetString(object entity, string key, string? raw)
    {
        var prop = Prop(entity, key);
        if (prop is null || !prop.CanWrite) return;
        var t = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

        if (string.IsNullOrWhiteSpace(raw))
        {
            prop.SetValue(entity, prop.PropertyType.IsValueType && Nullable.GetUnderlyingType(prop.PropertyType) is null
                ? Activator.CreateInstance(prop.PropertyType) : null);
            return;
        }

        if (t == typeof(string)) { prop.SetValue(entity, raw); return; }
        if (t == typeof(DateTime))
        {
            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                prop.SetValue(entity, d);
            return;
        }
        if (t == typeof(bool))
        {
            var v = raw.Trim().ToLowerInvariant();
            prop.SetValue(entity, v is "true" or "yes" or "1");
            return;
        }
        // fallback: assign as string if the property is object/other
        prop.SetValue(entity, raw);
    }
}
