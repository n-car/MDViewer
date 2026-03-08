using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace it.carpanese.utilities.MDViewer
{
    /// <summary>
    /// Gestisce il controllo degli aggiornamenti tramite GitHub Releases API.
    /// </summary>
    public class UpdateManager
    {
        private static UpdateManager _instance;
        public static UpdateManager Instance => _instance ?? (_instance = new UpdateManager());

        // Configurazione repository GitHub
        private const string GitHubOwner = "n-car";
        private const string GitHubRepo = "MDViewer";
        private const string GitHubApiUrl = "https://api.github.com/repos/{0}/{1}/releases/latest";
        
        private static readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

        private UpdateManager()
        {
            // Configura HttpClient con User-Agent richiesto da GitHub
            if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MDViewer/2.0");
                _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
            }
        }

        /// <summary>
        /// Versione corrente dell'applicazione.
        /// </summary>
        public Version CurrentVersion
        {
            get
            {
                var assembly = Assembly.GetExecutingAssembly();
                return assembly.GetName().Version;
            }
        }

        /// <summary>
        /// Stringa versione corrente formattata.
        /// </summary>
        public string CurrentVersionString => $"v{CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build}";

        /// <summary>
        /// Controlla se ci sono aggiornamenti disponibili.
        /// </summary>
        /// <returns>Informazioni sull'aggiornamento o null se non disponibile</returns>
        public async Task<UpdateInfo> CheckForUpdatesAsync()
        {
            try
            {
                var url = string.Format(GitHubApiUrl, GitHubOwner, GitHubRepo);
                System.Diagnostics.Debug.WriteLine($"Controllo aggiornamenti: {url}");

                var response = await _httpClient.GetStringAsync(url);
                
                // Parse JSON con DataContractJsonSerializer (evita dipendenze esterne)
                var updateInfo = ParseGitHubResponse(response);
                
                if (updateInfo == null)
                {
                    System.Diagnostics.Debug.WriteLine("Nessuna release trovata");
                    return null;
                }

                // Confronta versioni
                updateInfo.CurrentVersion = CurrentVersion;
                updateInfo.IsUpdateAvailable = updateInfo.LatestVersion > CurrentVersion;

                System.Diagnostics.Debug.WriteLine($"Versione corrente: {CurrentVersion}");
                System.Diagnostics.Debug.WriteLine($"Versione disponibile: {updateInfo.LatestVersion}");
                System.Diagnostics.Debug.WriteLine($"Aggiornamento disponibile: {updateInfo.IsUpdateAvailable}");

                return updateInfo;
            }
            catch (HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore rete: {ex.Message}");
                throw new UpdateCheckException(Localizer.Get("UpdateErrorCannotConnectGitHub"), ex);
            }
            catch (TaskCanceledException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Timeout: {ex.Message}");
                throw new UpdateCheckException(Localizer.Get("UpdateErrorTimeout"), ex);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore generico: {ex.Message}");
                throw new UpdateCheckException(Localizer.Format("UpdateErrorGenericFormat", ex.Message), ex);
            }
        }

        /// <summary>
        /// Parse della risposta JSON di GitHub (senza dipendenze esterne).
        /// </summary>
        private UpdateInfo ParseGitHubResponse(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                    return null;

                var serializer = new DataContractJsonSerializer(typeof(GitHubReleaseResponse));
                GitHubReleaseResponse release;

                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    release = serializer.ReadObject(ms) as GitHubReleaseResponse;
                }

                if (release == null || string.IsNullOrWhiteSpace(release.TagName))
                    return null;

                var latestVersion = ParseVersion(release.TagName);
                if (latestVersion == null)
                    return null;

                var info = new UpdateInfo
                {
                    TagName = release.TagName,
                    LatestVersion = latestVersion,
                    ReleaseName = release.Name,
                    ReleaseNotes = release.Body,
                    ReleaseUrl = release.HtmlUrl,
                    DownloadUrl = release.Assets != null
                        ? release.Assets.Find(a => !string.IsNullOrWhiteSpace(a?.BrowserDownloadUrl))?.BrowserDownloadUrl
                        : null
                };

                if (!string.IsNullOrWhiteSpace(release.PublishedAt) &&
                    DateTime.TryParse(
                        release.PublishedAt,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                        out var date))
                {
                    info.PublishedAt = date;
                }

                return info;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore parsing JSON: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Converte un tag di versione (es. "v1.2.3") in un oggetto Version.
        /// </summary>
        private Version ParseVersion(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return null;

            // Rimuovi prefisso "v" se presente
            var versionString = tag.TrimStart('v', 'V');

            // Prova a parsare
            if (Version.TryParse(versionString, out var version))
                return version;

            // Prova con regex per formati tipo "1.2.3-beta"
            var match = Regex.Match(versionString, @"^(\d+)\.(\d+)(?:\.(\d+))?");
            if (match.Success)
            {
                int major = int.Parse(match.Groups[1].Value);
                int minor = int.Parse(match.Groups[2].Value);
                int build = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : 0;
                return new Version(major, minor, build);
            }

            return null;
        }

        /// <summary>
        /// Apre la pagina di download nel browser predefinito.
        /// </summary>
        public void OpenDownloadPage(UpdateInfo info)
        {
            try
            {
                var url = info?.ReleaseUrl ?? $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore apertura browser: {ex.Message}");
            }
        }

        /// <summary>
        /// Apre la pagina GitHub del progetto.
        /// </summary>
        public void OpenProjectPage()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = $"https://github.com/{GitHubOwner}/{GitHubRepo}",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore apertura browser: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Informazioni su un aggiornamento disponibile.
    /// </summary>
    public class UpdateInfo
    {
        /// <summary>
        /// Tag della release (es. "v1.2.0").
        /// </summary>
        public string TagName { get; set; }

        /// <summary>
        /// Nome della release.
        /// </summary>
        public string ReleaseName { get; set; }

        /// <summary>
        /// Note di rilascio (changelog).
        /// </summary>
        public string ReleaseNotes { get; set; }

        /// <summary>
        /// URL della pagina release su GitHub.
        /// </summary>
        public string ReleaseUrl { get; set; }

        /// <summary>
        /// URL per il download diretto dell'installer.
        /// </summary>
        public string DownloadUrl { get; set; }

        /// <summary>
        /// Data di pubblicazione.
        /// </summary>
        public DateTime PublishedAt { get; set; }

        /// <summary>
        /// Versione più recente disponibile.
        /// </summary>
        public Version LatestVersion { get; set; }

        /// <summary>
        /// Versione corrente dell'applicazione.
        /// </summary>
        public Version CurrentVersion { get; set; }

        /// <summary>
        /// True se è disponibile un aggiornamento.
        /// </summary>
        public bool IsUpdateAvailable { get; set; }

        /// <summary>
        /// Stringa versione formattata.
        /// </summary>
        public string LatestVersionString => TagName ?? $"v{LatestVersion}";
    }

    /// <summary>
    /// Eccezione per errori durante il controllo aggiornamenti.
    /// </summary>
    public class UpdateCheckException : Exception
    {
        public UpdateCheckException(string message) : base(message) { }
        public UpdateCheckException(string message, Exception inner) : base(message, inner) { }
    }

    [DataContract]
    internal class GitHubReleaseResponse
    {
        [DataMember(Name = "tag_name")]
        public string TagName { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "body")]
        public string Body { get; set; }

        [DataMember(Name = "html_url")]
        public string HtmlUrl { get; set; }

        [DataMember(Name = "published_at")]
        public string PublishedAt { get; set; }

        [DataMember(Name = "assets")]
        public List<GitHubReleaseAsset> Assets { get; set; }
    }

    [DataContract]
    internal class GitHubReleaseAsset
    {
        [DataMember(Name = "browser_download_url")]
        public string BrowserDownloadUrl { get; set; }
    }
}
