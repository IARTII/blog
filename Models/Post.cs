namespace Blogs.Models
{
    public class Post
    {
        public string id { get; set; }
        public string user_id { get; set; }
        public string title { get; set; }
        public string contend { get; set; }
        public DateTime created_at { get; set; }
        public string? image_url { get; set; }
    }

}
