using Neo4j.Driver;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

class Movie
{
    public Movie(string title, int released, string tagline)
    {
        Title = title;
        Released = released;
        Tagline = tagline;
    }
    public string Title { get; set; }
    public int Released { get; set; }
    public string Tagline { get; set; }

    public override string ToString()
    {
        return $"{Title} ({Released}) - \"{Tagline}\"";
    }
}

class Program
{
    static async Task Main()
    {
        await using var driver = GraphDatabase.Driver(
            "bolt://localhost:7687",
            AuthTokens.Basic("neo4j", "password1")
        );

        await driver.VerifyConnectivityAsync();

        var session = driver.AsyncSession(o => o.WithDatabase("neo4j"));

        var result = await session.RunAsync("MATCH (n:Movie) RETURN n");

        var movies = new List<Movie>();

        await foreach (var record in result)
        {
            var node = record["n"].As<INode>();

            // Extract properties safely
            var title = node.Properties.ContainsKey("title") ? node.Properties["title"].ToString() : "Unknown";
            var released = node.Properties.ContainsKey("released") ? Convert.ToInt32(node.Properties["released"]) : 0;
            var tagline = node.Properties.ContainsKey("tagline") ? node.Properties["tagline"].ToString() : "";

            // Create Movie object and add to list
            movies.Add(new Movie(title, released, tagline));
        }

        await session.CloseAsync();

        // Now output all movies from the list
        Console.WriteLine("Movies retrieved from Neo4j:\n");
        foreach (var movie in movies)
        {
            Console.WriteLine(movie);
        }
    }
}
