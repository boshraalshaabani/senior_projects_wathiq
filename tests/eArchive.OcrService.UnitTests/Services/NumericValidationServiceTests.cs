using eArchive.OcrService.Services;

namespace eArchive.OcrService.UnitTests.Services;

[Trait("Layer", "Unit")]
[Trait("Area", "OCRNormalization")]
public class NumericValidationServiceTests
{
    private readonly NumericValidationService _service = new();

    [Fact]
    public void ValidateAndNormalize_MapsArabicAndPersianDigitsToLatin()
    {
        var input = "\u0661\u0662\u0663 and \u06F4\u06F5\u06F6";

        var result = _service.ValidateAndNormalize(input);

        Assert.Equal("123 and 456", result);
    }

    [Fact]
    public void ValidateAndNormalize_ReturnsEmptyForBlankInput()
    {
        var result = _service.ValidateAndNormalize("   ");

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ValidateAndNormalize_PreservesNonDigitCharactersWhileNormalizingMixedInput()
    {
        var input = "REF-123/\u0664\u0665";

        var result = _service.ValidateAndNormalize(input);

        Assert.Equal("REF-123/45", result);
    }
}
