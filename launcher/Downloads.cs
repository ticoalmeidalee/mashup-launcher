using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

/// <summary>Downloads for install steps: browser headers (some official sites reject bare clients), SHA-256 pins, archives.</summary>
static class Downloads
{
    const string UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36 MashupLauncher";

    /// <summary>file:// URLs are for the self-tests only (Tests.Run turns this on); players' downloads are https.</summary>
    public static bool AllowFileUrls;

    public static bool Allowed(string url) =>
        url != null && (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || (AllowFileUrls && url.StartsWith("file://", StringComparison.OrdinalIgnoreCase)));

    public static byte[] Get(string url, string referer = null)
    {
        if (!Allowed(url))
            throw new InvalidOperationException("Only https:// downloads are allowed (" + url + ")");
        ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        using (var wc = new WebClient())
        {
            wc.Headers[HttpRequestHeader.UserAgent] = UserAgent;
            wc.Headers[HttpRequestHeader.Accept] = "text/html,application/octet-stream,*/*";
            wc.Headers[HttpRequestHeader.AcceptLanguage] = "en-US"; // dev-c.com answers 406 without it
            if (referer != null) wc.Headers[HttpRequestHeader.Referer] = referer;
            return wc.DownloadData(url);
        }
    }

    public static string Sha256(byte[] data)
    {
        using (var sha = SHA256.Create())
            return string.Concat(sha.ComputeHash(data).Select(b => b.ToString("x2")));
    }

    /// <summary>The bytes at url; with a pin, an InvalidDataException unless their SHA-256 matches it.</summary>
    public static byte[] Verified(string url, string sha256, string referer = null)
    {
        byte[] data = Get(url, referer);
        if (!string.IsNullOrEmpty(sha256) && !Sha256(data).Equals(sha256.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Download from " + new Uri(url).Host + " does not match its pinned SHA-256: refusing it");
        return data;
    }

    /// <summary>The first link in html matching pattern, made absolute against pageUrl; null if none.</summary>
    public static string FindLink(string pageUrl, string html, string pattern)
    {
        var m = Regex.Match(html, pattern);
        return m.Success ? new Uri(new Uri(pageUrl), m.Value).AbsoluteUri : null;
    }

    public static string FindLinkOnPage(string pageUrl, string pattern) =>
        FindLink(pageUrl, System.Text.Encoding.UTF8.GetString(Get(pageUrl)), pattern)
            ?? throw new InvalidDataException("No download link matching '" + pattern + "' on " + pageUrl);

    /// <summary>
    /// Members of a zip (or of a zip appended to an .exe, like installers that carry their files) as target -> bytes.
    /// members maps the archive path to the target name; a missing member is an InvalidDataException.
    /// </summary>
    public static Dictionary<string, byte[]> Extract(byte[] archive, IDictionary<string, string> members)
    {
        using (var zip = Open(archive))
        {
            var result = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in members)
            {
                string want = kv.Key.Replace('\\', '/');
                var entry = zip.Entries.FirstOrDefault(e => e.FullName.Replace('\\', '/').Equals(want, StringComparison.OrdinalIgnoreCase))
                    ?? throw new InvalidDataException("'" + kv.Key + "' is not in the downloaded archive");
                using (var s = entry.Open())
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    result[kv.Value] = ms.ToArray();
                }
            }
            return result;
        }
    }

    static ZipArchive Open(byte[] data)
    {
        try
        {
            var plain = new ZipArchive(new MemoryStream(data), ZipArchiveMode.Read);
            _ = plain.Entries.Count; // ZipArchive reads its directory lazily: make a bad one fail here
            return plain;
        }
        catch (InvalidDataException) { }
        // a zip appended to something else: the end-of-central-directory record says where the archive starts
        for (int i = data.Length - 22; i >= Math.Max(0, data.Length - 65557); --i)
        {
            if (data[i] != 0x50 || data[i + 1] != 0x4B || data[i + 2] != 0x05 || data[i + 3] != 0x06)
                continue;
            long cdSize = BitConverter.ToUInt32(data, i + 12), cdOffset = BitConverter.ToUInt32(data, i + 16);
            long start = i - cdSize - cdOffset;
            if (start < 0)
                break;
            return new ZipArchive(new MemoryStream(data, (int)start, data.Length - (int)start), ZipArchiveMode.Read);
        }
        throw new InvalidDataException("The download is not a zip archive");
    }
}
