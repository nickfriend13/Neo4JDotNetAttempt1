//using Neo4j.Driver;
//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Threading;
//using System.Threading.Tasks;

//class Movie
//{
//    public string Title { get; }
//    public int Released { get; }
//    public string Tagline { get; }

//    public Movie(string title, int released, string tagline)
//    {
//        Title = title;
//        Released = released;
//        Tagline = tagline;
//    }

//    public override string ToString() => $"{Title} ({Released}) - \"{Tagline}\"";
//}

//class Program
//{
//    static List<Movie> QueryMovies(IDriver driver, int queryId, CancellationToken token)
//    {
//        var stopwatch = Stopwatch.StartNew();
//        var movies = new List<Movie>();

//        try
//        {
//            var session = driver.Session(o => o.WithDatabase("neo4j"));
//            var result = session.Run("MATCH (n:Movie) RETURN n");

//            // Explicit enumerator (no LINQ)
//            var enumerator = result.GetEnumerator();
//            while (enumerator.MoveNext())
//            {
//                if (token.IsCancellationRequested)
//                {
//                    Console.WriteLine($"⚠️ Query {queryId} cancelled after {stopwatch.ElapsedMilliseconds} ms");
//                    break;
//                }

//                var record = enumerator.Current;
//                var node = record["n"].As<INode>();
//                if (node == null) continue;

//                node.Properties.TryGetValue("title", out var titleObj);
//                node.Properties.TryGetValue("released", out var releasedObj);
//                node.Properties.TryGetValue("tagline", out var taglineObj);

//                var title = titleObj?.ToString() ?? "Unknown";
//                var released = releasedObj is int year ? year : 0;
//                var tagline = taglineObj?.ToString() ?? "";

//                movies.Add(new Movie(title, released, tagline));
//            }

//            stopwatch.Stop();
//            Console.WriteLine($"Query {queryId} finished with {movies.Count} movies in {stopwatch.ElapsedMilliseconds} ms");
//        }
//        catch (OperationCanceledException)
//        {
//            stopwatch.Stop();
//            Console.WriteLine($"⚠️ Query {queryId} timed out after {stopwatch.ElapsedMilliseconds} ms");
//        }

//        return movies;
//    }

//    static async Task Main()
//    {
//        await using var driver = GraphDatabase.Driver(
//            "bolt://localhost:7687",
//            AuthTokens.Basic("neo4j", "password1")
//        );

//        await driver.VerifyConnectivityAsync();

//        var tasks = new List<Task<List<Movie>>>();

//        // Launch 5 queries with per-query CTS (2s timeout each)
//        for (int i = 1; i <= 5; i++)
//        {
//            int queryId = i;
//            tasks.Add(Task.Run(() =>
//            {
//                using var perQueryCts = new CancellationTokenSource();
//                perQueryCts.CancelAfter(TimeSpan.FromSeconds(2)); // per-query timeout
//                return QueryMovies(driver, queryId, perQueryCts.Token);
//            }));
//        }

//        try
//        {
//            var allResults = await Task.WhenAll(tasks);

//            // Flatten results manually (no LINQ)
//            var allMovies = new List<Movie>();
//            foreach (var movieList in allResults)
//            {
//                foreach (var movie in movieList)
//                {
//                    allMovies.Add(movie);
//                }
//            }

//            Console.WriteLine($"\nTotal movies aggregated: {allMovies.Count}");
//        }
//        catch (OperationCanceledException)
//        {
//            Console.WriteLine("⚠️ One or more queries cancelled.");
//        }
//    }
//}
