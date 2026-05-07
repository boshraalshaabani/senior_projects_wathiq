using eArchiveSystem.Application.Services;

namespace eArchiveSystem.UnitTests.Services;

[Trait("Layer", "Unit")]
[Trait("Area", "Normalization")]
public class TextPreprocessorServiceTests
{
    private readonly TextPreprocessorService _service = new();

    [Fact]
    public void Clean_RemovesStopWordsAndNormalizesArabicAndEnglish()
    {
        const string input = "\u0641\u064A \u0647\u0630\u0627 \u0627\u0644\u0646\u0638\u0627\u0645\u060C \u0627\u0644\u0623\u0631\u0634\u0641\u0629 THE archive";

        var result = _service.Clean(input);

        Assert.Equal("\u0627\u0644\u0646\u0638\u0627\u0645 \u0627\u0644\u0627\u0631\u0634\u0641\u0647 archive", result);
    }

    [Fact]
    public void Clean_ReturnsEmptyStringForBlankInput()
    {
        var result = _service.Clean("   ");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Clean_RemovesArabicDiacriticsAndPunctuation()
    {
        const string input = "\u0623\u064E\u0631\u0652\u0634\u0650\u064A\u0641\u064C!!!";

        var result = _service.Clean(input);

        Assert.Equal("\u0627\u0631\u0634\u064A\u0641", result);
    }
}
