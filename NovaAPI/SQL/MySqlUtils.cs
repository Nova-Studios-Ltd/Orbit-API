using System.Collections.Generic;
using System.Data;
using MySql.Data.MySqlClient;

namespace NovaAPI.SQL;

public static class MySqlUtils
{
    public static bool ContainsRow(this MySqlConnection sql, string table, string field, string value)
    {
        if (sql.State != ConnectionState.Open) return false;
        using MySqlCommand contains = new MySqlCommand($"SELECT * FROM `{table}` WHERE {field}=@value", sql);
        contains.Parameters.AddWithValue("@value", value);
        MySqlDataReader reader = contains.ExecuteReader();
        if (reader.HasRows)
        {
            reader.Close();
            return true;
        }
        reader.Close();
        return false;
    }

    /*public static void UpdateRow(this MySqlConnection sql, string table, List<SQLColumn> columns)
    {
        if (sql.State != ConnectionState.Open) return;
        using MySqlCommand
    }*/
}