using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace it.carpanese.utilities.MDViewer
{
    /// <summary>
    /// Sistema di caching locale per il rendering Markdown.
    /// Salva le risposte dell'API GitHub per evitare chiamate ripetute.
    /// </summary>
    public class MarkdownCache
    {
        private readonly string _cacheFolder;
        private TimeSpan _cacheExpiration;
        private long _maxCacheSizeBytes;

        /// <summary>
        /// Crea una nuova istanza del cache manager.
        /// </summary>
        /// <param name="expiration">Durata validità cache (default: 7 giorni)</param>
        /// <param name="maxSizeMB">Dimensione massima cache in MB (default: 50MB)</param>
        public MarkdownCache(TimeSpan? expiration = null, int maxSizeMB = 50)
        {
            _cacheFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MDViewer",
                "Cache");

            _cacheExpiration = expiration ?? TimeSpan.FromDays(7);
            _maxCacheSizeBytes = maxSizeMB * 1024 * 1024;

            try
            {
                Directory.CreateDirectory(_cacheFolder);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Impossibile creare cartella cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Aggiorna i parametri della cache a runtime.
        /// </summary>
        public void UpdateSettings(TimeSpan? expiration = null, int? maxSizeMB = null)
        {
            if (expiration.HasValue && expiration.Value > TimeSpan.Zero)
            {
                _cacheExpiration = expiration.Value;
            }

            if (maxSizeMB.HasValue && maxSizeMB.Value > 0)
            {
                _maxCacheSizeBytes = (long)maxSizeMB.Value * 1024 * 1024;
            }
        }

        /// <summary>
        /// Genera un hash univoco del contenuto Markdown.
        /// Qualsiasi modifica al contenuto genera un hash completamente diverso.
        /// </summary>
        private string GetHash(string markdown)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(markdown);
                var hash = sha256.ComputeHash(bytes);
                // Usa solo i primi 16 caratteri dell'hash (sufficienti per evitare collisioni)
                return BitConverter.ToString(hash).Replace("-", "").Substring(0, 16);
            }
        }

        /// <summary>
        /// Ottiene il percorso del file cache per un dato hash.
        /// </summary>
        private string GetCachePath(string hash)
        {
            return Path.Combine(_cacheFolder, $"{hash}.html");
        }

        /// <summary>
        /// Prova a recuperare HTML dalla cache.
        /// </summary>
        /// <param name="markdown">Contenuto Markdown originale</param>
        /// <param name="cachedHtml">HTML renderizzato dalla cache (se trovato)</param>
        /// <returns>True se trovato in cache e valido, False altrimenti</returns>
        public bool TryGet(string markdown, out string cachedHtml)
        {
            cachedHtml = null;

            if (string.IsNullOrEmpty(markdown))
                return false;

            try
            {
                var hash = GetHash(markdown);
                var cachePath = GetCachePath(hash);

                if (!File.Exists(cachePath))
                {
                    System.Diagnostics.Debug.WriteLine($"Cache MISS: {hash}");
                    return false;
                }

                // Verifica se la cache è scaduta
                var fileAge = DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath);
                if (fileAge > _cacheExpiration)
                {
                    System.Diagnostics.Debug.WriteLine($"Cache EXPIRED: {hash} (età: {fileAge.TotalDays:F1} giorni)");
                    try { File.Delete(cachePath); } catch { }
                    return false;
                }

                cachedHtml = File.ReadAllText(cachePath, Encoding.UTF8);
                System.Diagnostics.Debug.WriteLine($"Cache HIT: {hash}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore lettura cache: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Salva HTML renderizzato nella cache.
        /// </summary>
        /// <param name="markdown">Contenuto Markdown originale</param>
        /// <param name="html">HTML renderizzato da salvare</param>
        public void Set(string markdown, string html)
        {
            if (string.IsNullOrEmpty(markdown) || string.IsNullOrEmpty(html))
                return;

            try
            {
                // Verifica dimensione cache e pulisci se necessario
                CleanupIfNeeded();

                var hash = GetHash(markdown);
                var cachePath = GetCachePath(hash);

                File.WriteAllText(cachePath, html, Encoding.UTF8);
                System.Diagnostics.Debug.WriteLine($"Cache SAVED: {hash}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore salvataggio cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Pulisce la cache se supera la dimensione massima.
        /// Rimuove prima i file più vecchi.
        /// </summary>
        private void CleanupIfNeeded()
        {
            try
            {
                var currentSize = GetCacheSize();
                if (currentSize < _maxCacheSizeBytes)
                    return;

                System.Diagnostics.Debug.WriteLine($"Cache cleanup: {currentSize / 1024 / 1024}MB > {_maxCacheSizeBytes / 1024 / 1024}MB");

                // Ordina per data modifica (più vecchi prima)
                var files = new DirectoryInfo(_cacheFolder)
                    .GetFiles("*.html")
                    .OrderBy(f => f.LastWriteTimeUtc)
                    .ToList();

                // Rimuovi file fino a tornare sotto il 70% del limite
                var targetSize = (long)(_maxCacheSizeBytes * 0.7);
                foreach (var file in files)
                {
                    if (currentSize <= targetSize)
                        break;

                    currentSize -= file.Length;
                    file.Delete();
                    System.Diagnostics.Debug.WriteLine($"Cache deleted: {file.Name}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore cleanup cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Pulisce la cache.
        /// </summary>
        /// <param name="onlyExpired">Se true, rimuove solo i file scaduti</param>
        public void Clear(bool onlyExpired = false)
        {
            try
            {
                foreach (var file in Directory.GetFiles(_cacheFolder, "*.html"))
                {
                    if (onlyExpired)
                    {
                        var fileAge = DateTime.UtcNow - File.GetLastWriteTimeUtc(file);
                        if (fileAge > _cacheExpiration)
                        {
                            File.Delete(file);
                        }
                    }
                    else
                    {
                        File.Delete(file);
                    }
                }

                System.Diagnostics.Debug.WriteLine($"Cache cleared (onlyExpired: {onlyExpired})");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore pulizia cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Restituisce la dimensione totale della cache in bytes.
        /// </summary>
        public long GetCacheSize()
        {
            try
            {
                long size = 0;
                foreach (var file in Directory.GetFiles(_cacheFolder, "*.html"))
                {
                    size += new FileInfo(file).Length;
                }
                return size;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Restituisce il numero di file nella cache.
        /// </summary>
        public int GetCacheCount()
        {
            try
            {
                return Directory.GetFiles(_cacheFolder, "*.html").Length;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Restituisce informazioni sulla cache.
        /// </summary>
        public string GetCacheInfo()
        {
            var size = GetCacheSize();
            var count = GetCacheCount();
            var sizeMB = size / 1024.0 / 1024.0;
            return $"{count} file, {sizeMB:F2} MB";
        }
    }
}
