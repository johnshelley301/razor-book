using System.Text.Json.Serialization;

namespace PubSearchSite.Models;

/// <summary>
/// Represents a document item from the LM and Considered Sites tables.
/// </summary>
public sealed class DocumentItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("siteType")]
    public string SiteType { get; set; } = string.Empty;

    [JsonPropertyName("lmcsNames")]
    public string LMCSNames { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("dateCreated")]
    public string DateCreated { get; set; } = string.Empty;

    [JsonPropertyName("datePosted")]
    public string DatePosted { get; set; } = string.Empty;

    [JsonPropertyName("filePath")]
    public string FilePath { get; set; } = string.Empty;

    [JsonPropertyName("arPdNumber")]
    public string ArPdNumber { get; set; } = string.Empty;

    [JsonPropertyName("documentDate")]
    public string DocumentDate { get; set; } = string.Empty;

    [JsonPropertyName("site")]
    public string Site { get; set; } = string.Empty;
}

/// <summary>
/// Represents a document from the CERCLA Administrative Record Index.
/// </summary>
public sealed class CerclaArIndexItem
{
    [JsonPropertyName("file")]
    public string File { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("site")]
    public string Site { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;
}

/// <summary>
/// Represents a site document from the CERCLA table.
/// </summary>
public sealed class CerclaSiteDocumentItem
{
    [JsonPropertyName("file")]
    public string File { get; set; } = string.Empty;

    [JsonPropertyName("arPdNumber")]
    public string ArPdNumber { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("documentDate")]
    public string DocumentDate { get; set; } = string.Empty;
}

/// <summary>
/// Contains the result of a document query, including key documents and all site documents.
/// </summary>
public sealed class DocumentResult
{
    public DocumentResult(IReadOnlyList<DocumentItem> keyDocuments, IReadOnlyList<DocumentItem> siteDocuments)
    {
        KeyDocuments = keyDocuments;
        SiteDocuments = siteDocuments;
    }

    [JsonPropertyName("keyDocuments")]
    public IReadOnlyList<DocumentItem> KeyDocuments { get; }

    [JsonPropertyName("siteDocuments")]
    public IReadOnlyList<DocumentItem> SiteDocuments { get; }
}

/// <summary>
/// Contains detailed information about a Considered Site.
/// </summary>
public sealed class SiteDetails
{
    [JsonPropertyName("designatedName")]
    public string DesignatedName { get; set; } = string.Empty;

    [JsonPropertyName("alternateName")]
    public string AlternateName { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    [JsonPropertyName("evaluationYear")]
    public string EvaluationYear { get; set; } = string.Empty;

    [JsonPropertyName("siteOperations")]
    public string SiteOperations { get; set; } = string.Empty;

    [JsonPropertyName("siteDisposition")]
    public string SiteDisposition { get; set; } = string.Empty;

    [JsonPropertyName("radioactiveMaterialsHandled")]
    public string RadioactiveMaterialsHandled { get; set; } = string.Empty;

    [JsonPropertyName("primaryRadioactiveMaterialsHandled")]
    public string PrimaryRadioactiveMaterialsHandled { get; set; } = string.Empty;

    [JsonPropertyName("radiologicalSurveys")]
    public string RadiologicalSurveys { get; set; } = string.Empty;

    [JsonPropertyName("siteStatus")]
    public string SiteStatus { get; set; } = string.Empty;

    [JsonPropertyName("siteSummary")]
    public string SiteSummary { get; set; } = string.Empty;

    [JsonPropertyName("lmSite")]
    public string LmSite { get; set; } = string.Empty;
}
