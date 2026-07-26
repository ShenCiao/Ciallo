using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Ciallo.Data;
using DuckDB.NET.Data;
using DuckDB.NET.Native;
using GdUnit4;
using static GdUnit4.Assertions;

namespace Ciallo.Tests;

[TestSuite]
public class DuckDbBatchAppenderTests
{
    [TestCase]
    public void CompositeColumnsRoundTripAcrossDataChunks()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "ciallo-duckdb-batch-test-" + Guid.NewGuid().ToString("N") + ".db");
        var vectorSize = checked((int)NativeMethods.Helpers.DuckDBVectorSize());
        var rowCount = vectorSize + 3;
        var tokens = new Guid[rowCount];

        try
        {
            using (var connection = new DuckDBConnection($"Data Source={path}"))
            {
                connection.Open();
                using var create = connection.CreateCommand();
                create.CommandText = """
                                     create table batch_test (
                                         id integer,
                                         point struct(x float, y float),
                                         points struct(x float, y float)[],
                                         numbers integer[],
                                         links map(integer, integer),
                                         payload blob,
                                         token uuid,
                                         transform struct(
                                             x struct(x float, y float),
                                             y struct(x float, y float),
                                             origin struct(x float, y float)
                                         ),
                                         curves struct(
                                             p struct(x float, y float),
                                             "in" struct(x float, y float),
                                             "out" struct(x float, y float)
                                         )[],
                                         optional_point struct(x float, y float)
                                     );
                                     """;
                create.ExecuteNonQuery();

                using var appender = connection.CreateBatchAppender("batch_test");
                for (int i = 0; i < rowCount; i++)
                {
                    tokens[i] = Guid.NewGuid();
                    var row = appender.CreateRow();
                    row.AppendValue(i);
                    row.AppendStruct([i + 0.25f, -i]);
                    row.AppendStructList([
                        new List<float> { i, i + 1 },
                        new List<float> { -i, -(i + 1) }
                    ]);
                    row.AppendValue(new[] { i, i + 10 });
                    row.AppendMap(new[] { i }, new[] { i + 100 });
                    row.AppendValue(BitConverter.GetBytes(i));
                    row.AppendValue(tokens[i]);
                    row.AppendStruct([i + 1f, i + 2f, i + 3f, i + 4f, i + 5f, i + 6f]);
                    row.AppendStructList([
                        new List<float> { i + 0.1f, i + 10.1f },
                        new List<float> { i + 1.1f, i + 11.1f },
                        new List<float> { i + 2.1f, i + 12.1f },
                        new List<float> { i + 3.1f, i + 13.1f },
                        new List<float> { i + 4.1f, i + 14.1f },
                        new List<float> { i + 5.1f, i + 15.1f }
                    ]);
                    row.AppendStruct(i % vectorSize == 0 ? null : [i + 0.5f, i + 0.75f]);
                    row.EndRow();
                }
            }

            using var readConnection = new DuckDBConnection($"Data Source={path};ACCESS_MODE=READ_ONLY");
            readConnection.Open();
            using var command = readConnection.CreateCommand();
            command.CommandText =
                $"select * from batch_test where id in (0, {vectorSize - 1}, {vectorSize}, {rowCount - 1}) order by id;";
            using var reader = command.ExecuteReader();

            foreach (var expectedId in new[] { 0, vectorSize - 1, vectorSize, rowCount - 1 })
            {
                AssertThat(reader.Read()).IsTrue();
                AssertThat(Convert.ToInt32(reader.GetValue(0))).IsEqual(expectedId);

                var point = Struct(reader.GetValue(1));
                AssertThat(Convert.ToSingle(point["x"])).IsEqual(expectedId + 0.25f);
                AssertThat(Convert.ToSingle(point["y"])).IsEqual(-expectedId);

                var points = ((IEnumerable)reader.GetValue(2)).Cast<object>().Select(Struct).ToArray();
                AssertThat(points.Length).IsEqual(2);
                AssertThat(Convert.ToSingle(points[1]["x"])).IsEqual(expectedId + 1f);
                AssertThat(Convert.ToSingle(points[1]["y"])).IsEqual(-(expectedId + 1f));

                var numbers = ((IEnumerable)reader.GetValue(3)).Cast<object>().Select(Convert.ToInt32).ToArray();
                AssertThat(numbers).ContainsExactly(expectedId, expectedId + 10);

                var links = (IDictionary)reader.GetValue(4);
                AssertThat(Convert.ToInt32(links[expectedId])).IsEqual(expectedId + 100);
                AssertThat(ReadBlob(reader.GetValue(5))).ContainsExactly(BitConverter.GetBytes(expectedId));
                AssertThat((Guid)reader.GetValue(6)).IsEqual(tokens[expectedId]);

                var transform = Struct(reader.GetValue(7));
                AssertThat(Convert.ToSingle(Struct(transform["x"])["y"]))
                    .IsEqual(expectedId + 2f);
                AssertThat(Convert.ToSingle(Struct(transform["origin"])["y"]))
                    .IsEqual(expectedId + 6f);

                var curves = ((IEnumerable)reader.GetValue(8)).Cast<object>().Select(Struct).ToArray();
                AssertThat(curves.Length).IsEqual(2);
                AssertThat(Convert.ToSingle(Struct(curves[1]["out"])["y"]))
                    .IsEqual(expectedId + 15.1f);

                if (expectedId % vectorSize == 0)
                    AssertThat(reader.IsDBNull(9)).IsTrue();
                else
                    AssertThat(Convert.ToSingle(Struct(reader.GetValue(9))["x"]))
                        .IsEqual(expectedId + 0.5f);
            }

            AssertThat(reader.Read()).IsFalse();
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static IReadOnlyDictionary<string, object> Struct(object value) =>
        (IReadOnlyDictionary<string, object>)value;

    private static byte[] ReadBlob(object value)
    {
        if (value is byte[] bytes)
            return bytes;
        using var stream = (Stream)value;
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }
}
