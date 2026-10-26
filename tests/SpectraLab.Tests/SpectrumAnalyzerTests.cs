
using SpectraLab.Api.Models;
using SpectraLab.Api.Services;

public class SpectrumAnalyzerTests
{
    [Fact]
    public void Analyze_UnevenSpacing_ReturnsCorrectArea()
    {
        // Intentionally provide unsorted wavelengths.
        var points = new[]
        {
            new SpectrumPoint
            {
                Wavelength = 550,
                Intensity = 10
            },
            new SpectrumPoint
            {
                Wavelength = 400,
                Intensity = 10
            },
            new SpectrumPoint
            {
                Wavelength = 450,
                Intensity = 20
            }
        };

        var result = SpectrumAnalyzer.Analyze(points);

        Assert.Equal(450, result.PeakWavelengthNm);
        Assert.Equal(20, result.PeakIntensity);
        Assert.Equal(2250, result.IntegratedIntensity, 6);
    }

    [Fact]
    public void Analyze_DuplicateWavelengths_ThrowsError()
    {
        var points = new[]
        {
            new SpectrumPoint
            {
                Wavelength = 400,
                Intensity = 10
            },
            new SpectrumPoint
            {
                Wavelength = 400,
                Intensity = 20
            }
        };

        Assert.Throws<ArgumentException>(
            () => SpectrumAnalyzer.Analyze(points)
        );
    }

    [Fact]
    public void Analyze_InvalidIntensity_ThrowsError()
    {
        var points = new[]
        {
            new SpectrumPoint
            {
                Wavelength = 400,
                Intensity = double.NaN
            },
            new SpectrumPoint
            {
                Wavelength = 500,
                Intensity = 20
            }
        };

        Assert.Throws<ArgumentException>(
            () => SpectrumAnalyzer.Analyze(points)
        );
    }
}
