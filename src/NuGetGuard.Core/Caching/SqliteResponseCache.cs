using Microsoft.Data.Sqlite;

namespace NuGetGuard.Core.Caching;

public class SqliteResponseCache : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly TimeSpan _vulnTtl;
    private readonly TimeSpan _nugetTtl;

    public SqliteResponseCache(string? dbPath = null, TimeSpan? vulnTtl = null, TimeSpan? nugetTtl = null)
    {
        dbPath ??= GetDefaultDbPath();
        var dir = Path.GetDirectoryName(dbPath);
        if (dir is not null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _connection = new SqliteConnection($"Data Source={dbPath}");
        _connection.Open();
        _vulnTtl = vulnTtl ?? TimeSpan.FromHours(6);
        _nugetTtl = nugetTtl ?? TimeSpan.FromHours(24);

        InitializeDb();
    }

    public string? GetVulnCache(string key)
    {
        return GetCache("osv_cache", key, _vulnTtl);
    }

    public void SetVulnCache(string key, string responseJson)
    {
        SetCache("osv_cache", key, responseJson);
    }

    public string? GetNuGetCache(string key)
    {
        return GetCache("nuget_cache", key, _nugetTtl);
    }

    public void SetNuGetCache(string key, string responseJson)
    {
        SetCache("nuget_cache", key, responseJson);
    }

    private string? GetCache(string table, string key, TimeSpan ttl)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"SELECT response_json, cached_at FROM {table} WHERE key = @key";
        cmd.Parameters.AddWithValue("@key", key);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;

        var cachedAt = DateTimeOffset.Parse(reader.GetString(1));
        if (DateTimeOffset.UtcNow - cachedAt > ttl)
            return null;

        return reader.GetString(0);
    }

    private void SetCache(string table, string key, string responseJson)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $@"
            INSERT OR REPLACE INTO {table} (key, response_json, cached_at)
            VALUES (@key, @json, @cachedAt)";
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@json", responseJson);
        cmd.Parameters.AddWithValue("@cachedAt", DateTimeOffset.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    private void InitializeDb()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS osv_cache (
                key TEXT PRIMARY KEY,
                response_json TEXT NOT NULL,
                cached_at TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS nuget_cache (
                key TEXT PRIMARY KEY,
                response_json TEXT NOT NULL,
                cached_at TEXT NOT NULL
            );";
        cmd.ExecuteNonQuery();
    }

    private static string GetDefaultDbPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrEmpty(localAppData))
            localAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache");

        return Path.Combine(localAppData, "nugetguard", "cache.db");
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
