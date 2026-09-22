using System.Collections.Generic;
using System.Text;
using GZCTF.Models.Data;
using GZCTF.Models.Request.Game;
using GZCTF.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NPOI.XSSF.UserModel;
using Xunit;

namespace GZCTF.Test.UnitTests.Services;

public class ScoreboardExportTests
{
    private static ScoreboardModel Board() => new() {
        Items = [], Divisions = [], Challenges = new Dictionary<ChallengeCategory, IEnumerable<ChallengeInfo>> {
            [ChallengeCategory.Misc] = [new ChallengeInfo { Id = 7, Title = "Challenge, One", Score = 500 }]
        }
    };

    [Fact]
    public void ExcelAndCsvSupportEmptyScoreboards()
    {
        using var provider = new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();
        var helper = new ExcelHelper(provider.GetRequiredService<IStringLocalizer<Program>>());
        using var stream = helper.GetScoreboardExcel(Board());
        stream.Position = 0;
        using var workbook = new XSSFWorkbook(stream);
        Assert.Equal(0, workbook.GetSheetAt(0).LastRowNum);
        var csv = Encoding.UTF8.GetString(helper.GetScoreboardCsv(Board()));
        Assert.StartsWith("\uFEFF", csv);
        Assert.Contains("\"Challenge, One\"", csv);
    }

    [Fact]
    public void ExportsPreserveScoresOrderUnicodeAndQuotedFields_AndCsvNeutralizesFormulas()
    {
        using var provider = new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();
        var helper = new ExcelHelper(provider.GetRequiredService<IStringLocalizer<Program>>());
        var board = Board();
        board.Items[2] = new ScoreboardItem { Rank = 2, Name = "  =SUM(1,2)", Score = 0, TeamInfo = new Team() };
        board.Items[1] = new ScoreboardItem { Rank = 1, Name = "Tim, \"中文\"\nA", Score = 123,
            TeamInfo = new Team(), SolvedChallenges = [new ChallengeItem { Id = 7, Score = 123 }] };
        var csv = Encoding.UTF8.GetString(helper.GetScoreboardCsv(board));
        Assert.Contains("\"Tim, \"\"中文\"\"\nA\"", csv);
        Assert.Contains("\"'  =SUM(1,2)\"", csv);
        Assert.Contains("\"123\",\"123\"\r\n", csv);
        Assert.True(csv.IndexOf("Tim,") < csv.IndexOf("SUM("));
        using var stream = helper.GetScoreboardExcel(board);
        stream.Position = 0;
        using var workbook = new XSSFWorkbook(stream);
        var row = workbook.GetSheetAt(0).GetRow(1);
        Assert.Equal(1, row.GetCell(0).NumericCellValue);
        Assert.Equal(board.Items[1].Name, row.GetCell(1).StringCellValue);
        Assert.Equal(123, row.GetCell(row.LastCellNum - 1).NumericCellValue);
    }
}
