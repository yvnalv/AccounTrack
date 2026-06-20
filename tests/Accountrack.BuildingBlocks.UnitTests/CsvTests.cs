using Accountrack.SharedKernel.Csv;
using FluentAssertions;
using Xunit;

namespace Accountrack.BuildingBlocks.UnitTests;

public class CsvTests
{
    [Fact]
    public void Parses_quoted_fields_with_commas_quotes_and_newlines()
    {
        var text = "Code,Name\r\nC1,\"Acme, Inc.\"\r\nC2,\"He said \"\"hi\"\"\"\r\n";
        var rows = Csv.Parse(text);

        rows.Should().HaveCount(3);
        rows[1][1].Should().Be("Acme, Inc.");
        rows[2][1].Should().Be("He said \"hi\"");
    }

    [Fact]
    public void Skips_blank_lines_and_handles_missing_trailing_newline()
    {
        var rows = Csv.Parse("A,B\r\n\r\n1,2");
        rows.Should().HaveCount(2);
        rows[1].Should().BeEquivalentTo(new[] { "1", "2" });
    }

    [Fact]
    public void Write_quotes_only_fields_that_need_it_and_round_trips()
    {
        var csv = Csv.Write(new[] { "Code", "Name" }, new[] { new string?[] { "C1", "Acme, Inc." } });
        csv.Should().Be("Code,Name\r\nC1,\"Acme, Inc.\"\r\n");

        var rows = Csv.Parse(csv);
        rows[1][1].Should().Be("Acme, Inc.");
    }
}
