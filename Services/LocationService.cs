using System.Data;
using System.Data.Common;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using PubSearchSite.Models;

namespace PubSearchSite.Services;

/// <summary>
/// Defines the contract for location and document retrieval services.
/// </summary>
public interface ILocationService
{
    /// <summary>
    /// Retrieves all LM site names.
    /// </summary>
    Task<IReadOnlyList<string>> GetLmSitesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all Considered site names.
    /// </summary>
    Task<IReadOnlyList<string>> GetConsideredSitesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all CERCLA site names.
    /// </summary>
    Task<IReadOnlyList<string>> GetCerclaSitesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves documents for a specific LM site.
    /// </summary>
    Task<DocumentResult> GetLmDocumentsAsync(string site, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves details for a specific Considered site.
    /// </summary>
    Task<SiteDetails?> GetConsideredSiteDetailsAsync(string filterName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves documents for a specific Considered site.
    /// </summary>
    Task<IReadOnlyList<DocumentItem>> GetConsideredSiteDocumentsAsync(string filterName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the Administrative Record Index for a CERCLA site.
    /// </summary>
    Task<IReadOnlyList<CerclaArIndexItem>> GetCerclaArIndexAsync(string site, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all site documents for a CERCLA site.
    /// </summary>
    Task<IReadOnlyList<CerclaSiteDocumentItem>> GetCerclaSiteDocumentsAsync(string site, CancellationToken cancellationToken = default);
}

/// <summary>
/// Provides data access services for location and document retrieval from SQL Server.
/// </summary>
public sealed class LocationService : ILocationService
{
    private readonly string _connectionString;
    private readonly ILogger<LocationService> _logger;
    private readonly IMemoryCache _cache;

    private const int DefaultCommandTimeout = 120;
    private const int LargeTableCommandTimeout = 300;
    private const int CerclaQueryTimeout = 600;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    private const string CerclaSitesCacheKey = "CerclaSites";

    public LocationService(IConfiguration configuration, ILogger<LocationService> logger, IMemoryCache cache)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _connectionString = configuration.GetConnectionString("PubSearch")
            ?? throw new InvalidOperationException("Connection string 'PubSearch' is missing from configuration.");
    }

    #region Public Methods

    public async Task<IReadOnlyList<string>> GetLmSitesAsync(CancellationToken cancellationToken = default)
    {
        var sites = new List<string>();

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT [Site] 
                FROM [PubSearch].[dbo].[LMandConsideredSites] 
                GROUP BY [Site]";
            command.CommandType = CommandType.Text;
            command.CommandTimeout = DefaultCommandTimeout;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var siteValue = SafeGetString(reader, "Site").Trim();
                if (string.IsNullOrWhiteSpace(siteValue)) continue;
                if (siteValue.Contains(' ') || siteValue.Contains('-')) continue;
                sites.Add(siteValue);
            }
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Failed to load sites from LMandConsideredSites table");
        }

        return sites
            .Where(s => !s.Contains('-') && !s.Contains(' '))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetConsideredSitesAsync(CancellationToken cancellationToken = default)
    {
        var sites = new List<string>();

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT [Filter_x0020_Name] 
                FROM [PubSearch].[dbo].[ConsideredSiteDetails] 
                ORDER BY [Filter_x0020_Name]";
            command.CommandType = CommandType.Text;
            command.CommandTimeout = DefaultCommandTimeout;

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var filterValue = SafeGetString(reader, "Filter_x0020_Name").Trim();
                if (!string.IsNullOrWhiteSpace(filterValue))
                {
                    sites.Add(filterValue);
                }
            }
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Failed to load sites from ConsideredSiteDetails table");
        }

        return sites
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();
    }

    public async Task<IReadOnlyList<string>> GetCerclaSitesAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CerclaSitesCacheKey, out IReadOnlyList<string>? cachedSites) && cachedSites != null)
        {
            return cachedSites;
        }

        var sites = new List<string>();

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(CancellationToken.None);

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT DISTINCT [Sites] 
                FROM [PubSearch].[dbo].[CERCLA] 
                WHERE [Sites] IS NOT NULL AND [Sites] <> ''";
            command.CommandType = CommandType.Text;
            command.CommandTimeout = CerclaQueryTimeout;

            await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);
            while (await reader.ReadAsync(CancellationToken.None))
            {
                var value = SafeGetString(reader, "Sites").Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    sites.Add(value);
                }
            }
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Failed to load sites from CERCLA table");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading CERCLA sites");
        }

        var finalSites = sites
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s)
            .ToList();

        if (finalSites.Count > 0)
        {
            _cache.Set(CerclaSitesCacheKey, (IReadOnlyList<string>)finalSites, CacheDuration);
            _logger.LogInformation("Cached {Count} CERCLA sites", finalSites.Count);
        }

        return finalSites;
    }

    public async Task<DocumentResult> GetLmDocumentsAsync(string site, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(site))
        {
            return new DocumentResult(Array.Empty<DocumentItem>(), Array.Empty<DocumentItem>());
        }

        var allDocs = new List<DocumentItem>();
        var keyDocs = new List<DocumentItem>();

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var hasDateColumn = await ColumnExistsAsync(connection, "LMandConsideredSites", "LegacyDateCreated", cancellationToken);

            var dateSelect = hasDateColumn
                ? "CASE WHEN [LegacyDateCreated] IS NOT NULL THEN CONVERT(VARCHAR(50), [LegacyDateCreated], 101) ELSE '' END AS DatePosted"
                : "'' AS DatePosted";
            var orderBy = hasDateColumn ? "ORDER BY [LegacyDateCreated] DESC" : "ORDER BY [Title]";

            await using var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT 
                    COALESCE([DocDesc], [Title], [FileLeafRef]) AS Title,
                    COALESCE([Document_x0020_Category], '') AS DocumentCategory,
                    COALESCE([State], '') AS State,
                    {dateSelect},
                    [FileLeafRef],
                    COALESCE([Key_x0020_Document], 0) AS IsKeyDocument
                FROM [PubSearch].[dbo].[LMandConsideredSites]
                WHERE [Site] = @site
                  AND COALESCE([DocDesc], [Title], [FileLeafRef]) IS NOT NULL 
                  AND COALESCE([DocDesc], [Title], [FileLeafRef]) <> ''
                {orderBy}";
            command.CommandType = CommandType.Text;
            command.CommandTimeout = DefaultCommandTimeout;
            command.Parameters.AddWithValue("@site", site);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var title = SafeGetString(reader, "Title").Trim();
                if (string.IsNullOrWhiteSpace(title)) continue;

                var docItem = new DocumentItem
                {
                    Title = title,
                    Category = SafeGetString(reader, "DocumentCategory").Trim(),
                    State = SafeGetString(reader, "State").Trim(),
                    DatePosted = SafeGetString(reader, "DatePosted").Trim(),
                    FilePath = SafeGetString(reader, "FileLeafRef").Trim()
                };

                allDocs.Add(docItem);

                var isKeyDocValue = SafeGetString(reader, "IsKeyDocument");
                if (int.TryParse(isKeyDocValue, out var isKeyDoc) && isKeyDoc == 1)
                {
                    keyDocs.Add(docItem);
                }
            }

            keyDocs = keyDocs
                .OrderBy(d => DateTime.TryParse(d.DatePosted, out var dt) ? dt : DateTime.MinValue)
                .ToList();

            _logger.LogInformation("Loaded {KeyCount} key documents and {TotalCount} documents for site {Site}",
                keyDocs.Count, allDocs.Count, site);
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Failed to load documents for site {Site}", site);
        }

        return new DocumentResult(keyDocs, allDocs);
    }

    public async Task<SiteDetails?> GetConsideredSiteDetailsAsync(string filterName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filterName))
        {
            return null;
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var availableColumns = await GetTableColumnsAsync(connection, "ConsideredSiteDetails", cancellationToken);
            if (availableColumns.Count == 0)
            {
                return null;
            }

            var requiredColumns = new[]
            {
                "Designated_x0020_Name", "Alternate_x0020_Name", "Location", "Evaluation_x0020_Year",
                "Site_x0020_Operations", "Site_x0020_Disposition", "Radioactive_x0020_Materials_x002",
                "Primary_x0020_Radioactive_x0020_", "Radiological_x0020_Surveys", "Site_x0020_Status",
                "Site_x0020_Summary", "SiteLink1"
            };

            var selectColumns = requiredColumns
                .Where(c => availableColumns.Contains(c))
                .Select(c => $"[{c}]")
                .ToList();

            if (selectColumns.Count == 0)
            {
                return null;
            }

            var normalizedForDb = filterName.Replace(" ", "_");
            var hasLmcsNames = availableColumns.Contains("LMCS_x0020_Names");

            var whereClause = hasLmcsNames
                ? @"([Filter_x0020_Name] = @filterName OR [Filter_x0020_Name] = @normalizedFilter OR 
                    REPLACE([Filter_x0020_Name], '_', ' ') = @filterName OR
                    [LMCS_x0020_Names] = @filterName OR [LMCS_x0020_Names] = @normalizedFilter OR 
                    REPLACE([LMCS_x0020_Names], '_', ' ') = @filterName)"
                : @"([Filter_x0020_Name] = @filterName OR [Filter_x0020_Name] = @normalizedFilter OR 
                    REPLACE([Filter_x0020_Name], '_', ' ') = @filterName)";

            await using var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT TOP 1 {string.Join(", ", selectColumns)}
                FROM [PubSearch].[dbo].[ConsideredSiteDetails]
                WHERE {whereClause}";
            command.CommandType = CommandType.Text;
            command.CommandTimeout = DefaultCommandTimeout;
            command.Parameters.AddWithValue("@filterName", filterName);
            command.Parameters.AddWithValue("@normalizedFilter", normalizedForDb);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                return new SiteDetails
                {
                    DesignatedName = SafeGetString(reader, "Designated_x0020_Name"),
                    AlternateName = SafeGetString(reader, "Alternate_x0020_Name"),
                    Location = SafeGetString(reader, "Location"),
                    EvaluationYear = SafeGetString(reader, "Evaluation_x0020_Year"),
                    SiteOperations = SafeGetString(reader, "Site_x0020_Operations"),
                    SiteDisposition = SafeGetString(reader, "Site_x0020_Disposition"),
                    RadioactiveMaterialsHandled = SafeGetString(reader, "Radioactive_x0020_Materials_x002"),
                    PrimaryRadioactiveMaterialsHandled = SafeGetString(reader, "Primary_x0020_Radioactive_x0020_"),
                    RadiologicalSurveys = SafeGetString(reader, "Radiological_x0020_Surveys"),
                    SiteStatus = SafeGetString(reader, "Site_x0020_Status"),
                    SiteSummary = SafeGetString(reader, "Site_x0020_Summary"),
                    LmSite = SafeGetString(reader, "SiteLink1")
                };
            }
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Failed to load site details for filter {FilterName}", filterName);
        }

        return null;
    }

    public async Task<IReadOnlyList<DocumentItem>> GetConsideredSiteDocumentsAsync(string filterName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filterName))
        {
            return Array.Empty<DocumentItem>();
        }

        var documents = new List<DocumentItem>();

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            var availableColumns = await GetTableColumnsAsync(connection, "LMandConsideredSites", cancellationToken);

            var hasDocDesc = availableColumns.Contains("DocDesc");
            var hasTitle = availableColumns.Contains("Title");
            var hasFileLeafRef = availableColumns.Contains("FileLeafRef");
            var hasDocumentCategory = availableColumns.Contains("Document_x0020_Category");
            var hasState = availableColumns.Contains("State");
            var hasLmcsNames = availableColumns.Contains("LMCSNames") || availableColumns.Contains("LMCS_x0020_Names");
            var hasFilterName = availableColumns.Contains("Filter_x0020_Name");
            var hasLegacyDateCreated = availableColumns.Contains("LegacyDateCreated");
            var hasDateCreated = availableColumns.Contains("Date_x0020_Created");
            var hasDatePosted = availableColumns.Contains("Date_x0020_Posted");

            var titleParts = new List<string>();
            if (hasDocDesc) titleParts.Add("[DocDesc]");
            if (hasTitle) titleParts.Add("[Title]");
            if (hasFileLeafRef) titleParts.Add("[FileLeafRef]");

            if (titleParts.Count == 0)
            {
                return documents;
            }

            var titleColumn = titleParts.Count == 1
                ? $"{titleParts[0]} AS Title"
                : $"COALESCE({string.Join(", ", titleParts)}) AS Title";

            var categoryColumn = hasDocumentCategory
                ? "COALESCE([Document_x0020_Category], '') AS DocumentCategory"
                : "'' AS DocumentCategory";

            var siteTypeColumn = availableColumns.Contains("SiteType")
                ? "COALESCE([SiteType], '') AS SiteType"
                : "'' AS SiteType";

            var lmcsNamesColumn = availableColumns.Contains("LMCSNames")
                ? "COALESCE([LMCSNames], '') AS LMCSNames"
                : "'' AS LMCSNames";

            var stateColumn = hasState
                ? "COALESCE([State], '') AS State"
                : "'' AS State";

            string dateColumn;
            if (hasDateCreated)
                dateColumn = "CASE WHEN [Date_x0020_Created] IS NOT NULL THEN CONVERT(VARCHAR(50), [Date_x0020_Created], 101) ELSE '' END AS DateCreated";
            else if (hasLegacyDateCreated)
                dateColumn = "CASE WHEN [LegacyDateCreated] IS NOT NULL THEN CONVERT(VARCHAR(50), [LegacyDateCreated], 101) ELSE '' END AS DateCreated";
            else if (hasDatePosted)
                dateColumn = "CASE WHEN [Date_x0020_Posted] IS NOT NULL THEN CONVERT(VARCHAR(50), [Date_x0020_Posted], 101) ELSE '' END AS DateCreated";
            else
                dateColumn = "'' AS DateCreated";

            var fileRefColumn = hasFileLeafRef
                ? "[FileLeafRef] AS FileLeafRef"
                : "'' AS FileLeafRef";

            var normalizedForDb = filterName.Replace(" ", "_");
            var lmcsColumn = availableColumns.Contains("LMCSNames") ? "[LMCSNames]" : "[LMCS_x0020_Names]";

            string whereClause;
            if (hasLmcsNames)
            {
                whereClause = $@"({lmcsColumn} = @filterName OR {lmcsColumn} = @normalizedFilter OR 
                    REPLACE({lmcsColumn}, '_', ' ') = @filterName" +
                    (hasFilterName ? @" OR [Filter_x0020_Name] = @filterName OR [Filter_x0020_Name] = @normalizedFilter OR 
                        REPLACE([Filter_x0020_Name], '_', ' ') = @filterName" : "") + ")";
            }
            else if (hasFilterName)
            {
                whereClause = @"([Filter_x0020_Name] = @filterName OR [Filter_x0020_Name] = @normalizedFilter OR 
                    REPLACE([Filter_x0020_Name], '_', ' ') = @filterName)";
            }
            else
            {
                whereClause = @"([Site] = @filterName OR [Site] = @normalizedFilter OR 
                    REPLACE([Site], '_', ' ') = @filterName)";
            }

            var nullChecks = titleParts.Select(tp => $"{tp} IS NOT NULL");
            whereClause += $" AND ({string.Join(" OR ", nullChecks)})";

            var orderByClause = hasDateCreated ? "ORDER BY [Date_x0020_Created] DESC"
                : hasLegacyDateCreated ? "ORDER BY [LegacyDateCreated] DESC"
                : hasDatePosted ? "ORDER BY [Date_x0020_Posted] DESC"
                : hasDocDesc ? "ORDER BY [DocDesc]"
                : hasTitle ? "ORDER BY [Title]"
                : "ORDER BY [FileLeafRef]";

            await using var command = connection.CreateCommand();
            command.CommandText = $@"
                SELECT {titleColumn}, {categoryColumn}, {siteTypeColumn}, {lmcsNamesColumn}, 
                       {stateColumn}, {dateColumn}, {fileRefColumn}
                FROM [PubSearch].[dbo].[LMandConsideredSites]
                WHERE {whereClause}
                {orderByClause}";
            command.CommandType = CommandType.Text;
            command.CommandTimeout = DefaultCommandTimeout;
            command.Parameters.AddWithValue("@filterName", filterName);
            command.Parameters.AddWithValue("@normalizedFilter", normalizedForDb);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var title = SafeGetString(reader, "Title").Trim();
                if (string.IsNullOrWhiteSpace(title)) continue;

                documents.Add(new DocumentItem
                {
                    Name = "📄",
                    Title = title,
                    Category = SafeGetString(reader, "DocumentCategory").Trim(),
                    SiteType = SafeGetString(reader, "SiteType").Trim(),
                    LMCSNames = SafeGetString(reader, "LMCSNames").Trim(),
                    State = SafeGetString(reader, "State").Trim(),
                    DateCreated = SafeGetString(reader, "DateCreated").Trim(),
                    FilePath = hasFileLeafRef ? SafeGetString(reader, "FileLeafRef").Trim() : string.Empty
                });
            }

            _logger.LogInformation("Loaded {Count} documents for filter {FilterName}", documents.Count, filterName);
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Failed to load documents for filter {FilterName}", filterName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading documents for filter {FilterName}", filterName);
        }

        return documents;
    }

    public async Task<IReadOnlyList<CerclaArIndexItem>> GetCerclaArIndexAsync(string site, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(site))
        {
            return Array.Empty<CerclaArIndexItem>();
        }

        var items = new List<CerclaArIndexItem>();

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(CancellationToken.None);

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    COALESCE([FileLeafRef], '') AS [File],
                    COALESCE([ContentTitle], [DocDesc], [Title], '') AS Title,
                    COALESCE([Sites], '') AS Site,
                    COALESCE([State], '') AS State
                FROM [PubSearch].[dbo].[CERCLA]
                WHERE [Sites] = @site AND [Key_x0020_Document] = 1
                ORDER BY Title";
            command.CommandType = CommandType.Text;
            command.CommandTimeout = LargeTableCommandTimeout;
            command.Parameters.AddWithValue("@site", site);

            await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);
            while (await reader.ReadAsync(CancellationToken.None))
            {
                var title = SafeGetString(reader, "Title").Trim();
                if (string.IsNullOrWhiteSpace(title)) continue;

                items.Add(new CerclaArIndexItem
                {
                    File = SafeGetString(reader, "File").Trim(),
                    Title = title,
                    Site = SafeGetString(reader, "Site").Trim(),
                    State = SafeGetString(reader, "State").Trim()
                });
            }

            _logger.LogInformation("Loaded {Count} AR Index items for site {Site}", items.Count, site);
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Failed to load AR Index for site {Site}", site);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading AR Index for site {Site}", site);
        }

        return items;
    }

    public async Task<IReadOnlyList<CerclaSiteDocumentItem>> GetCerclaSiteDocumentsAsync(string site, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(site))
        {
            return Array.Empty<CerclaSiteDocumentItem>();
        }

        var items = new List<CerclaSiteDocumentItem>();

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(CancellationToken.None);

            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    COALESCE([FileLeafRef], '') AS [File],
                    COALESCE([AR_NUMBER], '') AS ArPdNumber,
                    COALESCE([ContentTitle], [DocDesc], [Title], '') AS Title,
                    CASE WHEN [Document_Date] IS NOT NULL 
                         THEN CONVERT(VARCHAR(50), [Document_Date], 101) 
                         ELSE '' END AS DocumentDate
                FROM [PubSearch].[dbo].[CERCLA]
                WHERE [Sites] = @site
                ORDER BY [Document_Date] DESC";
            command.CommandType = CommandType.Text;
            command.CommandTimeout = LargeTableCommandTimeout;
            command.Parameters.AddWithValue("@site", site);

            await using var reader = await command.ExecuteReaderAsync(CancellationToken.None);
            while (await reader.ReadAsync(CancellationToken.None))
            {
                var title = SafeGetString(reader, "Title").Trim();
                if (string.IsNullOrWhiteSpace(title)) continue;

                items.Add(new CerclaSiteDocumentItem
                {
                    File = SafeGetString(reader, "File").Trim(),
                    ArPdNumber = SafeGetString(reader, "ArPdNumber").Trim(),
                    Title = title,
                    DocumentDate = SafeGetString(reader, "DocumentDate").Trim()
                });
            }

            _logger.LogInformation("Loaded {Count} site documents for site {Site}", items.Count, site);
        }
        catch (SqlException ex)
        {
            _logger.LogError(ex, "Failed to load site documents for site {Site}", site);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading site documents for site {Site}", site);
        }

        return items;
    }

    #endregion

    #region Private Helper Methods

    private static string SafeGetString(DbDataReader reader, string column)
    {
        try
        {
            var ordinal = reader.GetOrdinal(column);
            if (reader.IsDBNull(ordinal)) return string.Empty;

            var value = reader.GetValue(ordinal);
            if (value is DateTime dateValue)
            {
                return dateValue.ToString("M/d/yyyy");
            }

            var stringValue = value.ToString() ?? string.Empty;
            return StripHtmlTags(stringValue);
        }
        catch (IndexOutOfRangeException)
        {
            return string.Empty;
        }
    }

    private static string StripHtmlTags(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;

        var stripped = Regex.Replace(html, "<.*?>", string.Empty);
        stripped = WebUtility.HtmlDecode(stripped);
        stripped = Regex.Replace(stripped, @"\s+", " ").Trim();

        return stripped;
    }

    private async Task<bool> ColumnExistsAsync(SqlConnection connection, string tableName, string columnName, CancellationToken cancellationToken)
    {
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COUNT(*) 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = @tableName AND COLUMN_NAME = @columnName";
            command.Parameters.AddWithValue("@tableName", tableName);
            command.Parameters.AddWithValue("@columnName", columnName);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result != null && Convert.ToInt32(result) > 0;
        }
        catch
        {
            return false;
        }
    }

    private async Task<HashSet<string>> GetTableColumnsAsync(SqlConnection connection, string tableName, CancellationToken cancellationToken)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT COLUMN_NAME 
                FROM INFORMATION_SCHEMA.COLUMNS 
                WHERE TABLE_NAME = @tableName 
                ORDER BY ORDINAL_POSITION";
            command.Parameters.AddWithValue("@tableName", tableName);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                columns.Add(reader.GetString(0));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get columns for table {TableName}", tableName);
        }

        return columns;
    }

    #endregion
}
