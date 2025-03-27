using System;
using System.Data.Common;
using System.Data;
using System.Text.RegularExpressions;

namespace Aikido.Zen.Core.Models
{
    public class DummyCommand : System.Data.Common.DbCommand
    {
        public DummyCommand()
        {

        }

        public DummyCommand(string sql, Object[] parameters = null)
        {
            if (parameters != null)
            {
                var paramRegex = new Regex(@"\@(\w+)");
                var matches = paramRegex.Matches(sql);
                foreach (Match match in matches)
                {
                    // quick clean and escape of the parameter value
                    var paramValue = parameters[match.Index].ToString().Replace("'", "''");
                    sql = sql.Replace(match.Value, paramValue);
                }
            }
            CommandText = sql;
        }
        public override string CommandText { get; set; }
        public override int CommandTimeout { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override CommandType CommandType { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override bool DesignTimeVisible { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override UpdateRowSource UpdatedRowSource { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        protected override DbConnection DbConnection { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        protected override DbParameterCollection DbParameterCollection => throw new NotImplementedException();

        protected override DbTransaction DbTransaction { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Cancel()
        {
            throw new NotImplementedException();
        }

        public override int ExecuteNonQuery()
        {
            throw new NotImplementedException();
        }

        public override object ExecuteScalar()
        {
            throw new NotImplementedException();
        }

        public override void Prepare()
        {
            throw new NotImplementedException();
        }

        protected override DbParameter CreateDbParameter()
        {
            throw new NotImplementedException();
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            throw new NotImplementedException();
        }
    }
}
