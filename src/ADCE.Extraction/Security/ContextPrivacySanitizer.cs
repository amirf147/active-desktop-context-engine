// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using System;
using System.Collections.Generic;
using System.Linq;

namespace ADCE.Extraction.Security;

/// <summary>
/// Privacy firewall sanitizing URLs, edit buffers, and focused elements to prevent
/// plaintext secrets, OAuth tokens, and credentials from leaking into context envelopes.
/// </summary>
public static class ContextPrivacySanitizer
{
    private static readonly HashSet<string> SensitiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".env",
        ".pem",
        ".key",
        ".pfx",
        ".p12",
        "id_rsa",
        "id_ed25519",
        "secrets.json",
        "secrets.yaml",
        "credentials",
        ".kdbx"
    };

    /// <summary>
    /// Strips query parameters, credentials, and fragment identifiers from raw address bar strings.
    /// Example: 'https://auth.example.com/oauth?code=xyz#state' -> 'https://auth.example.com/oauth'
    /// </summary>
    public static string SanitizeUrl(string? rawUrl)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
            return string.Empty;

        rawUrl = rawUrl.Trim();

        // If it starts with common schemes or standard domain syntax
        if (Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps ||
                uri.Scheme.Equals("chrome", StringComparison.OrdinalIgnoreCase) ||
                uri.Scheme.Equals("edge", StringComparison.OrdinalIgnoreCase))
            {
                return $"{uri.Scheme}://{uri.Authority}{uri.AbsolutePath}";
            }

            if (uri.Scheme.Equals("about", StringComparison.OrdinalIgnoreCase))
            {
                int q = rawUrl.IndexOf('?');
                string withoutQuery = q >= 0 ? rawUrl[..q] : rawUrl;
                int h = withoutQuery.IndexOf('#');
                return h >= 0 ? withoutQuery[..h] : withoutQuery;
            }
        }

        // If it's a domain/path without scheme (e.g. "github.com/repo?token=123")
        int queryIdx = rawUrl.IndexOf('?');
        if (queryIdx >= 0)
        {
            rawUrl = rawUrl[..queryIdx];
        }

        int hashIdx = rawUrl.IndexOf('#');
        if (hashIdx >= 0)
        {
            rawUrl = rawUrl[..hashIdx];
        }

        return rawUrl;
    }

    /// <summary>
    /// Checks whether a file path or name matches sensitive credential patterns.
    /// </summary>
    public static bool IsSensitiveFile(string? pathOrName)
    {
        if (string.IsNullOrWhiteSpace(pathOrName))
            return false;

        string normalized = pathOrName.Replace('\\', '/');
        string fileName = normalized.Split('/').LastOrDefault() ?? normalized;

        return SensitiveExtensions.Any(ext =>
            fileName.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ||
            fileName.StartsWith(ext, StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals(ext, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Redacts buffer content if the input control is a password field or belongs to a sensitive file.
    /// </summary>
    public static string? SanitizeBuffer(string? buffer, string? fileName, bool isPasswordControl)
    {
        if (isPasswordControl)
            return "[REDACTED_PASSWORD]";

        if (IsSensitiveFile(fileName))
            return "[REDACTED_SENSITIVE_FILE_BUFFER]";

        return buffer;
    }

    /// <summary>
    /// Strips local filesystem paths, user profile directory roots, and local file protocol URIs from text strings.
    /// </summary>
    public static string SanitizeText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        string sanitized = text;

        // Replace user profile roots
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            @"(?:[A-Za-z]:[\\/]+|/)(?:Users|home|Documents and Settings)[\\/]+[^\\/]+[\\/]+",
            "~/",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        // Strip local file scheme prefix
        sanitized = System.Text.RegularExpressions.Regex.Replace(
            sanitized,
            @"file:[\\/]{3}",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        return sanitized;
    }
}
