//using Neo4j.Driver;
//using System;
//using System.Collections.Generic;
//using System.Diagnostics;
//using System.Linq;
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
//    static async Task<List<Movie>> QueryMoviesAsync(IDriver driver)
//    {
//        var movies = new List<Movie>();

//        await using var session = driver.AsyncSession(o => o.WithDatabase("neo4j"));
//        var result = await session.RunAsync("MATCH (n:Movie) RETURN n");


//        await foreach (var record in result)
//        {
//            var node = record["n"].As<INode>();

//            var title = node.Properties.ContainsKey("title") ? node.Properties["title"].ToString() : "Unknown";
//            var released = node.Properties.ContainsKey("released") ? Convert.ToInt32(node.Properties["released"]) : 0;
//            var tagline = node.Properties.ContainsKey("tagline") ? node.Properties["tagline"].ToString() : "";

//            movies.Add(new Movie(title, released, tagline));
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

//        var stopwatch = Stopwatch.StartNew();

//        // Launch 100 queries in parallel
//        var tasks = Enumerable.Range(1, 100)
//                              .Select(i => QueryMoviesAsync(driver))
//                              .ToList();

//        var results = await Task.WhenAll(tasks);

//        stopwatch.Stop();

//        // Flatten results (each task returns a list of movies)
//        var allMovies = results.SelectMany(m => m).ToList();

//        Console.WriteLine($"Executed 100 parallel queries in {stopwatch.ElapsedMilliseconds} ms\n");

//        // Deduplicate by Title
//        var uniqueMovies = allMovies
//            .GroupBy(m => m.Title)             // group by title
//            .Select(g => g.First())            // take the first movie in each group
//            .ToList();

//        Console.WriteLine($"Total movies retrieved across all queries: {allMovies.Count}");
//        Console.WriteLine($"Unique movies after deduplication: {uniqueMovies.Count}\n");

//        foreach (var movie in uniqueMovies)
//        {
//            Console.WriteLine(movie);
//        }

//        // Print a sample from each query
//        for (int i = 0; i < results.Length; i++)
//        {
//            Console.WriteLine($"Query {i + 1} returned {results[i].Count} movies");
//        }
//    }
//}
