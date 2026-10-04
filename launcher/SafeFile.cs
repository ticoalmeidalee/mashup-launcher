using System;
using System.IO;
using System.Text;
using System.Threading;

/// <summary>
/// Whole-file writes that survive crashes and power cuts: the new content is written to a temp file and flushed to the
/// disk, then swapped in, with a flushed copy in &lt;file&gt;.bak (ReadWithFallback reads it if the main file is
/// empty or unreadable). Antivirus and indexers briefly hold files that were just written, so the swap retries briefly.
/// </summary>
static class SafeFile
{
    /// <param name="fallback">false for files that aren't ours (another program's config): no .bak beside them</param>
    public static void WriteAllText(string path, string text, bool fallback = true)
    {
        string tmp = path + ".mashup-tmp";
        byte[] bytes = new UTF8Encoding(false).GetBytes(text);
        WriteFlushed(tmp, bytes); // on the disk before it replaces anything
        for (int attempt = 0; ; ++attempt)
        {
            try
            {
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
                break;
            }
            catch (IOException) when (attempt < 20) { Thread.Sleep(50); }
            catch (UnauthorizedAccessException) when (attempt < 20) { Thread.Sleep(50); }
        }
        // then the same content as the fallback: a cut during this write leaves the main file whole, and a cut during
        // the main file's swap leaves this one (the previous version) whole
        if (fallback)
            try { WriteFlushed(path + ".bak", bytes); } catch (IOException) { }
    }

    static void WriteFlushed(string path, byte[] bytes)
    {
        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            fs.Write(bytes, 0, bytes.Length);
            fs.Flush(true);
        }
    }

    /// <summary>parse(text) of the file, or of its .bak when the file is empty or parse throws/returns null; null if neither works.</summary>
    public static T ReadWithFallback<T>(string path, Func<string, T> parse) where T : class
    {
        foreach (string f in new[] { path, path + ".bak" })
        {
            try
            {
                if (!File.Exists(f)) continue;
                string text = File.ReadAllText(f);
                if (text.Trim().Length == 0) continue;
                T value = parse(text);
                if (value != null) return value;
            }
            catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is IOException || e is FormatException) { }
        }
        return null;
    }

    public static void Delete(string path)
    {
        foreach (string f in new[] { path, path + ".bak", path + ".mashup-tmp" })
            if (File.Exists(f)) File.Delete(f);
    }
}
