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

namespace Neo4JDotNetAttempt1
{
    public class Movie
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

    public class NodeDto
    {
        public NodeDto(string id, Dictionary<string, object> properties)
        {
            Id = id;
            Properties = properties;
        }

        public string Id { get; set; }
        public Dictionary<string, object> Properties { get; set; }
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

            // Run multiple queries in parallel
            var tasks = Enumerable.Range(1, 3).Select(async i =>
            {
                using var perQueryCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
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
            var allMovies = allResults.SelectMany(m => m).ToList();
            var uniqueMovies = allMovies.GroupBy(m => m.Title).Select(g => g.First()).ToList();

            var app = builder.Build();

            app.UseHttpsRedirection();
            app.UseSwagger();
            app.UseSwaggerUI();

            app.MapGet("/", () => "see /swagger for instructions");

            // Return movies directly as JSON
            app.MapGet("/movies", () => uniqueMovies);

            // Custom query endpoint
            app.MapPost("/query", async (string cypher) =>
            {
                var nodes = await ExecuteCustomQuery(driver, cypher);
                return Results.Json(nodes);
            });

            // Find movie by title
            app.MapPost("/title", (string title) =>
            {
                var movie = uniqueMovies
                    .FirstOrDefault(m => m.Title.Equals(title, StringComparison.OrdinalIgnoreCase));

                return movie != null
                    ? Results.Json(movie)
                    : Results.NotFound("Movie not found");
            });

            app.Run();
        }

        private static async Task<List<NodeDto>> ExecuteCustomQuery(IDriver driver, string cypher)
        {
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var results = await QueryCustomAsync(driver, cypher, cts.Token);
            return results;
        }

        static async Task<List<NodeDto>> QueryCustomAsync(IDriver driver, string cypher, CancellationToken token)
        {
            var nodes = new List<NodeDto>();
            await using var session = driver.AsyncSession(o => o.WithDatabase("neo4j"));

            var cursor = await session.RunAsync(cypher);
            var records = await cursor.ToListAsync(token);

            foreach (var record in records)
            {
                var node = record.Values.FirstOrDefault().Value?.As<INode>();
                if (node != null)
                {
                    nodes.Add(new NodeDto(
                        node.Id.ToString(),
                        node.Properties.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    ));
                }
            }

            return nodes;
        }
    }
}