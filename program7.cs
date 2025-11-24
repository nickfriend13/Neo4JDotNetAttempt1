using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Neo4j.Driver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

class Movie
{
    public Movie(string title, long released, string tagline)
    {
        Title = title;
        Released = released;
        Tagline = tagline;
    }
    public string Title { get; set; }
    public long Released { get; set; }
    public string Tagline { get; set; }

    public override string ToString() => $"\t{Title} ({Released}) - \"{Tagline}\"";
}

class Program
{
    static async Task<List<Movie>> QueryMoviesAsync(IDriver driver, int queryId, CancellationToken token)
    {
        var movies = new List<Movie>();
        await using var session = driver.AsyncSession(o => o.WithDatabase("neo4j"));
        var cypher = "MATCH (n:Movie) RETURN n";
        var cursor = await session.RunAsync(cypher);

        var records = await cursor.ToListAsync(token);

        foreach (var record in records)
        {
            var node = record["n"]?.As<INode>();
            if (node == null) continue;

            node.Properties.TryGetValue("title", out var titleObj);
            node.Properties.TryGetValue("released", out var releasedObj);
            node.Properties.TryGetValue("tagline", out var taglineObj);

            var title = titleObj?.ToString() ?? "Unknown";
            var released = releasedObj.As<long>();
            var tagline = taglineObj?.ToString() ?? "";

            movies.Add(new Movie(title, released, tagline));
        }

        Console.WriteLine($"Query {queryId} finished with {movies.Count} movies");
        return movies;
    }

    static async Task<List<INode>> QueryCustomAsync(IDriver driver, string cypher, CancellationToken token)
    {
        var nodes = new List<INode>();
        await using var session = driver.AsyncSession(o => o.WithDatabase("neo4j"));

        var stopwatch = Stopwatch.StartNew();
        var cursor = await session.RunAsync(cypher);
        var records = await cursor.ToListAsync(token);
        stopwatch.Stop();

        foreach (var record in records)
        {
            var node = record[0]?.As<INode>();
            if (node != null)
            {
                nodes.Add(node);
            }
        }

        Console.WriteLine(
            $"***Custom query {cypher} finished with {nodes.Count} nodes\n***" +
            $"***It took just {stopwatch.ElapsedMilliseconds}ms ***\n"
        );

        return nodes;
    }

    static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        await using var driver = GraphDatabase.Driver(
            "bolt://localhost:7687",
            AuthTokens.Basic("neo4j", "password1")
        );

        await driver.VerifyConnectivityAsync();

        var stopwatch = Stopwatch.StartNew();
        int numSecondsPerQuery = 30;

        var tasks = Enumerable.Range(1, 3).Select(async i =>
        {
            using var perQueryCts = new CancellationTokenSource(TimeSpan.FromSeconds(numSecondsPerQuery));
            try
            {
                return await QueryMoviesAsync(driver, i, perQueryCts.Token);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine($"Error: Query {i} timed out.");
                return new List<Movie>();
            }
        }).ToList();

        var allResults = await Task.WhenAll(tasks);
        stopwatch.Stop();

        Console.WriteLine($"\nTask.WhenAll: all queries finished in {stopwatch.ElapsedMilliseconds} ms");

        var allMovies = allResults.SelectMany(m => m).ToList();
        var uniqueMovies = allMovies.GroupBy(m => m.Title).Select(g => g.First()).ToList();

        string jsonString = JsonSerializer.Serialize(uniqueMovies, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(jsonString);

        var app = builder.Build();

        app.UseSwagger();
        app.UseSwaggerUI();

        app.MapGet("/", () => "see /swagger for instructions");
        app.MapGet("/movies", () => jsonString);
        app.MapPost("/query", async (string cypher) =>
        {
            var nodes = await ExecuteCustomQuery(cypher);
            return nodes;
        });
        app.MapPost("/title", (string title) =>
        {
            // Find the first movie that matches the title (case-insensitive)
            var movie = uniqueMovies
                .FirstOrDefault(m => m.Title.Equals(title, StringComparison.OrdinalIgnoreCase));

            // Return JSON if found, otherwise a message
            return movie != null
                ? JsonSerializer.Serialize(movie, new JsonSerializerOptions { WriteIndented = true })
                : "Movie not found";
        });

        app.Run();
    }

    private static async Task<string> ExecuteCustomQuery(string cypher)
    {
        try
        {
            await using var driver = GraphDatabase.Driver(
                "bolt://localhost:7687",
                AuthTokens.Basic("neo4j", "password1")
            );

            await driver.VerifyConnectivityAsync();
            var results = await QueryCustomAsync(driver, cypher, new CancellationTokenSource().Token);

            Console.WriteLine($"Custom query executed successfully with {results.Count} results.");
            var jsonString = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
            Console.WriteLine(jsonString);
            return jsonString;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error executing query: {ex.Message}");
            return $"Error executing query: {ex.Message}";
        }
    }
}
