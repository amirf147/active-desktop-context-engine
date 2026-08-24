// SPDX-License-Identifier: Apache-2.0
// Copyright (c) 2024-2026 Amir Farhadi

using ADCE.Extraction.Security;
using Xunit;

namespace ADCE.Extraction.Tests;

public class ContextPrivacySanitizerTests
{
    [Theory]
    [InlineData("https://github.com/amirf147/repo?token=ghp_123456789#heading", "https://github.com/amirf147/repo")]
    [InlineData("https://auth.example.com/oauth/callback?code=eyJhbGciOi...&state=xyz", "https://auth.example.com/oauth/callback")]
    [InlineData("https://example.com/search?q=test&api_key=secret123", "https://example.com/search")]
    [InlineData("http://localhost:3000/dashboard?auth=bearer_abc", "http://localhost:3000/dashboard")]
    [InlineData("about:blank", "about:blank")]
    [InlineData("github.com/repo?param=secret", "github.com/repo")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void SanitizeUrl_StripsSensitiveQueryParameters(string? rawUrl, string expected)
    {
        string sanitized = ContextPrivacySanitizer.SanitizeUrl(rawUrl);
        Assert.Equal(expected, sanitized);
    }

    [Theory]
    [InlineData(".env", true)]
    [InlineData(".env.production", true)]
    [InlineData("id_rsa", true)]
    [InlineData("id_ed25519", true)]
    [InlineData("server.key", true)]
    [InlineData("cert.pem", true)]
    [InlineData("secrets.json", true)]
    [InlineData("secrets.yaml", true)]
    [InlineData(".aws/credentials", true)]
    [InlineData("README.md", false)]
    [InlineData("Program.cs", false)]
    [InlineData("package.json", false)]
    public void IsSensitiveFile_DetectsSecretsFiles(string fileName, bool expected)
    {
        bool result = ContextPrivacySanitizer.IsSensitiveFile(fileName);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SanitizeBuffer_RedactsPasswordsAndSensitiveFiles()
    {
        // 1. Password input control
        string? passwordResult = ContextPrivacySanitizer.SanitizeBuffer("my_super_secret_password", "PasswordInput", isPasswordControl: true);
        Assert.Equal("[REDACTED_PASSWORD]", passwordResult);

        // 2. Sensitive file buffer (.env)
        string? envResult = ContextPrivacySanitizer.SanitizeBuffer("OPENAI_API_KEY=sk-123456", ".env", isPasswordControl: false);
        Assert.Equal("[REDACTED_SENSITIVE_FILE_BUFFER]", envResult);

        // 3. Normal code file buffer
        string? normalResult = ContextPrivacySanitizer.SanitizeBuffer("public class Foo {}", "Foo.cs", isPasswordControl: false);
        Assert.Equal("public class Foo {}", normalResult);
    }
}
