
namespace SpectraLab.Api.Models;

public class Measurement
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<SpectrumPoint> Points { get; set; } = new();
}

public class SpectrumPoint
{
    public int Id { get; set; }

    public int MeasurementId { get; set; }

    public double Wavelength { get; set; }

    public double Intensity { get; set; }
}
