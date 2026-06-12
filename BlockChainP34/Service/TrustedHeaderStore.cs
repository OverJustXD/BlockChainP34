using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BlockChainP34.Service.P2P
{
    public static class TrustedHeaderStore
    {
        private static readonly object _sync = new();
        private static readonly string FilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "trusted_headers.json");
        private static HashSet<string> _cachedRoots = new(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded = false;

        public static void Load()
        {
            lock (_sync)
            {
                if (_loaded) return;

                if (File.Exists(FilePath))
                {
                    try
                    {
                        var json = File.ReadAllText(FilePath);
                        var roots = JsonSerializer.Deserialize<HashSet<string>>(json);

                        _cachedRoots = roots != null
                            ? new HashSet<string>(roots, StringComparer.OrdinalIgnoreCase)
                            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    }
                    catch
                    {
                        _cachedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    }
                }

                _loaded = true;
            }
        }

        public static void SyncFromChain(IEnumerable<BlockChainP34.Models.Block> chain)
        {
            lock (_sync)
            {
                Load();

                if (chain != null)
                {
                    foreach (var block in chain)
                    {
                        if (block != null && !string.IsNullOrWhiteSpace(block.MerkleRoot))
                            _cachedRoots.Add(block.MerkleRoot);
                    }
                }

                SaveLocked();
            }
        }

        public static void AddRoots(IEnumerable<string> roots)
        {
            lock (_sync)
            {
                Load();

                if (roots != null)
                {
                    foreach (var root in roots)
                    {
                        if (!string.IsNullOrWhiteSpace(root))
                            _cachedRoots.Add(root);
                    }
                }

                SaveLocked();
            }
        }

        public static bool Contains(string root)
        {
            lock (_sync)
            {
                Load();
                return !string.IsNullOrWhiteSpace(root) && _cachedRoots.Contains(root);
            }
        }

        public static int Count
        {
            get
            {
                lock (_sync)
                {
                    Load();
                    return _cachedRoots.Count;
                }
            }
        }

        private static void SaveLocked()
        {
            var json = JsonSerializer.Serialize(_cachedRoots.OrderBy(x => x).ToList(), new JsonSerializerOptions
            {
                WriteIndented = true
            });

            File.WriteAllText(FilePath, json);
        }
    }
}