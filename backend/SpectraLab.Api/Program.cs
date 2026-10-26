
using Microsoft.EntityFrameworkCore;
using SpectraLab.Api.Data;
using SpectraLab.Api.Models;
using SpectraLab.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure the SQLite database.

var databasePath = builder.Configuration["DatabasePath"]
    ?? Path.Combine(
        builder.Environment.ContentRootPath,
        "spectralab.db"
    );

builder.Services.AddDbContext<SpectraLabDbContext>(options =>
    options.UseSqlite($"Data Source={databasePath}")
);


builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDev", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors("AngularDev");

// Create database tables for this initial prototype.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider
        .GetRequiredService<SpectraLabDbContext>();

    db.Database.EnsureCreated();
}

app.MapGet("/", () => "SpectraLab API is running");

// GET: Retrieve all saved measurements.
app.MapGet("/api/measurements", async (SpectraLabDbContext db) =>
{
    return await db.Measurements
        .Include(m => m.Points)
        .AsNoTracking()
        .ToListAsync();
});

// GET: Retrieve one measurement by ID.
app.MapGet("/api/measurements/{id:int}",
    async (int id, SpectraLabDbContext db) =>
    {
        var measurement = await db.Measurements
            .Include(m => m.Points)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        return measurement is null
            ? Results.NotFound()
            : Results.Ok(measurement);
    });


app.MapGet("/api/measurements/{id:int}/analysis",
    async (int id, SpectraLabDbContext db) =>
    {
        // Step 1: Load the measurement and its data points.
        var measurement = await db.Measurements
            .Include(m => m.Points)
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == id);

        // Step 2: Handle a measurement that does not exist.
        if (measurement is null)
        {
            return Results.NotFound(new
            {
                error = "Measurement not found."
            });
        }

        // Step 3: Run the scientific analysis.
        try
        {
            var analysis = SpectrumAnalyzer.Analyze(
                measurement.Points
            );

            return Results.Ok(analysis);
        }
        catch (ArgumentException ex)
        {
            // The stored data cannot be analysed safely.
            return Results.UnprocessableEntity(new
            {
                error = ex.Message
            });
        }
    });


// POST: Save a new measurement.
app.MapPost("/api/measurements",
    async (CreateMeasurementRequest request, SpectraLabDbContext db) =>
    {
        if (string.IsNullOrWhiteSpace(request.Name) ||
            request.Name.Length > 100 ||
            request.Points is null ||
            request.Points.Count < 2 ||
            request.Points.Count > 10000)
        {
            return Results.BadRequest(new
            {
                Error = "Provide a name (up to 100 characters) and 2-10000 points."
            });
        }

        if (request.Points.Any(p =>
            !double.IsFinite(p.Wavelength) ||
            !double.IsFinite(p.Intensity) ||
            p.Wavelength <= 0))
        {
            return Results.BadRequest(new
            {
                Error = "Measurement points must contain valid numeric values."
            });
        }

        var uniqueWavelengths = request.Points
            .Select(p => p.Wavelength)
            .Distinct()
            .Count();

        if (uniqueWavelengths != request.Points.Count)
        {
            return Results.BadRequest(new
            {
                Error = "Duplicate wavelength values are not allowed."
            });
        }

        var measurement = new Measurement
        {
            Name = request.Name.Trim(),
            Points = request.Points.Select(p => new SpectrumPoint
            {
                Wavelength = p.Wavelength,
                Intensity = p.Intensity
            }).ToList()
        };

        db.Measurements.Add(measurement);
        await db.SaveChangesAsync();

        return Results.Created(
            $"/api/measurements/{measurement.Id}",
            measurement
        );
    });

app.Run();

// Request data types.
record CreateMeasurementRequest(
    string? Name,
    List<SpectrumPointInput>? Points
);

record SpectrumPointInput(
    double Wavelength,
    double Intensity
);

public partial class Program { }