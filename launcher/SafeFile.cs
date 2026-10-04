using System;
using System.IO;
using System.Threading;

/// <summary>
/// Whole-file writes that never leave a half-written file: write a temp file, then swap it in. Antivirus and indexers
/// briefly hold files that were just written, so the swap retries for about a second before giving up.
/// </summary>
static class SafeFile
{
    public static void WriteAllText(string path, string text)
    {
        string tmp = path + ".mashup-tmp";
        File.WriteAllText(tmp, text);
        for (int attempt = 0; ; ++attempt)
        {
            try
            {
                if (File.Exists(path)) File.Replace(tmp, path, null);
                else File.Move(tmp, path);
                return;
            }
            catch (IOException) when (attempt < 20)
            {
                Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException) when (attempt < 20)
            {
                Thread.Sleep(50);
            }
        }
    }
}
