using MyHttpServer.ORM;

namespace MyHttpServer;

[Table("users")]
public sealed class User
{
    [Column("id", key: true)]
    public int Id { get; set; }

    [Column("name")]
    public string Name { get; set; } = "";

    [Column("groupname")]
    public string GroupName { get; set; } = "";

    [Column("age")]
    public int Age { get; set; }

    [Column("gender")]
    public bool Gender { get; set; }
}
