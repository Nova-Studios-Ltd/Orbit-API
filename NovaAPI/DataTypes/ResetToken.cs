using System;

namespace NovaAPI.DataTypes;

public class ResetToken
{
    public string UUID;
    public DateTime Created;
    public bool CleanUp;

    public ResetToken(string uuid)
    {
        UUID = uuid;
        Created = DateTime.Now;
        CleanUp = false;
    }
}