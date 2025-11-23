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

//// Custom async enumerable with stopwatch
//class MovieQueryEnumerable : IAsyncEnumerable<Movie>
//{
//    private readonly IDriver _driver;
//    private readonly string _cypher;
//    private readonly CancellationToken _token;

//    public MovieQueryEnumerable(IDriver driver, string cypher, CancellationToken token)
//    {
//        _driver = driver;
//        _cypher = cypher;
//        _token = token;
//    }

//    public async IAsyncEnumerator<Movie> GetAsyncEnumerator(CancellationToken cancellationToken = default)
//    {
//        var stopwatch = Stopwatch.StartNew();

//        await using var session = _driver.AsyncSession(o => o.WithDatabase("neo4j"));
//        var result = await session.RunAsync(_cypher);

//        int count = 0;
//        await foreach (var record in result.WithCancellation(_token))
//        {
//            var node = record["n"]?.As<INode>();
//            if (node == null) continue;

//            node.Properties.TryGetValue("title", out var titleObj);
//            node.Properties.TryGetValue("released", out var releasedObj);
//            node.Properties.TryGetValue("tagline", out var taglineObj);

//            var title = titleObj?.ToString() ?? "Unknown";
//            var released = releasedObj is int year ? year : 0;
//            var tagline = taglineObj?.ToString() ?? "";

//            count++;
//            yield return new Movie(title, released, tagline);
//        }

//        stopwatch.Stop();
//        Console.WriteLine($"Query finished: {count} movies streamed in {stopwatch.ElapsedMilliseconds} ms");
//    }
//}

//class Program
//{
//    static async Task Main()
//    {
//        await using var driver = GraphDatabase.Driver(
//            "bolt://localhost:7687",
//            AuthTokens.Basic("neo4j", "password1")
//        );

//        await driver.VerifyConnectivityAsync();

//        using var cts = new CancellationTokenSource();
//        cts.CancelAfter(TimeSpan.FromSeconds(10)); // cancel if query takes too long

//        var moviesEnumerable = new MovieQueryEnumerable(driver, "MATCH (n:Movie) RETURN n", cts.Token);

//        Console.WriteLine("Streaming movies from Neo4j...\n");

//        await foreach (var movie in moviesEnumerable)
//        {
//            Console.WriteLine(movie);
//        }

//        Console.WriteLine("\nDone.");
//    }
//}