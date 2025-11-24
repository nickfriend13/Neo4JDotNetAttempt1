namespace  Neo4JDotNetAttempt1.Models
{
    public class MovieBuilder
    {
        private string _title = "Untitled";
        private long _released = 0;
        private string _tagline = "";

        public MovieBuilder setTitle(string title)
        {
            _title = title;
            return this;
        }

        public MovieBuilder setReleased(long released)
        {
            _released = released;
            return this;
        }

        public MovieBuilder setTagline(string tagline)
        {
            _tagline = tagline;
            return this;
        }

        public Movie Build()
        {
            return new Movie(_title, _released, _tagline);
        }
    }
}