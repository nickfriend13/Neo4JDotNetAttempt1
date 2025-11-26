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

public class Health
{
    private readonly PerformanceCounter cpu;
    private readonly PerformanceCounter ram;
    private float CpuUsage;
    private float AvailableRAM;

    public Health()
    {
        cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        ram = new PerformanceCounter("Memory", "Available MBytes");
        CpuUsage = GetCurrentCpuUsage();
        AvailableRAM = GetAvailableRAM();  
    }

    public float GetCurrentCpuUsage()
    {
        CpuUsage = cpu.NextValue();
        Task.Delay(1000); // wait a second to get a valid reading
        return CpuUsage;
    }

    public float GetAvailableRAM()
    {
        AvailableRAM = ram.NextValue();
        return AvailableRAM;
    }

    public string GetHealthStatus()
    {
        return $"CPU Usage: {CpuUsage:F2}%, Available RAM: {AvailableRAM} MB";
    }
}



    public static class Program
    {

        // static async Task<List<Movie>> QueryMoviesAsync(IDriver driver, int queryId, CancellationToken token)
        // {
        //     var movies = new List<Movie>();
        //     Stopwatch stopwatch = new Stopwatch();
        //     stopwatch.Start();
        //     await using var session = driver.AsyncSession(o => o.WithDatabase("neo4j"));
            
        //     var cypher = "MATCH (n:Movie) RETURN n";
        //     var cursor = await session.RunAsync(cypher);
        //     var records = await cursor.ToListAsync(token);

        //     foreach (var record in records)
        //     {
        //         var node = record["n"]?.As<INode>();
        //         if (node == null) continue;

        //         node.Properties.TryGetValue("title", out var titleObj);
        //         node.Properties.TryGetValue("released", out var releasedObj);
        //         node.Properties.TryGetValue("tagline", out var taglineObj);

        //         var title = titleObj?.ToString() ?? "Unknown";
        //         var released = releasedObj.As<long>();
        //         var tagline = taglineObj?.ToString() ?? "";

        //         movies.Add(new Movie(title, released, tagline));
        //     }
        //     stopwatch.Stop();
        //     Console.WriteLine($"Query {queryId} finished with {movies.Count} movies in {stopwatch.ElapsedMilliseconds}ms");
        //     return movies;
        // }




public static async Task<List<(string Actor, string Movie, IDictionary<string, object> RelProps)>> GetActorsByFilmAsync(string filmTitle)
{
    var query = @"
        MATCH (actor:Person)-[r:ACTED_IN]->(movie:Movie {title: $title})
        RETURN actor.name AS Actor, movie.title AS Movie, properties(r) AS RelProps
    ";

    await using var driver = GraphDatabase.Driver("bolt://localhost:7687", AuthTokens.Basic("neo4j", "password1"));
    await driver.VerifyConnectivityAsync();
    var session = driver.AsyncSession();

    try
    {
        var result = await session.RunAsync(query, new { title = filmTitle });
        return await result.ToListAsync(record =>
        (
            Actor: record["Actor"].As<string>(),
            Movie: record["Movie"].As<string>(),
            RelProps: record["RelProps"].As<IDictionary<string, object>>()
        ));
    }
    finally
    {
        await session.CloseAsync();
    }
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
            Console.WriteLine("Connected to Neo4j database.");

            var app = builder.Build();

            app.UseHttpsRedirection();
            app.UseSwagger();
            app.UseSwaggerUI();


            app.MapGet("/", () => { return "go to /swagger "; });
            app.MapGet("/shutdown", () =>
            {//exit program gracefully
            
                Console.WriteLine("Shutting down... Press any key but q to exit.");
                if(Console.ReadLine() == "q")
                {
                   //keep running
                    Console.WriteLine("Continuing to run...");
                }
                else
                {
                    Console.WriteLine("Shutting down...");
                     Environment.Exit(0);
                }
            });
            app.MapGet("/isAlive", () => { return true; });

            app.MapGet("/health", () =>
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                var health = new Health();
                var output = Results.Json(new
                {
                    CpuUsage = health.GetCurrentCpuUsage(),
                    AvailableRam = health.GetAvailableRAM(),
                    Status = health.GetHealthStatus()
                });
                stopwatch.Stop(); Console.WriteLine($"Health Check Returned in: {stopwatch.ElapsedMilliseconds} ms\n");
                Console.WriteLine(health.GetHealthStatus());
                return output;
            });

            app.MapGet("/getAllNodes", async () =>
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                using var cts = new CancellationTokenSource();
                var nodes = await QueryCustomAsync(driver, "Match (n) return n", cts.Token);
                stopwatch.Stop(); Console.WriteLine($"All Nodes Returned in: {stopwatch.ElapsedMilliseconds} ms");
                return Results.Json(nodes, new JsonSerializerOptions { WriteIndented = true });
            });

           app.MapGet("/getAllRelationships", async () =>
            {
                var stopwatch = Stopwatch.StartNew();

                await using var session = driver.AsyncSession();
                var cursor = await session.RunAsync("MATCH (a)-[r]->(b) RETURN a, r, b");
                var records = await cursor.ToListAsync();

               var results = records.Select(record =>
                    {
                        var startNode = record["a"].As<INode>();
                        var rel = record["r"].As<IRelationship>();
                        var endNode = record["b"].As<INode>();

                        var dto = new 
                        {
                            Start = new  {
                                Id = startNode.Id,
                                Labels = startNode.Labels,
                                Properties = startNode.Properties
                            },
                            Relationship = new {
                                Type = rel.Type,
                                Roles = rel.TryGet<object>("roles", out var rolesObj) ? (rolesObj as List<object>)?.Select(r => r.ToString()).ToList() : null,
                            },
                            End = new  {
                                Id = endNode.Id,
                                Labels = endNode.Labels,
                                Properties = endNode.Properties
                            }
                        };

                        

                        return dto;
                    }).ToList();

                stopwatch.Stop();
                Console.WriteLine($"All Relationships Returned in: {stopwatch.ElapsedMilliseconds} ms");

                return Results.Json(results, new JsonSerializerOptions { WriteIndented = true });
            });

            app.MapGet("/findActorsbyFilm/{title}", async (string title) =>
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();

                // Query Neo4j
                var nodes = await GetActorsByFilmAsync(title);
                    
                // Project into Actor + Role pairs
              var actorsAndRole = nodes.Select(n => new {
                    Actor = n.Actor,
                    Role = n.RelProps.TryGetValue("roles", out var rolesObj) ? string.Join(", ", (rolesObj as List<object>)?.Select(r => r.ToString()) ?? [])
                        : "Unknown"
                }).ToList();

                Console.WriteLine($"Actors found for film '{title}':");
                foreach (var ar in actorsAndRole)
                {
                    Console.WriteLine($"Actor: {ar.Actor}, Role: {ar.Role}");
                }
                // Build response object
                var actorList = new
                {
                    Film = title,
                    Actors = actorsAndRole // ✅ fixed casing
                };

                stopwatch.Stop();
                Console.WriteLine($"Actors for film '{title}' returned in: {stopwatch.ElapsedMilliseconds} ms");

                return Results.Json(actorList, new JsonSerializerOptions { WriteIndented = true });
            });



            // Return movies directly as JSON
            app.MapGet("/movies", async () =>
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                
                using var cts = new CancellationTokenSource();
                var nodes = await QueryCustomAsync(driver, "Match (p:Movie) return p", cts.Token);
                    stopwatch.Stop(); Console.WriteLine($"All Movies Returned in: {stopwatch.ElapsedMilliseconds} ms"); 
                    return Results.Json(nodes, new JsonSerializerOptions { WriteIndented = true });
                
            });

            app.MapGet("/people", async () =>
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                using var cts = new CancellationTokenSource();
                var nodes = await QueryCustomAsync(driver, "Match (p:Person) return p", cts.Token);
                stopwatch.Stop(); Console.WriteLine($"All People Returned in: {stopwatch.ElapsedMilliseconds} ms"); 
                return Results.Json(nodes, new JsonSerializerOptions { WriteIndented = true });
            });
            app.MapPost("/title", async (string title) =>
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                var cypher = @"
                    MATCH (m:Movie)
                    WHERE toLower(m.title) CONTAINS toLower($title)
                    RETURN m
                ";

                await using var session = driver.AsyncSession();
                var cursor = await session.RunAsync(cypher, new { title });

                var results = await cursor.ToListAsync(record =>
                {
                    var node = record["m"].As<INode>();

                    return new
                    {
                        Title = node.Properties["title"].As<string>(),
                        Released = node.Properties["released"].As<long>(),
                        Tagline = node.Properties["tagline"].As<string>()
                    };
                });

                stopwatch.Stop();
                Console.WriteLine($"Movie search returned in {stopwatch.ElapsedMilliseconds}ms");

                if (results.Count == 0)
                    return Results.NotFound("No movies found");

                return Results.Json(results, new JsonSerializerOptions { WriteIndented = true });
            });

            // Custom query endpoint
            app.MapPost("/query", async (string cypher) =>
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                using var cts = new CancellationTokenSource();
                var nodes = await QueryCustomAsync(driver, cypher, cts.Token);
               
                stopwatch.Stop(); Console.WriteLine($"All Cypher Returned in: {stopwatch.ElapsedMilliseconds} ms");   
                return Results.Json(nodes, new JsonSerializerOptions { WriteIndented = true });
            });
            app.MapGet("/paths", async () =>
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                await using var session = driver.AsyncSession();

                var cursor = await session.RunAsync(
                    "MATCH p = (a)-[r]->(b) RETURN p as Path");

                var paths = await cursor.ToListAsync(record =>
                    record["Path"].As<IPath>());

                var dto = paths.Select(p => p).ToList();
                stopwatch.Stop(); Console.WriteLine($"All Paths Returned in: {stopwatch.ElapsedMilliseconds} ms");
                return Results.Json(dto, new JsonSerializerOptions { WriteIndented = true });
            });

            // Find movie by title
            

        static async Task<List<object>> QueryCustomAsync(IDriver driver, string cypher, CancellationToken token)
{
    var items = new List<object>();

    await using var session = driver.AsyncSession(o => o.WithDatabase("neo4j"));
    var cursor = await session.RunAsync(cypher);
    var records = await cursor.ToListAsync(token);

    foreach (var record in records)
{
    foreach (var kv in record.Values)
    {
        switch (kv.Value)
        {
            case INode node:
                items.Add(new {
                    Type = "Node",
                    Id = node.Id,
                    Labels = node.Labels,
                    Properties = node.Properties
                });
                break;

            case IRelationship rel:
                items.Add(new {
                    Type = "Relationship",
                    Id = rel.Id,
                    RelType = rel.Type,
                    StartNode = rel.StartNodeId,
                    EndNode = rel.EndNodeId,
                    Properties = rel.Properties
                });
                break;
        }
    }
}
    return items;
}
        static async Task<List<Object>> QueryRelationshipsAsync(IDriver driver, string cypher, CancellationToken token)
        {
            var relationships = new List<Object>();
            await using var session = driver.AsyncSession(o => o.WithDatabase("neo4j"));

            var cursor = await session.RunAsync(cypher);
            var records = await cursor.ToListAsync(token);

            foreach (var record in records)
            {
                var rel = record["r"].As<IRelationship>();
                relationships.Add(new {
                    Type = "Relationship",
                    Id = rel.Id,
                    RelType = rel.Type,
                    StartNode = rel.StartNodeId,
                    EndNode = rel.EndNodeId,
                    Properties = rel.Properties
                });
            }

            return relationships;    
        }
    await app.RunAsync();
}
}}
    

