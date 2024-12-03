using System;

namespace Aikido.Zen.Core.Models
{
    public enum AttackKind
    {
        NosqlInjection,
        SqlInjection,
        ShellInjection,
        PathTraversal,
        Ssrf
    }

    public static class AttackKindExtensions 
    {
        public static string ToHumanName(this AttackKind kind)
        {
            switch (kind)
            {
                case AttackKind.NosqlInjection:
                    return "a NoSQL injection";
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
