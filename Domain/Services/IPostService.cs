using Blogs.Models;
using Microsoft.AspNetCore.Mvc;

namespace Blogs.Domain.Services
{
    public interface IPostService
    {
        Task<(bool Success, string Message, List<Post> Posts)> Posts();
        Task<(bool Success, string Message)> NewPost(string title, string content, string tags, IFormFile? image);
        Task<(bool Success, string Message, object Data)> LikePost([FromBody] LikePostRequest request);
    }
}
