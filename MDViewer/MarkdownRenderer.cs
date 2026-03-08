using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Markdig;

namespace it.carpanese.utilities.MDViewer
{
    /// <summary>
    /// Provider disponibili per il rendering Markdown.
    /// </summary>
    public enum MarkdownProvider
    {
        /// <summary>
        /// Markdig - Rendering locale, veloce e privato.
        /// </summary>
        Markdig,

        /// <summary>
        /// GitHub API - Rendering online con supporto completo GitHub Flavored Markdown.
        /// </summary>
        GitHubApi
    }

    /// <summary>
    /// Informazioni descrittive sui provider di rendering.
    /// </summary>
    public static class MarkdownProviderInfo
    {
        public static string GetDisplayName(MarkdownProvider provider)
        {
            switch (provider)
            {
                case MarkdownProvider.Markdig:
                    return Localizer.Get("ProviderMarkdigDisplayName");
                case MarkdownProvider.GitHubApi:
                    return Localizer.Get("ProviderGitHubDisplayName");
                default:
                    return provider.ToString();
            }
        }

        public static string GetDescription(MarkdownProvider provider)
        {
            switch (provider)
            {
                case MarkdownProvider.Markdig:
                    return Localizer.Get("ProviderMarkdigDescription");

                case MarkdownProvider.GitHubApi:
                    return Localizer.Get("ProviderGitHubDescription");

                default:
                    return string.Empty;
            }
        }

        public static string GetShortDescription(MarkdownProvider provider)
        {
            switch (provider)
            {
                case MarkdownProvider.Markdig:
                    return Localizer.Get("ProviderMarkdigShortDescription");
                case MarkdownProvider.GitHubApi:
                    return Localizer.Get("ProviderGitHubShortDescription");
                default:
                    return string.Empty;
            }
        }
    }

    /// <summary>
    /// Servizio di rendering Markdown con supporto per più provider.
    /// </summary>
    public class MarkdownRenderer
    {
        private static readonly HashSet<string> _blockedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "script", "iframe", "object", "embed", "form", "meta", "base", "link"
        };

        private static readonly HashSet<string> _urlAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "href", "src", "xlink:href", "formaction"
        };

        private static readonly string[] _safeDataImagePrefixes =
        {
            "data:image/png;base64,",
            "data:image/jpeg;base64,",
            "data:image/jpg;base64,",
            "data:image/gif;base64,",
            "data:image/webp;base64,"
        };

        private readonly MarkdownPipeline _markdigPipeline;
        private readonly MarkdownCache _cache;
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        public MarkdownProvider CurrentProvider { get; set; } = MarkdownProvider.Markdig;

        public MarkdownRenderer(MarkdownCache cache = null)
        {
            _cache = cache;

            // Configura pipeline Markdig con tutte le estensioni GFM
            _markdigPipeline = new MarkdownPipelineBuilder()
                .UseAdvancedExtensions()          // Tabelle, footnotes, abbreviations, etc.
                .UseAutoLinks()                   // Auto-link URLs e email
                .UseTaskLists()                   // - [ ] Task lists
                .UseEmojiAndSmiley()              // :emoji: → Unicode
                .UseSoftlineBreakAsHardlineBreak() // Line breaks come <br>
                .UseAutoIdentifiers()             // ID automatici per headers
                .Build();

            // Configura HttpClient per GitHub API
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MDViewer/2.0");
            }
        }

        /// <summary>
        /// Renderizza Markdown in HTML usando il provider corrente.
        /// </summary>
        public async Task<RenderResult> RenderAsync(string markdown)
        {
            if (string.IsNullOrEmpty(markdown))
                return new RenderResult { Html = string.Empty, FromCache = false };

            switch (CurrentProvider)
            {
                case MarkdownProvider.Markdig:
                    return RenderWithMarkdig(markdown);

                case MarkdownProvider.GitHubApi:
                    return await RenderWithGitHubApiAsync(markdown);

                default:
                    return RenderWithMarkdig(markdown);
            }
        }

        /// <summary>
        /// Rendering locale con Markdig (nessuna cache necessaria - sempre istantaneo).
        /// </summary>
        private RenderResult RenderWithMarkdig(string markdown)
        {
            try
            {
                var html = Markdown.ToHtml(markdown, _markdigPipeline);
                html = SanitizeHtml(html);
                System.Diagnostics.Debug.WriteLine("Markdig: rendering completato");
                return new RenderResult
                {
                    Html = html,
                    FromCache = false,
                    Provider = MarkdownProvider.Markdig
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Markdig error: {ex.Message}");
                // Fallback: testo escapato
                return new RenderResult
                {
                    Html = $"<pre>{System.Net.WebUtility.HtmlEncode(markdown)}</pre>",
                    FromCache = false,
                    Error = ex.Message,
                    Provider = MarkdownProvider.Markdig
                };
            }
        }

        /// <summary>
        /// Rendering online con GitHub API (usa cache per evitare chiamate ripetute).
        /// </summary>
        private async Task<RenderResult> RenderWithGitHubApiAsync(string markdown)
        {
            // 1. Prima controlla la cache
            if (_cache != null && _cache.TryGet(markdown, out string cachedHtml))
            {
                System.Diagnostics.Debug.WriteLine("GitHub API: cache HIT");
                return new RenderResult
                {
                    Html = cachedHtml,
                    FromCache = true,
                    Provider = MarkdownProvider.GitHubApi
                };
            }

            // 2. Chiama GitHub API
            try
            {
                System.Diagnostics.Debug.WriteLine("GitHub API: chiamata in corso...");

                var payload = "{\"text\":" + JsonEscape(markdown) + ",\"mode\":\"gfm\"}";
                var content = new StringContent(payload, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://api.github.com/markdown", content);
                response.EnsureSuccessStatusCode();

                var html = await response.Content.ReadAsStringAsync();
                html = SanitizeHtml(html);

                // 3. Salva in cache
                if (_cache != null)
                {
                    _cache.Set(markdown, html);
                    System.Diagnostics.Debug.WriteLine("GitHub API: salvato in cache");
                }

                return new RenderResult
                {
                    Html = html,
                    FromCache = false,
                    Provider = MarkdownProvider.GitHubApi
                };
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"GitHub API error: {ex.Message}");
                return new RenderResult
                {
                    Html = null,
                    FromCache = false,
                    Error = ex.Message,
                    IsNetworkError = true,
                    Provider = MarkdownProvider.GitHubApi
                };
            }
            catch (TaskCanceledException ex)
            {
                System.Diagnostics.Debug.WriteLine($"GitHub API timeout: {ex.Message}");
                return new RenderResult
                {
                    Html = null,
                    FromCache = false,
                    Error = Localizer.Get("RendererTimeoutError"),
                    IsNetworkError = true,
                    Provider = MarkdownProvider.GitHubApi
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GitHub API unexpected error: {ex.Message}");
                return new RenderResult
                {
                    Html = null,
                    FromCache = false,
                    Error = ex.Message,
                    Provider = MarkdownProvider.GitHubApi
                };
            }
        }

        /// <summary>
        /// Escape di stringa per JSON.
        /// </summary>
        private static string JsonEscape(string s)
        {
            var sb = new StringBuilder();
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\b': sb.Append("\\b"); break;
                    case '\f': sb.Append("\\f"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (char.IsControl(c))
                        {
                            sb.Append("\\u");
                            sb.Append(((int)c).ToString("x4"));
                        }
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
            return sb.ToString();
        }

        /// <summary>
        /// Sanitizzazione HTML difensiva basata su parser DOM.
        /// </summary>
        private static string SanitizeHtml(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return string.Empty;

            var doc = new HtmlDocument
            {
                OptionFixNestedTags = true,
                OptionAutoCloseOnEnd = true
            };
            doc.LoadHtml(html);

            var nodes = doc.DocumentNode.SelectNodes("//*");
            if (nodes == null)
                return doc.DocumentNode.InnerHtml;

            foreach (var node in nodes.ToList())
            {
                if (_blockedTags.Contains(node.Name))
                {
                    node.Remove();
                    continue;
                }

                if (!node.HasAttributes)
                    continue;

                for (int i = node.Attributes.Count - 1; i >= 0; i--)
                {
                    var attribute = node.Attributes[i];
                    var attributeName = attribute.Name;

                    if (attributeName.StartsWith("on", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(attributeName, "srcdoc", StringComparison.OrdinalIgnoreCase))
                    {
                        node.Attributes.Remove(attribute);
                        continue;
                    }

                    if (_urlAttributes.Contains(attributeName))
                    {
                        var value = HtmlEntity.DeEntitize(attribute.Value ?? string.Empty).Trim();
                        if (!IsSafeUrl(value))
                        {
                            node.Attributes.Remove(attribute);
                        }
                        else
                        {
                            node.SetAttributeValue(attributeName, value);
                        }
                        continue;
                    }

                    if (string.Equals(attributeName, "style", StringComparison.OrdinalIgnoreCase))
                    {
                        var normalized = (attribute.Value ?? string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
                        if (normalized.Contains("expression(") ||
                            normalized.Contains("javascript:") ||
                            normalized.Contains("vbscript:") ||
                            normalized.Contains("data:text/html"))
                        {
                            node.Attributes.Remove(attribute);
                        }
                    }
                }
            }

            return doc.DocumentNode.InnerHtml;
        }

        private static bool IsSafeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return true;

            if (url.StartsWith("#", StringComparison.Ordinal) ||
                url.StartsWith("/", StringComparison.Ordinal) ||
                url.StartsWith("./", StringComparison.Ordinal) ||
                url.StartsWith("../", StringComparison.Ordinal))
            {
                return true;
            }

            // Percorsi Windows assoluti (es. C:\docs\readme.md)
            if (url.Length >= 3 &&
                char.IsLetter(url[0]) &&
                url[1] == ':' &&
                (url[2] == '\\' || url[2] == '/'))
            {
                return true;
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri))
            {
                var scheme = absoluteUri.Scheme.ToLowerInvariant();
                switch (scheme)
                {
                    case "http":
                    case "https":
                    case "mailto":
                    case "file":
                        return true;
                    case "data":
                        return IsSafeDataImageUrl(url);
                    default:
                        return false;
                }
            }

            // URL relativa senza schema esplicito.
            return !url.Contains(":");
        }

        private static bool IsSafeDataImageUrl(string url)
        {
            foreach (var prefix in _safeDataImagePrefixes)
            {
                if (url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Risultato del rendering Markdown.
    /// </summary>
    public class RenderResult
    {
        /// <summary>
        /// HTML renderizzato (null se errore).
        /// </summary>
        public string Html { get; set; }

        /// <summary>
        /// True se il risultato proviene dalla cache.
        /// </summary>
        public bool FromCache { get; set; }

        /// <summary>
        /// Messaggio di errore (null se successo).
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// True se l'errore è di rete (utile per decidere fallback).
        /// </summary>
        public bool IsNetworkError { get; set; }

        /// <summary>
        /// Provider usato per il rendering.
        /// </summary>
        public MarkdownProvider Provider { get; set; }

        /// <summary>
        /// True se il rendering è avvenuto con successo.
        /// </summary>
        public bool Success => Html != null && Error == null;
    }
}
