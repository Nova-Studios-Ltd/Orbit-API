namespace NovaAPI.SQL;

public struct SQLColumn
{
    public string Column { get; private set; }
    public string Value { get; private set; }

    public SQLColumn(string column, string value)
    {
        Column = column;
        Value = value;
    }
}