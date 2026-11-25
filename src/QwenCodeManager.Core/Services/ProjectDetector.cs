using System.Text.RegularExpressions;
using QwenCodeManager.Core.Infrastructure;
using QwenCodeManager.Core.Models;

namespace QwenCodeManager.Core.Services;

/// <summary>
/// Detects project name and type from files inside the directory. Read-only.
/// </summary>
public static partial class ProjectDetector
{
    public sealed record Detection(string Name, ProjectType Type);

    public static Detection Detect(string projectPath)
    {
        var name = DetectName(projectPath) ?? Path.GetFileName(projectPath.TrimEnd('\\', '/'));
        var type = DetectType(projectPath);
        return new Detection(name, type);
    }

    public static string? DetectName(string dir)
    {
        // Gradle rootProject.name
        foreach (var gradle in new[] { "settings.gradle.kts", "settings.gradle" })
        {
            var path = Path.Combine(dir, gradle);
            if (File.Exists(path))
            {
                var m = RootProjectNameRegex().Match(SafeReadHead(path));
                if (m.Success)
                {
                    return m.Groups[1].Value;
                }
            }
        }

        // package.json name
        var packageJson = Path.Combine(dir, "package.json");
        if (File.Exists(packageJson))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(packageJson));
                if (doc.RootElement.TryGetProperty("name", out var nameEl)
                    && nameEl.ValueKind == System.Text.Json.JsonValueKind.String
                    && nameEl.GetString() is { Length: > 0 } pkgName)
                {
                    return pkgName;
                }
            }
            catch
            {
                // malformed package.json — fall through
            }
        }

        // .csproj / .sln file name
        try
        {
            var csproj = Directory.GetFiles(dir, "*.csproj").FirstOrDefault();
            if (csproj is not null)
            {
                return Path.GetFileNameWithoutExtension(csproj);
            }

            var sln = Directory.GetFiles(dir, "*.sln").FirstOrDefault();
            if (sln is not null)
            {
                return Path.GetFileNameWithoutExtension(sln);
            }
        }
        catch
        {
            // unreadable dir
        }

        // README first heading
        foreach (var readme in new[] { "README.md", "README.MD", "readme.md", "README.txt", "README" })
        {
            var path = Path.Combine(dir, readme);
            if (File.Exists(path))
            {
                var m = ReadmeHeadingRegex().Match(SafeReadHead(path));
                if (m.Success)
                {
                    return m.Groups[1].Value.Trim();
                }
            }
        }

        return null;
    }

    public static ProjectType DetectType(string dir)
    {
        try
        {
            // Unity: Assets + ProjectSettings/ProjectVersion.txt
            if (Directory.Exists(Path.Combine(dir, "Assets"))
                && File.Exists(Path.Combine(dir, "ProjectSettings", "ProjectVersion.txt")))
            {
                return ProjectType.Unity;
            }

            // Unreal: *.uproject
            if (Directory.GetFiles(dir, "*.uproject").Length > 0)
            {
                return ProjectType.Unreal;
            }

            // Android: gradle + AndroidManifest or com.android application plugin
            var hasGradle = File.Exists(Path.Combine(dir, "settings.gradle"))
                || File.Exists(Path.Combine(dir, "settings.gradle.kts"))
                || File.Exists(Path.Combine(dir, "build.gradle"))
                || File.Exists(Path.Combine(dir, "build.gradle.kts"));
            if (hasGradle)
            {
                if (Directory.Exists(Path.Combine(dir, "app", "src", "main"))
                    && File.Exists(Path.Combine(dir, "app", "src", "main", "AndroidManifest.xml")))
                {
                    return ProjectType.Android;
                }

                foreach (var buildFile in new[] { "build.gradle.kts", "build.gradle", "app/build.gradle.kts", "app/build.gradle" })
                {
                    var p = Path.Combine(dir, buildFile);
                    if (File.Exists(p))
                    {
                        var text = SafeReadHead(p, 16384);
                        if (text.Contains("com.android.application") || text.Contains("com.android.library"))
                        {
                            return ProjectType.Android;
                        }
                    }
                }
            }

            // .NET
            if (Directory.GetFiles(dir, "*.csproj").Length > 0
                || Directory.GetFiles(dir, "*.fsproj").Length > 0
                || Directory.GetFiles(dir, "*.sln").Length > 0)
            {
                return ProjectType.DotNet;
            }

            // Rust
            if (File.Exists(Path.Combine(dir, "Cargo.toml")))
            {
                return ProjectType.Rust;
            }

            // Python
            if (File.Exists(Path.Combine(dir, "pyproject.toml"))
                || File.Exists(Path.Combine(dir, "requirements.txt"))
                || File.Exists(Path.Combine(dir, "setup.py"))
                || File.Exists(Path.Combine(dir, "Pipfile")))
            {
                return ProjectType.Python;
            }

            // Node / Web via package.json
            var packageJson = Path.Combine(dir, "package.json");
            if (File.Exists(packageJson))
            {
                var text = SafeReadHead(packageJson, 32768);
                if (WebMarkersRegex().IsMatch(text)
                    || File.Exists(Path.Combine(dir, "index.html"))
                    || Directory.Exists(Path.Combine(dir, "src")) && File.Exists(Path.Combine(dir, "vite.config.ts")))
                {
                    return ProjectType.Web;
                }

                return ProjectType.NodeJs;
            }

            if (File.Exists(Path.Combine(dir, "index.html")))
            {
                return ProjectType.Web;
            }

            // C++
            if (File.Exists(Path.Combine(dir, "CMakeLists.txt"))
                || Directory.GetFiles(dir, "*.vcxproj").Length > 0)
            {
                return ProjectType.Cpp;
            }

            // Java / Kotlin via gradle without Android
            if (hasGradle || File.Exists(Path.Combine(dir, "pom.xml")))
            {
                var kotlin = SafeEnumerate(dir, "*.kt", depth: 3).Any();
                return kotlin ? ProjectType.Kotlin : ProjectType.Java;
            }
        }
        catch
        {
            // detection must never throw
        }

        return ProjectType.Other;
    }

    private static IEnumerable<string> SafeEnumerate(string dir, string pattern, int depth)
    {
        if (depth < 0)
        {
            yield break;
        }

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(dir, pattern);
        }
        catch
        {
            yield break;
        }

        foreach (var f in files)
        {
            yield return f;
        }

        IEnumerable<string> dirs;
        try
        {
            dirs = Directory.EnumerateDirectories(dir);
        }
        catch
        {
            yield break;
        }

        foreach (var sub in dirs)
        {
            var name = Path.GetFileName(sub);
            if (name is "node_modules" or ".git" or "bin" or "obj" or "build" or ".gradle")
            {
                continue;
            }

            foreach (var f in SafeEnumerate(sub, pattern, depth - 1))
            {
                yield return f;
            }
        }
    }

    private static string SafeReadHead(string path, int maxChars = 8192)
    {
        try
        {
            using var reader = new StreamReader(path);
            var buffer = new char[maxChars];
            var read = reader.Read(buffer, 0, maxChars);
            return new string(buffer, 0, read);
        }
        catch
        {
            return string.Empty;
        }
    }

    [GeneratedRegex("""rootProject\.name\s*=\s*["']([^"']+)["']""")]
    private static partial Regex RootProjectNameRegex();

    [GeneratedRegex(@"^\s*#\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex ReadmeHeadingRegex();

    [GeneratedRegex("""["'](react|vue|svelte|next|nuxt|vite|astro|angular|@angular/core|solid-js|preact|webpack|parcel)["']\s*:""")]
    private static partial Regex WebMarkersRegex();
}
