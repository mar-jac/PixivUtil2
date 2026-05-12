using Microsoft.Data.Sqlite;
using PixivUtil.Windows.Models;

namespace PixivUtil.Windows.Services;

public sealed class PixivDatabase(PixivSettingsStore settingsStore)
{
    public async Task<IReadOnlyList<DownloadHistoryItem>> LoadRecentHistoryAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        var settings = settingsStore.Load();
        if (!File.Exists(settings.DatabasePath))
        {
            return [];
        }

        var results = new List<DownloadHistoryItem>();
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = settings.DatabasePath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await AddRowsAsync(
            connection,
            """
            SELECT 'Pixiv' AS source,
                   CAST(image_id AS TEXT) AS id,
                   COALESCE(title, '') AS title,
                   CAST(member_id AS TEXT) AS member_id,
                   save_name AS local_path,
                   last_update_date
            FROM pixiv_master_image
            WHERE save_name IS NOT NULL AND save_name <> 'N/A'
            ORDER BY datetime(last_update_date) DESC
            LIMIT $limit
            """,
            limit,
            settings.RootDirectory,
            results,
            cancellationToken);

        await AddRowsAsync(
            connection,
            """
            SELECT 'FANBOX' AS source,
                   CAST(post_id AS TEXT) AS id,
                   COALESCE(title, '') AS title,
                   CAST(member_id AS TEXT) AS member_id,
                   NULL AS local_path,
                   last_update_date
            FROM fanbox_master_post
            ORDER BY datetime(last_update_date) DESC
            LIMIT $limit
            """,
            limit,
            settings.RootDirectory,
            results,
            cancellationToken);

        await AddRowsAsync(
            connection,
            """
            SELECT 'Sketch' AS source,
                   CAST(post_id AS TEXT) AS id,
                   COALESCE(title, '') AS title,
                   CAST(member_id AS TEXT) AS member_id,
                   NULL AS local_path,
                   last_update_date
            FROM sketch_master_post
            ORDER BY datetime(last_update_date) DESC
            LIMIT $limit
            """,
            limit,
            settings.RootDirectory,
            results,
            cancellationToken);

        return results
            .OrderByDescending(static item => item.LastUpdated)
            .Take(limit)
            .ToList();
    }

    private static async Task AddRowsAsync(
        SqliteConnection connection,
        string sql,
        int limit,
        string rootDirectory,
        ICollection<DownloadHistoryItem> target,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("$limit", limit);

        try
        {
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                target.Add(new DownloadHistoryItem(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetString(3),
                    NormalizePath(reader.IsDBNull(4) ? null : reader.GetString(4), rootDirectory),
                    ParseDate(reader.IsDBNull(5) ? null : reader.GetString(5))));
            }
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 1)
        {
            // Older or partial databases may not have every PixivUtil table yet.
        }
    }

    private static DateTimeOffset? ParseDate(string? value)
    {
        return DateTimeOffset.TryParse(value, out var parsed)
            ? parsed
            : null;
    }

    private static string? NormalizePath(string? path, string rootDirectory)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(Path.Combine(rootDirectory, path));
    }
}
