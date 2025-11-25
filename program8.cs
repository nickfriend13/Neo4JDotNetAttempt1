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
        Thread.Sleep(1000); // wait a second to get a valid reading
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
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();
                using var cts = new CancellationTokenSource();
                var nodes = await QueryRelationshipsAsync(driver, "Match ()-[r]->() return r", cts.Token);
                stopwatch.Stop(); Console.WriteLine($"All Relationships Returned in: {stopwatch.ElapsedMilliseconds} ms");
                return Results.Json(nodes, new JsonSerializerOptions { WriteIndented = true });
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

            // Find movie by title
            app.MapPost("/title", async (string title) =>
            {
                Stopwatch stopwatch = new Stopwatch();
                stopwatch.Start();

                using var cts = new CancellationTokenSource();
                var nodes = await QueryCustomAsync(driver, "Match (p:Movie) return p", cts.Token);
                
               var movieBuilder = new Neo4JDotNetAttempt1.Models.MovieBuilder();
               var movies = nodes
                .Where(node => node.Properties["title"].ToString()
                    .Contains(title, StringComparison.OrdinalIgnoreCase))
                .Select(node => {
                    movieBuilder.setTitle(node.Properties["title"].ToString());
                    movieBuilder.setReleased(node.Properties["released"].As<long>());
                    movieBuilder.setTagline(node.Properties["tagline"].ToString());
                    return movieBuilder.Build();
                    }
            )
               .ToList();


                stopwatch.Stop(); Console.WriteLine($"\nMovie(s) Found in: {stopwatch.ElapsedMilliseconds} ms\n"); 
                
                if (nodes.Count == 0)
                {
                    return Results.NotFound("No movies found in database");
                }
                else
                    return Results.Json(movies, new JsonSerializerOptions { WriteIndented = true });

            });
            Console.WriteLine("SERVING ON WEB port 5000\n");
            
            await app.RunAsync();
            
        }


        static async Task<List<INode>> QueryCustomAsync(IDriver driver, string cypher, CancellationToken token)
        {
            var nodes = new List<INode>();
            await using var session = driver.AsyncSession(o => o.WithDatabase("neo4j"));

            var cursor = await session.RunAsync(cypher);
            var records = await cursor.ToListAsync(token);

            foreach (var record in records)
            {
                var node = record.Values.FirstOrDefault().Value?.As<INode>();
                if (node != null)
                {
                    nodes.Add(node);
                }
            }

            return nodes;
        }
    
        static async Task<List<IRelationship>> QueryRelationshipsAsync(IDriver driver, string cypher, CancellationToken token)
        {
            var relationships = new List<IRelationship>();
            await using var session = driver.AsyncSession(o => o.WithDatabase("neo4j"));

            var cursor = await session.RunAsync(cypher);
            var records = await cursor.ToListAsync(token);

            foreach (var record in records)
            {
                var relationship = record.Values.FirstOrDefault().Value?.As<IRelationship>();
                if (relationship != null)
                {
                    relationships.Add(relationship);
                }
            }

            return relationships;    
        }
    
}
}
    

