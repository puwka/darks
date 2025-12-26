using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace darks.Classes
{
    public static class Db
    {
        // В реальном проекте пароль не хранят в коде!
        private static string connString = "Host=localhost;Port=5432;Username=postgres;Password=12345;Database=darks;";

        public static NpgsqlConnection GetConn()
        {
            return new NpgsqlConnection(connString);
        }
    }
}
