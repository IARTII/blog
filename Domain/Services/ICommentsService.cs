using Blogs.Models;

namespace Blogs.Domain.Services
{
    public interface ICommentsService
    {
        Task<(bool Success, string Message, PostWithComments Model)> Comments(int? postId);
        Task<(bool Success, string Message, Comment Model)> AddComment(Comment model);
    }
}
