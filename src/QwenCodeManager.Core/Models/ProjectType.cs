namespace QwenCodeManager.Core.Models;

public enum ProjectType
{
    Other,
    Android,
    DotNet,
    Web,
    NodeJs,
    Python,
    Java,
    Kotlin,
    Rust,
    Cpp,
    Unity,
    Unreal,
}

public static class ProjectTypeExtensions
{
    public static string DisplayName(this ProjectType type) => type switch
    {
        ProjectType.Android => "Android",
        ProjectType.DotNet => ".NET",
        ProjectType.Web => "Web",
        ProjectType.NodeJs => "Node.js",
        ProjectType.Python => "Python",
        ProjectType.Java => "Java",
        ProjectType.Kotlin => "Kotlin",
        ProjectType.Rust => "Rust",
        ProjectType.Cpp => "C++",
        ProjectType.Unity => "Unity",
        ProjectType.Unreal => "Unreal",
        _ => "Other",
    };
}

public enum LaunchMode
{
    NewSession,
    Continue,
}
