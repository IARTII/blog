namespace Blogs.Models
{
    public class Comment
    {
        public int postId { get; set; }
        public string text { get; set; }
        public string username { get; set; }
        public DateTime created_at { get; set; }
    }
}
