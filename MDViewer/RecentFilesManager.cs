using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace it.carpanese.utilities.MDViewer
{
    /// <summary>
    /// Gestisce la lista dei file aperti di recente con persistenza su disco.
    /// </summary>
    public class RecentFilesManager
    {
        private static RecentFilesManager _instance;
        public static RecentFilesManager Instance => _instance ?? (_instance = new RecentFilesManager());

        private readonly string _recentFilesPath;
        private readonly List<string> _recentFiles;
        private int _maxRecentFiles;

        /// <summary>
        /// Evento fired quando la lista dei file recenti cambia.
        /// </summary>
        public event EventHandler RecentFilesChanged;

        /// <summary>
        /// Lista dei file recenti (sola lettura).
        /// </summary>
        public IReadOnlyList<string> RecentFiles => _recentFiles.AsReadOnly();

        /// <summary>
        /// Numero massimo di file recenti da memorizzare.
        /// </summary>
        public int MaxRecentFiles => _maxRecentFiles;

        private RecentFilesManager(int maxRecentFiles = 10)
        {
            _maxRecentFiles = maxRecentFiles;
            _recentFiles = new List<string>();

            // Percorso file di configurazione
            string appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MDViewer");

            Directory.CreateDirectory(appDataFolder);
            _recentFilesPath = Path.Combine(appDataFolder, "recent_files.txt");

            // Carica i file recenti all'avvio
            Load();
        }

        /// <summary>
        /// Aggiunge un file alla lista dei recenti.
        /// Se il file esiste già, viene spostato in cima.
        /// </summary>
        public void AddFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            // Normalizza il percorso
            string normalizedPath = Path.GetFullPath(filePath);

            // Rimuovi se già presente (verrà riaggiunto in cima)
            _recentFiles.RemoveAll(f => 
                string.Equals(f, normalizedPath, StringComparison.OrdinalIgnoreCase));

            // Aggiungi in cima
            _recentFiles.Insert(0, normalizedPath);

            // Limita la dimensione
            while (_recentFiles.Count > _maxRecentFiles)
            {
                _recentFiles.RemoveAt(_recentFiles.Count - 1);
            }

            // Salva e notifica
            Save();
            RecentFilesChanged?.Invoke(this, EventArgs.Empty);

            System.Diagnostics.Debug.WriteLine($"File aggiunto ai recenti: {normalizedPath}");
        }

        /// <summary>
        /// Aggiorna il numero massimo di file recenti mantenuti.
        /// </summary>
        public void SetMaxRecentFiles(int maxRecentFiles)
        {
            if (maxRecentFiles <= 0 || maxRecentFiles == _maxRecentFiles)
                return;

            _maxRecentFiles = maxRecentFiles;

            while (_recentFiles.Count > _maxRecentFiles)
            {
                _recentFiles.RemoveAt(_recentFiles.Count - 1);
            }

            Save();
            RecentFilesChanged?.Invoke(this, EventArgs.Empty);
            System.Diagnostics.Debug.WriteLine($"Max file recenti aggiornato: {_maxRecentFiles}");
        }

        /// <summary>
        /// Rimuove un file dalla lista dei recenti.
        /// </summary>
        public void RemoveFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return;

            string normalizedPath = Path.GetFullPath(filePath);
            
            if (_recentFiles.RemoveAll(f => 
                string.Equals(f, normalizedPath, StringComparison.OrdinalIgnoreCase)) > 0)
            {
                Save();
                RecentFilesChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Svuota la lista dei file recenti.
        /// </summary>
        public void Clear()
        {
            _recentFiles.Clear();
            Save();
            RecentFilesChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Rimuove dalla lista i file che non esistono più.
        /// </summary>
        public void CleanupInvalidFiles()
        {
            int removed = _recentFiles.RemoveAll(f => !File.Exists(f));
            
            if (removed > 0)
            {
                Save();
                RecentFilesChanged?.Invoke(this, EventArgs.Empty);
                System.Diagnostics.Debug.WriteLine($"Rimossi {removed} file non più esistenti");
            }
        }

        /// <summary>
        /// Restituisce i file recenti con informazioni aggiuntive.
        /// </summary>
        public IEnumerable<RecentFileInfo> GetRecentFilesInfo()
        {
            foreach (var filePath in _recentFiles)
            {
                yield return new RecentFileInfo
                {
                    FullPath = filePath,
                    FileName = Path.GetFileName(filePath),
                    Directory = Path.GetDirectoryName(filePath),
                    Exists = File.Exists(filePath),
                    LastAccessTime = File.Exists(filePath) 
                        ? File.GetLastWriteTime(filePath) 
                        : DateTime.MinValue
                };
            }
        }

        /// <summary>
        /// Carica la lista dei file recenti da disco.
        /// </summary>
        private void Load()
        {
            _recentFiles.Clear();

            try
            {
                if (File.Exists(_recentFilesPath))
                {
                    var lines = File.ReadAllLines(_recentFilesPath);
                    
                    foreach (var line in lines.Where(l => !string.IsNullOrWhiteSpace(l)))
                    {
                        // Aggiungi solo se il percorso è valido
                        if (IsValidPath(line))
                        {
                            _recentFiles.Add(line.Trim());
                        }
                    }

                    System.Diagnostics.Debug.WriteLine($"Caricati {_recentFiles.Count} file recenti");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore caricamento file recenti: {ex.Message}");
            }
        }

        /// <summary>
        /// Salva la lista dei file recenti su disco.
        /// </summary>
        private void Save()
        {
            try
            {
                File.WriteAllLines(_recentFilesPath, _recentFiles);
                System.Diagnostics.Debug.WriteLine($"Salvati {_recentFiles.Count} file recenti");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Errore salvataggio file recenti: {ex.Message}");
            }
        }

        /// <summary>
        /// Verifica se un percorso è valido.
        /// </summary>
        private bool IsValidPath(string path)
        {
            try
            {
                // Verifica che il percorso sia sintatticamente valido
                Path.GetFullPath(path);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Informazioni dettagliate su un file recente.
    /// </summary>
    public class RecentFileInfo
    {
        /// <summary>
        /// Percorso completo del file.
        /// </summary>
        public string FullPath { get; set; }

        /// <summary>
        /// Nome del file (senza percorso).
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Directory contenente il file.
        /// </summary>
        public string Directory { get; set; }

        /// <summary>
        /// True se il file esiste ancora.
        /// </summary>
        public bool Exists { get; set; }

        /// <summary>
        /// Data ultima modifica del file.
        /// </summary>
        public DateTime LastAccessTime { get; set; }

        /// <summary>
        /// Descrizione formattata per visualizzazione.
        /// </summary>
        public string DisplayText => Exists 
            ? FileName 
            : Localizer.Format("RecentFileMissingDisplayFormat", FileName);

        /// <summary>
        /// Tooltip con percorso completo.
        /// </summary>
        public string TooltipText => FullPath;
    }
}
