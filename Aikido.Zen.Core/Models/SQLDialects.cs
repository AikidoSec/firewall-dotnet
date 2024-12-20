using System;

namespace Aikido.Zen.Core.Models
{
    public enum SQLDialect
    {
        Generic,
        MicrosoftSQL,
        MySQL, 
        PostgreSQL
    }

    public static class SQLDialectExtensions 
    {
        public static string ToHumanName(this SQLDialect dialect)
        {
            switch (dialect)
            {
                case SQLDialect.Generic:
                    return "Generic";
                case SQLDialect.MicrosoftSQL:
                    return "Microsoft SQL";
                case SQLDialect.MySQL:
                    return "MySQL";
                case SQLDialect.PostgreSQL:
                    return "PostgreSQL";
                default:
                    throw new ArgumentOutOfRangeException(nameof(dialect));
            }
        }

        public static int ToRustDialectInt(this SQLDialect dialect)
        {
            // Reference : https://github.com/AikidoSec/zen-internals/blob/main/src/sql_injection/helpers/select_dialect_based_on_enum.rs
            switch (dialect)
            {
                case SQLDialect.Generic:
                    return 0;
                case SQLDialect.MicrosoftSQL:
                    return 7;
                case SQLDialect.MySQL:
                    return 8;
                case SQLDialect.PostgreSQL:
                    return 9;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dialect));
            }
        }

        public static SQLDialect ToSQLDialect(this int dialect)
        {
            switch (dialect)
            {
                case 0:
                    return SQLDialect.Generic;
                case 7:
                    return SQLDialect.MicrosoftSQL;
                case 8:
                    return SQLDialect.MySQL;
                case 9:
                    return SQLDialect.PostgreSQL;
                default:
                    throw new ArgumentOutOfRangeException(nameof(dialect));
            }
        }
    }
}
