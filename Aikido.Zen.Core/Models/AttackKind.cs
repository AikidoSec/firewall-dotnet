using System;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Aikido.Zen.Core.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AttackKind
    {
        // leave this out for now, until we cover nosql attacks
        // NosqlInjection,
        SqlInjection,
        ShellInjection,
        PathTraversal,
        Ssrf
    }

    public static class AttackKindExtensions 
    {
        public static string ToJsonName(this AttackKind kind) {
            switch (kind)
            {
                // leave this out for now, until we cover nosql attacks
                // case AttackKind.NosqlInjection:
                //     return "a NoSQL injection";
                case AttackKind.SqlInjection:
                    return "sql_injection";
                case AttackKind.ShellInjection:
                    return "shell_injection";
                case AttackKind.PathTraversal:
                    return "path_traversal";
                case AttackKind.Ssrf:
                    return "ssrf";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        public static string ToHumanName(this AttackKind kind)
        {
            switch (kind)
            {
                // leave this out for now, until we cover nosql attacks
                // case AttackKind.NosqlInjection:
                //     return "a NoSQL injection";
                case AttackKind.SqlInjection:
                    return "an SQL injection";
                case AttackKind.ShellInjection:
                    return "a shell injection";
                case AttackKind.PathTraversal:
                    return "a path traversal attack";
                case AttackKind.Ssrf:
                    return "a server-side request forgery";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
    }
}
