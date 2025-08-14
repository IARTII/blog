namespace Blogs.Models
{
    public class PostWithComments
    {
        public Post Post { get; set; }
        public List<Comment> Comments { get; set; }  // <- здесь Comment, а не object
        public string CurrentUsername { get; set; }
    }

}
