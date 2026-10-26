
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SpectraLab.Api.Data;

public class MeasurementApiTests
{
    // Replace the application's database with a test-only database.
    private static WebApplicationFactory<Program> CreateFactory(
        SqliteConnection connection)
    {
        return new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    // Remove the original database configuration.
                    services.RemoveAll<
                        DbContextOptions<SpectraLabDbContext>>();

                    services.RemoveAll<
                        IDbContextOptionsConfiguration<SpectraLabDbContext>>();

                    // Register the isolated SQLite database.
                    services.AddDbContext<SpectraLabDbContext>(
                        options => options.UseSqlite(connection));
                });
            });
    }

    [Fact]
    public async Task GetMeasurements_NewDatabase_ReturnsEmptyList()
    {
        // Each test gets its own empty database.
        using var connection = new SqliteConnection(
            "Data Source=:memory:");

        connection.Open();

        using var factory = CreateFactory(connection);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/measurements");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        Assert.Equal(0, json.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task PostMeasurement_ValidData_CanBeRetrieved()
    {
        using var connection = new SqliteConnection(
            "Data Source=:memory:");

        connection.Open();

        using var factory = CreateFactory(connection);
        using var client = factory.CreateClient();

        var payload = new
        {
            name = "Test Spectrum",
            points = new[]
            {
                new { wavelength = 400, intensity = 12.5 },
                new { wavelength = 450, intensity = 18.2 }
            }
        };

        // Create a new measurement.
        var postResponse = await client.PostAsJsonAsync(
            "/api/measurements", payload);

        Assert.Equal(
            HttpStatusCode.Created,
            postResponse.StatusCode);

        // Retrieve the saved measurement.
        var measurements =
            await client.GetFromJsonAsync<JsonElement>(
                "/api/measurements");

        Assert.Equal(1, measurements.GetArrayLength());

        var measurement = measurements[0];

        Assert.Equal(
            "Test Spectrum",
            measurement.GetProperty("name").GetString());

        Assert.Equal(
            2,
            measurement.GetProperty("points").GetArrayLength());
    }

    [Fact]
    public async Task PostMeasurement_InvalidData_ReturnsBadRequest()
    {
        using var connection = new SqliteConnection(
            "Data Source=:memory:");

        connection.Open();

        using var factory = CreateFactory(connection);
        using var client = factory.CreateClient();

        var payload = new
        {
            name = "",
            points = new[]
            {
                new { wavelength = 400, intensity = 12.5 }
            }
        };

        var response = await client.PostAsJsonAsync(
            "/api/measurements", payload);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }


    [Fact]
    public async Task GetAnalysis_ValidMeasurement_ReturnsCorrectResults()
    {
        using var connection = new SqliteConnection(
            "Data Source=:memory:");

        connection.Open();

        using var factory = CreateFactory(connection);
        using var client = factory.CreateClient();

        var payload = new
        {
            name = "Irregular Spectrum",
            points = new[]
            {
                new { wavelength = 550, intensity = 10.0 },
                new { wavelength = 400, intensity = 10.0 },
                new { wavelength = 450, intensity = 20.0 }
            }
        };

        var postResponse = await client.PostAsJsonAsync(
            "/api/measurements",
            payload
        );

        Assert.Equal(HttpStatusCode.Created, postResponse.StatusCode);

        // The POST response points to the newly created measurement.
        var analysisUrl = $"{postResponse.Headers.Location}/analysis";

        var response = await client.GetAsync(analysisUrl);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var analysis = await response.Content
            .ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            450,
            analysis.GetProperty("peakWavelengthNm").GetDouble()
        );

        Assert.Equal(
            20,
            analysis.GetProperty("peakIntensity").GetDouble()
        );

        Assert.Equal(
            2250,
            analysis.GetProperty("integratedIntensity").GetDouble()
        );
    }

    [Fact]
    public async Task PostMeasurement_DuplicateWavelengths_ReturnsBadRequest()
    {
        // Each test uses an isolated SQLite database.
        using var connection = new SqliteConnection(
            "Data Source=:memory:");

        connection.Open();

        using var factory = CreateFactory(connection);
        using var client = factory.CreateClient();

        // Two measurements have the same wavelength.
        var payload = new
        {
            name = "Duplicate Spectrum",
            points = new[]
            {
            new { wavelength = 400.0, intensity = 12.5 },
            new { wavelength = 400.0, intensity = 18.2 }
        }
        };

        // Try to save invalid data.
        var response = await client.PostAsJsonAsync(
            "/api/measurements",
            payload
        );

        // Invalid measurements should be rejected.
        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode
        );

        // Verify that invalid data was not saved.
        var measurements =
            await client.GetFromJsonAsync<JsonElement>(
                "/api/measurements");

        Assert.Equal(0, measurements.GetArrayLength());
    }

}
