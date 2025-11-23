//using Neo4j.Driver;
//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Threading;
//using System.Threading.Tasks;

//class Movie
//{
//    public Movie(string title, int released, string tagline)
//    {
//        Title = title;
//        Released = released;
//        Tagline = tagline;
//    }
//    public string Title { get; set; }
//    public int Released { get; set; }
//    public string Tagline { get; set; }

//    public override string ToString() => $"{Title} ({Released}) - \"{Tagline}\"";
//}

//class Program
//{
//    static async Task<List<Movie>> QueryMoviesAsync(IDriver driver, int queryId, CancellationToken token)
//    {
//        var movies = new List<Movie>();

//        // Pass cancellation token into session
//        await using var session = driver.AsyncSession(o => o.WithDatabase("neo4j"));

//        // RunAsync supports cancellation
//        var result = await session.RunAsync("MATCH (n:Movie) RETURN n");

//        await foreach (var record in result.WithCancellation(token))
//        {
//            var node = record["n"]?.As<INode>();
//            if (node == null) continue;

//            node.Properties.TryGetValue("title", out var titleObj);
//            node.Properties.TryGetValue("released", out var releasedObj);
//            node.Properties.TryGetValue("tagline", out var taglineObj);

//            var title = titleObj?.ToString() ?? "Unknown";
//            var released = releasedObj is int year ? year : 0;
//            var tagline = taglineObj?.ToString() ?? "";

//            movies.Add(new Movie(title, released, tagline));
//        }

//        Console.WriteLine($"Query {queryId} finished with {movies.Count} movies");
//        return movies;
//    }

//    static async Task Main()
//    {
//        await using var driver = GraphDatabase.Driver(
//            "bolt://localhost:7687",
//            AuthTokens.Basic("neo4j", "password1")
//        );

//        await driver.VerifyConnectivityAsync();

//        using var cts = new CancellationTokenSource();
//        cts.CancelAfter(TimeSpan.FromSeconds(2)); // auto‑cancel after 5 seconds

//        var tasks = Enumerable.Range(1, 100)
//                              .Select(i => QueryMoviesAsync(driver, i, cts.Token))
//                              .ToList();

//        var stopwatch = Stopwatch.StartNew();

//        try
//        {
//            // --- Task.WhenAll ---
//            var allResults = await Task.WhenAll(tasks);
//            stopwatch.Stop();
//            Console.WriteLine($"\nTask.WhenAll: all queries finished in {stopwatch.ElapsedMilliseconds} ms");

//            var allMovies = allResults.SelectMany(m => m).ToList();
//            Console.WriteLine($"Total movies aggregated: {allMovies.Count}\n");
//        }
//        catch (OperationCanceledException)
//        {
//            Console.WriteLine("⚠️ Queries cancelled due to timeout.");
//            Console.WriteLine($"Completed tasks before cancellation: {tasks.Count(t => t.IsCompletedSuccessfully)}\n");
//            Console.WriteLine($"{ stopwatch.ElapsedMilliseconds} ms");

//            // --- Task.WhenAny ---
//            stopwatch.Restart();
//            var firstFinished = await Task.WhenAny(tasks);
//            stopwatch.Stop();
//            Console.WriteLine($"Task.WhenAny: first query finished in {stopwatch.ElapsedMilliseconds} ms");

//            var firstMovies = await firstFinished; // unwrap result
//            Console.WriteLine($"First query returned {firstMovies.Count} movies\n");
//        }

       
//    }
//}