using Npgsql;
using System.Data;

namespace YourNamespace.Postgres
{
    public class Db
    {
        private readonly string _connectionString;

        public Db(IConfiguration config)
        {
            _connectionString = config.GetConnectionString("DefaultConnection");
        }

        public async Task<bool> UserExistsAsync(string username)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand("SELECT 1 FROM users WHERE username = @username LIMIT 1", conn);
            cmd.Parameters.AddWithValue("username", username);

            var result = await cmd.ExecuteScalarAsync();
            return result != null;
        }

        public async Task CreateUserAsync(string username, string passwordHash)
        {
            await using var conn = new NpgsqlConnection(_connectionString);
            await conn.OpenAsync();

            var cmd = new NpgsqlCommand(
                "INSERT INTO users (username, password_hash) VALUES (@username, @password)", conn);

            cmd.Parameters.AddWithValue("username", username);
            cmd.Parameters.AddWithValue("password", passwordHash);

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
