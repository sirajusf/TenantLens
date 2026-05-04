using LeaseLense.Application.Abstractions;
using LeaseLense.Application.Search;

namespace LeaseLense.Web.Services;

public sealed class SmartSearchOrchestrator
{
    private readonly INlQueryParserLlmClient _nlParser;
    private readonly ICoreSearchService _coreSearch;
    private readonly ILogger<SmartSearchOrchestrator> _logger;

    public SmartSearchOrchestrator(
        INlQueryParserLlmClient nlParser,
        ICoreSearchService coreSearch,
        ILogger<SmartSearchOrchestrator> logger)
    {
        _nlParser = nlParser;
        _coreSearch = coreSearch;
        _logger = logger;
    }

    public async Task<SmartSearchResult> SearchAsync(
        string rawQuery,
        SearchEntityType? forcedEntityType,
        int limit,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawQuery))
        {
            return new SmartSearchResult
            {
                RawQuery = rawQuery,
                EffectiveQuery = new SearchQueryDto
                {
                    EntityType = forcedEntityType ?? SearchEntityType.Property,
                    QueryText = rawQuery,
                    Limit = Math.Max(1, limit)
                },
                TargetEntityTypes = forcedEntityType is null
                    ? [SearchEntityType.Property]
                    : [forcedEntityType.Value]
            };
        }

        var normalized = rawQuery.Trim();
        var isLongQuery = ShouldUseNlFirst(normalized);

        NlQueryParseResult? parsed = null;
        bool nlAttempted = false;
        bool nlFallback = false;
        bool llmUnavailable = false;

        if (isLongQuery)
        {
            nlAttempted = true;
            parsed = await TryParseNlAsync(normalized, cancellationToken);
            llmUnavailable = parsed is null;
        }

        var keywordQuery = new SearchQueryDto
        {
            EntityType = forcedEntityType ?? SearchEntityType.Property,
            QueryText = normalized,
            Limit = Math.Max(1, limit)
        };

        var parsedTargetTypes = GetTargetEntityTypes(parsed, forcedEntityType);
        var primaryEffective = GetEffectiveQuery(parsed, forcedEntityType, keywordQuery, limit);
        var primaryResults = await ExecuteAsync(primaryEffective, parsed, parsedTargetTypes, forcedEntityType, cancellationToken);

        if (primaryResults.TotalCount > 0)
        {
            return new SmartSearchResult
            {
                RawQuery = normalized,
                EffectiveQuery = primaryEffective,
                TargetEntityTypes = parsedTargetTypes,
                NlAttempted = nlAttempted,
                NlFallback = false,
                LlmUnavailable = llmUnavailable,
                InterpretedFilters = parsed?.InterpretedFilters ?? [],
                PropertyResults = primaryResults.PropertyResults,
                ReviewResults = primaryResults.ReviewResults,
                ScamReportResults = primaryResults.ScamReportResults
            };
        }

        // Long-query path: if AI parsing was attempted but produced zero matches,
        // fall back to keyword search so users do not hit an empty dead-end.
        if (nlAttempted)
        {
            var keywordTargetTypes = forcedEntityType is not null
                ? [forcedEntityType.Value]
                : (parsedTargetTypes.Count > 0 ? parsedTargetTypes : [SearchEntityType.Property]);

            var keywordFallbackResults = await ExecuteAsync(
                keywordQuery,
                parsed: null,
                keywordTargetTypes,
                forcedEntityType,
                cancellationToken);

            return new SmartSearchResult
            {
                RawQuery = normalized,
                EffectiveQuery = keywordQuery,
                TargetEntityTypes = keywordTargetTypes,
                NlAttempted = true,
                NlFallback = false,
                LlmUnavailable = llmUnavailable,
                InterpretedFilters = parsed?.InterpretedFilters ?? [],
                PropertyResults = keywordFallbackResults.PropertyResults,
                ReviewResults = keywordFallbackResults.ReviewResults,
                ScamReportResults = keywordFallbackResults.ScamReportResults
            };
        }

        // Short query with 0 results: try NL as a second chance.
        nlAttempted = true;
        parsed = await TryParseNlAsync(normalized, cancellationToken);
        if (parsed is null)
        {
            llmUnavailable = true;
            return new SmartSearchResult
            {
                RawQuery = normalized,
                EffectiveQuery = primaryEffective,
                TargetEntityTypes = [primaryEffective.EntityType],
                NlAttempted = true,
                NlFallback = false,
                LlmUnavailable = true,
                InterpretedFilters = [],
                PropertyResults = primaryResults.PropertyResults,
                ReviewResults = primaryResults.ReviewResults,
                ScamReportResults = primaryResults.ScamReportResults
            };
        }

        nlFallback = true;
        llmUnavailable = false;

        var fallbackTargetTypes = GetTargetEntityTypes(parsed, forcedEntityType);
        var fallbackEffective = GetEffectiveQuery(parsed, forcedEntityType, primaryEffective, limit);
        var fallbackResults = await ExecuteAsync(fallbackEffective, parsed, fallbackTargetTypes, forcedEntityType, cancellationToken);

        return new SmartSearchResult
        {
            RawQuery = normalized,
            EffectiveQuery = fallbackEffective,
            TargetEntityTypes = fallbackTargetTypes,
            NlAttempted = true,
            NlFallback = nlFallback,
            LlmUnavailable = llmUnavailable,
            InterpretedFilters = parsed.InterpretedFilters,
            PropertyResults = fallbackResults.PropertyResults,
            ReviewResults = fallbackResults.ReviewResults,
            ScamReportResults = fallbackResults.ScamReportResults
        };
    }

    /// <summary>
    /// Returns true when the query should be routed through the NL parser before keyword search.
    /// Any query with 4+ words always qualifies. Shorter queries qualify when they contain
    /// qualifier words that signal semantic intent beyond a bare keyword lookup.
    /// </summary>
    private static bool ShouldUseNlFirst(string query)
    {
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 4) return true;

        // Short query: check for qualifier signals
        var lower = query.ToLowerInvariant();
        return QualifierWords.Any(q => lower.Contains(q));
    }

    // Words that indicate the user has semantic intent beyond a simple keyword search.
    private static readonly string[] QualifierWords =
    [
        // price
        "cheap", "affordable", "budget", "expensive", "luxury", "moderate", "under", "below",
        "above", "over", "around", "max", "minimum",
        // quality
        "good", "bad", "poor", "great", "excellent", "best", "worst", "highly", "highly-rated",
        "top", "decent", "terrible", "awful", "amazing",
        // verification
        "verified", "confirmed",
        // scam/fraud
        "scam", "fraud", "fake", "suspicious", "dangerous", "sketchy", "shady",
        // time
        "recent", "latest", "newest", "last", "past", "since", "new",
        // issue types
        "mold", "pests", "noise", "noisy", "maintenance", "billing", "unsafe", "dirty",
        "roaches", "parking", "heating", "cooling", "plumbing", "leak",
        // intent words
        "with", "without", "near", "not", "avoid", "responsive", "unresponsive",
    ];

    private async Task<NlQueryParseResult?> TryParseNlAsync(string q, CancellationToken cancellationToken)
    {
        try
        {
            return await _nlParser.TryParseAsync(q, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NL query parser threw for query '{Query}'.", q);
            return null;
        }
    }

    private static SearchQueryDto? ApplyForcedEntityAndLimit(
        SearchQueryDto? query,
        SearchEntityType? forcedEntityType,
        int limit)
    {
        if (query is null)
            return null;

        var effectiveEntityType = forcedEntityType ?? query.EntityType;
        var effectiveLimit = Math.Max(1, limit);

        if (query.EntityType == effectiveEntityType && query.Limit == effectiveLimit)
            return query;

        return new SearchQueryDto
        {
            QueryText = query.QueryText,
            EntityType = effectiveEntityType,
            Limit = effectiveLimit,

            City = query.City,
            PropertyType = query.PropertyType,
            LandlordName = query.LandlordName,
            MinRent = query.MinRent,
            MaxRent = query.MaxRent,
            MinRating = query.MinRating,
            MinCommunicationRating = query.MinCommunicationRating,
            MinMaintenanceRating = query.MinMaintenanceRating,
            VerifiedOnly = query.VerifiedOnly,
            HasVerifiedReviews = query.HasVerifiedReviews,
            IssueTypes = query.IssueTypes,
            MinSeverity = query.MinSeverity,
            MaxSeverity = query.MaxSeverity,
            ScamType = query.ScamType,
            DateReportedAfter = query.DateReportedAfter,
            SortBy = query.SortBy
        };
    }

    private static IReadOnlyList<SearchEntityType> GetTargetEntityTypes(
        NlQueryParseResult? parsed,
        SearchEntityType? forcedEntityType)
    {
        if (forcedEntityType is not null)
            return [forcedEntityType.Value];

        if (parsed?.TargetEntityTypes is { Count: > 0 } targets)
            return targets;

        if (parsed?.Query is not null)
            return [parsed.Query.EntityType];

        return [SearchEntityType.Property];
    }

    private static SearchQueryDto GetEffectiveQuery(
        NlQueryParseResult? parsed,
        SearchEntityType? forcedEntityType,
        SearchQueryDto fallback,
        int limit)
    {
        if (forcedEntityType is not null)
        {
            var scopedCandidate = parsed is null
                ? null
                : GetQueryForEntity(parsed, forcedEntityType.Value) ?? parsed.Query;

            return ApplyForcedEntityAndLimit(scopedCandidate, forcedEntityType, limit) ?? fallback;
        }

        var primary = parsed?.Query;
        if (primary is null && parsed?.TargetEntityTypes is { Count: > 0 } targets)
        {
            var firstTarget = targets[0];
            primary = GetQueryForEntity(parsed, firstTarget);
        }

        return ApplyForcedEntityAndLimit(primary, null, limit) ?? fallback;
    }

    private static SearchQueryDto? GetQueryForEntity(NlQueryParseResult parsed, SearchEntityType entityType)
    {
        if (parsed.QueriesByEntity.TryGetValue(entityType, out var q))
            return q;
        if (parsed.Query?.EntityType == entityType)
            return parsed.Query;
        return null;
    }

    private async Task<SearchExecutionBundle> ExecuteAsync(
        SearchQueryDto effectiveQuery,
        NlQueryParseResult? parsed,
        IReadOnlyList<SearchEntityType> targetEntityTypes,
        SearchEntityType? forcedEntityType,
        CancellationToken cancellationToken)
    {
        if (forcedEntityType is not null || targetEntityTypes.Count <= 1)
        {
            try
            {
                var raw = await _coreSearch.SearchAsync(effectiveQuery, cancellationToken);
                return ToBundle(raw, effectiveQuery.EntityType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CoreSearchService threw for query '{Query}'.", effectiveQuery.QueryText);
                return new SearchExecutionBundle();
            }
        }

        var bundle = new SearchExecutionBundle();
        foreach (var entity in targetEntityTypes.Distinct())
        {
            SearchQueryDto queryForEntity =
                parsed is not null
                    ? ApplyForcedEntityAndLimit(GetQueryForEntity(parsed, entity), null, effectiveQuery.Limit) ?? effectiveQuery
                    : effectiveQuery;

            if (queryForEntity.EntityType != entity)
            {
                queryForEntity = new SearchQueryDto
                {
                    QueryText = effectiveQuery.QueryText,
                    EntityType = entity,
                    Limit = effectiveQuery.Limit,
                    City = effectiveQuery.City,
                    PropertyType = effectiveQuery.PropertyType,
                    LandlordName = effectiveQuery.LandlordName,
                    MinRent = effectiveQuery.MinRent,
                    MaxRent = effectiveQuery.MaxRent,
                    MinRating = effectiveQuery.MinRating,
                    MinCommunicationRating = effectiveQuery.MinCommunicationRating,
                    MinMaintenanceRating = effectiveQuery.MinMaintenanceRating,
                    VerifiedOnly = effectiveQuery.VerifiedOnly,
                    HasVerifiedReviews = effectiveQuery.HasVerifiedReviews,
                    IssueTypes = effectiveQuery.IssueTypes,
                    MinSeverity = effectiveQuery.MinSeverity,
                    MaxSeverity = effectiveQuery.MaxSeverity,
                    ScamType = effectiveQuery.ScamType,
                    DateReportedAfter = effectiveQuery.DateReportedAfter,
                    SortBy = effectiveQuery.SortBy
                };
            }

            try
            {
                var raw = await _coreSearch.SearchAsync(queryForEntity, cancellationToken);
                MergeBundle(bundle, ToBundle(raw, entity));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CoreSearchService threw for global mixed query '{Query}' entity {EntityType}.", queryForEntity.QueryText, entity);
            }
        }

        return bundle;
    }

    private static SearchExecutionBundle ToBundle(object raw, SearchEntityType entityType) => entityType switch
    {
        SearchEntityType.Review => new SearchExecutionBundle
        {
            ReviewResults = raw as IReadOnlyList<ReviewMatch> ?? []
        },
        SearchEntityType.ScamReport => new SearchExecutionBundle
        {
            ScamReportResults = raw as IReadOnlyList<ScamReportMatch> ?? []
        },
        _ => new SearchExecutionBundle
        {
            PropertyResults = raw as IReadOnlyList<PropertyMatch> ?? []
        }
    };

    private static void MergeBundle(SearchExecutionBundle target, SearchExecutionBundle source)
    {
        target.PropertyResults = source.PropertyResults.Count == 0
            ? target.PropertyResults
            : source.PropertyResults;
        target.ReviewResults = source.ReviewResults.Count == 0
            ? target.ReviewResults
            : source.ReviewResults;
        target.ScamReportResults = source.ScamReportResults.Count == 0
            ? target.ScamReportResults
            : source.ScamReportResults;
    }

    private sealed class SearchExecutionBundle
    {
        public IReadOnlyList<PropertyMatch> PropertyResults { get; set; } = [];
        public IReadOnlyList<ReviewMatch> ReviewResults { get; set; } = [];
        public IReadOnlyList<ScamReportMatch> ScamReportResults { get; set; } = [];
        public int TotalCount => PropertyResults.Count + ReviewResults.Count + ScamReportResults.Count;
    }
}

public sealed class SmartSearchResult
{
    public required string RawQuery { get; init; }
    public required SearchQueryDto EffectiveQuery { get; init; }
    public IReadOnlyList<SearchEntityType> TargetEntityTypes { get; init; } = [];

    public bool NlAttempted { get; init; }
    public bool NlFallback { get; init; }
    public bool LlmUnavailable { get; init; }

    public IReadOnlyList<string> InterpretedFilters { get; init; } = [];
    public IReadOnlyList<PropertyMatch> PropertyResults { get; init; } = [];
    public IReadOnlyList<ReviewMatch> ReviewResults { get; init; } = [];
    public IReadOnlyList<ScamReportMatch> ScamReportResults { get; init; } = [];
}

