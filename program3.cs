//using Neo4j.Driver;
//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
//using System.Threading;

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
//    static List<Movie> QueryMovies(IDriver driver, int queryId, CancellationToken token)
//    {
//        var movies = new List<Movie>();

//        using var session = driver.AsyncSession(o => o.WithDatabase("neo4j")); // sync session

//        var result = session.Run("MATCH (n:Movie) RETURN n");

//        foreach (var record in result)
//        {
//            if (token.IsCancellationRequested) break;

//            var node = record["n"].As<INode>();
//            if (node == null) continue;

//            node.Properties.TryGetValue("title", out var titleObj);
//            node.Properties.TryGetValue("released", out var releasedObj);
//            node.Properties.TryGetValue("tagline", out var taglineObj);

//            var title = titleObj?.ToString() ?? "Unknown";
//            var released = releasedObj is int year ? year : 0;
//            var tagline = taglineObj?.ToString() ?? "";

//            movies.Add(new Movie(title, released, tagline));
//        }

//        Console.WriteLine($"Thread {queryId} finished with {movies.Count} movies");
//        return movies;
//    }

//    static void Main()
//    {
//        using var driver = GraphDatabase.Driver(
//            "bolt://localhost:7687",
//            AuthTokens.Basic("neo4j", "password1")
//        );

//        await driver.VerifyConnectivityAsync();

//        using var cts = new CancellationTokenSource();
//        cts.CancelAfter(TimeSpan.FromSeconds(15));

//        var stopwatch = Stopwatch.StartNew();

//        var threads = new List<Thread>();
//        var results = new List<List<Movie>>();
//        var lockObj = new object();

//        // Spin up 5 threads
//        for (int i = 1; i <= 5; i++)
//        {
//            int queryId = i;
//            var thread = new Thread(() =>
//            {
//                try
//                {
//                    var movies = QueryMovies(driver, queryId, cts.Token);
//                    lock (lockObj)
//                    {
//                        results.Add(movies);
//                    }
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"Thread {queryId} failed: {ex.Message}");
//                }
//            });

//            threads.Add(thread);
//            thread.Start();
//        }

//        // Wait for all threads
//        foreach (var t in threads)
//            t.Join();

//        stopwatch.Stop();

//        Console.WriteLine($"\nAll threads finished in {stopwatch.ElapsedMilliseconds} ms");

//        var allMovies = results.SelectMany(m => m).ToList();
//        Console.WriteLine($"Total movies aggregated: {allMovies.Count}\n");
//    }
//}