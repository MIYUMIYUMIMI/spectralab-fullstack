
using SpectraLab.Api.Models;

namespace SpectraLab.Api.Services;

public record SpectrumAnalysisResult(
    double PeakWavelengthNm,
    double PeakIntensity,
    double IntegratedIntensity,
    int PointCount
);

public static class SpectrumAnalyzer
{
    public static SpectrumAnalysisResult Analyze(
        IEnumerable<SpectrumPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        // Sort the data by wavelength.
        var sorted = points
            .OrderBy(p => p.Wavelength)
            .ToList();

        if (sorted.Count < 2)
        {
            throw new ArgumentException(
                "At least two spectrum points are required.");
        }

        // Reject invalid numerical measurements.
        foreach (var point in sorted)
        {
            if (!double.IsFinite(point.Wavelength) ||
                !double.IsFinite(point.Intensity) ||
                point.Wavelength <= 0)
            {
                throw new ArgumentException(
                    "Invalid wavelength or intensity.");
            }
        }

        // Wavelengths must be unique.
        for (int i = 1; i < sorted.Count; i++)
        {
            if (sorted[i].Wavelength <=
                sorted[i - 1].Wavelength)
            {
                throw new ArgumentException(
                    "Duplicate wavelengths are not allowed.");
            }
        }

        // Identify the highest measured intensity.
        var peak = sorted.MaxBy(p => p.Intensity)!;

        // Integrate using the trapezoidal rule.
        double area = 0;

        for (int i = 1; i < sorted.Count; i++)
        {
            var previous = sorted[i - 1];
            var current = sorted[i];

            double width =
                current.Wavelength - previous.Wavelength;

            double averageIntensity =
                (previous.Intensity + current.Intensity) / 2;

            area += width * averageIntensity;
        }

        return new SpectrumAnalysisResult(
            peak.Wavelength,
            peak.Intensity,
            area,
            sorted.Count
        );
    }
}
