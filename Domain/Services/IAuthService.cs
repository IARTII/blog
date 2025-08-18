using Blogs.Models;

namespace Blogs.Domain.Services
{
    public interface IAuthService
    {
        Task<(bool Success, string Message)> Inlet(User request);
        Task<(bool Success, string Message)> Register(User request);
        Task<(bool Success, string Message)> Logout();
    }
}
