using Microsoft.AspNetCore.Mvc;

namespace Blogs.Domain.Services
{
    public interface IStatsService
    {
        Task<(bool Success, string Message, int count)> GetStats([FromQuery] string? date, [FromQuery] string? user_id, [FromQuery] string? tag);
    }
}
