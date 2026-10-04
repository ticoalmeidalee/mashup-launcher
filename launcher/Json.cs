using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;

/// <summary>JSON through the framework's JavaScriptSerializer: objects are Dictionary&lt;string, object&gt;, arrays object[] / ArrayList.</summary>
static class Json
{
    static JavaScriptSerializer S() => new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

    public static object Parse(string text) => S().DeserializeObject(text);
    public static string Write(object value) => S().Serialize(value);

    public static IDictionary<string, object> Obj(object o, string key) =>
        o is IDictionary<string, object> d && d.TryGetValue(key, out object v) ? v as IDictionary<string, object> : null;

    public static IList Arr(object o, string key) =>
        o is IDictionary<string, object> d && d.TryGetValue(key, out object v) ? v as IList : null;

    public static string Str(object o, string key, string def = null) =>
        o is IDictionary<string, object> d && d.TryGetValue(key, out object v) && v != null ? v.ToString() : def;

    public static int Int(object o, string key, int def = 0) =>
        o is IDictionary<string, object> d && d.TryGetValue(key, out object v) && v != null && int.TryParse(v.ToString(), out int i) ? i : def;

    public static bool Bool(object o, string key, bool def = false) =>
        o is IDictionary<string, object> d && d.TryGetValue(key, out object v) && v is bool b ? b : def;
}
