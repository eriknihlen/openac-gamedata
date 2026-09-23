using OpenAC.GameData.Sql;

namespace OpenAC.GameData.Tests;

public class SqlDumpTests
{
    private const string Dump = """
        CREATE TABLE `things` (
          `id` int(10) unsigned NOT NULL,
          `name` text,
          `weight` float NOT NULL,
          `flag` bit(1),
          PRIMARY KEY (`id`)
        ) ENGINE=InnoDB;
        INSERT INTO `things` VALUES (1,'It\'s a \\ path\nnext','1.5','\0'),(2,NULL,-3,'x'),(3,'a),(b',2.5e-3,'it''s');
        CREATE TABLE `other` (
          `id` int(10) NOT NULL,
          PRIMARY KEY (`id`)
        );
        INSERT INTO `other` VALUES (9);
        CREATE TABLE `named` (
          `a` int NOT NULL,
          `b` int NOT NULL,
          `c` int NOT NULL,
          PRIMARY KEY (`a`)
        );
        INSERT INTO `named` (`c`, `a`) VALUES (30,10),(31,11);
        """;

    [Fact]
    public void Reads_numbers_strings_escapes_and_nulls()
    {
        SqlTable things = SqlDump.Read(new StringReader(Dump), ["things"])["things"];

        Assert.Equal(["id", "name", "weight", "flag"], things.Columns);
        Assert.Equal(3, things.Rows.Count);
        Assert.Equal(1L, things.Rows[0][0]);
        Assert.Equal("It's a \\ path\nnext", things.Rows[0][1]);
        Assert.Equal("1.5", things.Rows[0][2]);
        Assert.Equal("\0", things.Rows[0][3]);
        Assert.Null(things.Rows[1][1]);
        Assert.Equal(-3L, things.Rows[1][2]);
        Assert.Equal("a),(b", things.Rows[2][1]);
        Assert.Equal(0.0025, things.Rows[2][2]);
        Assert.Equal("it's", things.Rows[2][3]);
    }

    [Fact]
    public void A_named_column_list_fills_those_columns_and_leaves_the_rest_null()
    {
        SqlTable named = SqlDump.Read(new StringReader(Dump), ["named"])["named"];

        Assert.Equal(2, named.Rows.Count);
        Assert.Equal([10L, null, 30L], named.Rows[0]);
        Assert.Equal([11L, null, 31L], named.Rows[1]);
    }

    [Fact]
    public void Reads_only_the_tables_asked_for()
    {
        Dictionary<string, SqlTable> tables = SqlDump.Read(new StringReader(Dump), ["other"]);

        Assert.Equal(["other"], tables.Keys);
        Assert.Equal(9L, Assert.Single(tables["other"].Rows)[0]);
    }

    [Fact]
    public void A_missing_table_is_an_error()
    {
        Assert.Throws<InvalidDataException>(() => SqlDump.Read(new StringReader(Dump), ["absent"]));
    }
}
