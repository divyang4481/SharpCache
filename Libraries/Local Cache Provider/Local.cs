using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;

namespace Codeology.SharpCache.Providers
{

    public class LocalCacheProvider : CacheProvider
    {

        private const string PROVIDER_ID = "{9E1094F3-A61C-415E-A148-FE8EF79209F9}";
        private const string PROVIDER_NAME = "Local";

        class CachedItem
        {

            public byte[] Value;
            public DateTime Cached;
            public DateTime Expires;

        }

        private object locker;
        private long max_memory;
        private long cache_memory;
        private Dictionary<string,CachedItem> cache;
        private bool thread_terminate;
        private Thread thread;

        public LocalCacheProvider()
        {
            locker = new object();
            max_memory = (1024 * 1000) * 100; // Default 100mb max memory
            cache_memory = 0;
            cache = new Dictionary<string,CachedItem>();
            thread_terminate = false;
            thread = null;
        }

        #region Methods

        public override void Initialize()
        {
            lock (locker) {
                // If thread is already running, return
                if (thread != null) return;

                // Reset thread terminate flag
                thread_terminate = false;

                // Create thread and start
                thread = new Thread(new ThreadStart(ThreadProc));
                thread.IsBackground = true;
                thread.Priority = ThreadPriority.BelowNormal;
                thread.Start();
            }
        }

        public override void Uninitialize()
        {
            lock (locker) {
                // If thread is not running, return
                if (thread == null) return;

                // Signal thread to stop
                thread_terminate = true;
            }

            // Wait for thread to stop
            while (true) {
                lock (locker) {
                    if (thread == null) break;
                }

                Thread.Sleep(100);
            }
        }

        private void ThreadProc()
        {
            while (true) {
                lock (locker) {
                    // Check for thread terminate
                    if (thread_terminate) break;

                    // While cache memory exceeds maximum memory
                    while (cache_memory > max_memory) {
                        // Get oldest cached item key
                        string oldest_key = GetOldestCachedItem();

                        // If we have a key, remove it from the cache
                        if (!String.IsNullOrEmpty(oldest_key)) cache.Remove(oldest_key);
                    }

                    // Create list to store expired keys
                    List<string> expired_keys = new List<string>();

                    // Process cache for expired keys
                    foreach(KeyValuePair<string,CachedItem> kvp in cache) {
                        if (kvp.Value.Expires <= DateTime.UtcNow) expired_keys.Add(kvp.Key); 
                    }

                    // Remove expired cached items
                    foreach(string key in expired_keys) cache.Remove(key);
                }

                // Sleep for a bit
                Thread.Sleep(1000);
            }

            // Nullify thread
            lock (locker) {
                thread = null;
            }
        }

        private string GetOldestCachedItem()
        {
            DateTime dt = DateTime.UtcNow;
            string key = String.Empty;

            foreach(KeyValuePair<string,CachedItem> kvp in cache) {
                if (kvp.Value.Cached < dt) {
                    dt = kvp.Value.Cached;
                    key = kvp.Key;
                }
            }

            return key;
        }

        private CachedItem Serialize(object value, DateTime expires)
        {
            // Open memory stream
            MemoryStream mem = new MemoryStream();

            try {
                // Serialize value to memory
                BinaryFormatter formatter = new BinaryFormatter();

                formatter.Serialize(mem,value);

                // Create cached item
                CachedItem item = new CachedItem();

                item.Value = mem.ToArray();
                item.Cached = DateTime.UtcNow;
                item.Expires = expires;

                // Return
                return item;
            } finally {
                // Close memory stream
                mem.Close();
            }
        }

        private object Deserialize(CachedItem item)
        {
            // Open memory stream
            MemoryStream mem = new MemoryStream(item.Value);

            try {
                // Deserialize value from memory
                BinaryFormatter formatter = new BinaryFormatter();

                object value = formatter.Deserialize(mem);

                // Return
                return value;
            } finally {
                // Close memory stream
                mem.Close();
            }
        }

        public override void Clear()
        {
            lock (locker) {
                cache.Clear();
            }
        }

        public override bool Exists(string key)
        {
            lock (locker) {
                return cache.ContainsKey(key);
            }
        }

        public override object Get(string key)
        {
            lock (locker) {
                // If key doesn't exist, return null
                if (!cache.ContainsKey(key)) return null;

                // Get cached item
                CachedItem item = cache[key];

                // Deserialize cached item
                object result = Deserialize(item);

                // Return
                return result;
            }
        }

        public override void Set(string key, object value, DateTime dt)
        {
            lock (locker) {
                // If cached item exists already, remove it
                if (cache.ContainsKey(key)) cache.Remove(key);

                // Serialize value to item
                CachedItem item = Serialize(value,dt);

                // Increment cache size
                cache_memory += item.Value.LongLength;

                // Add item to cache
                cache.Add(key,item);
            }
        }

        public override void Unset(string key)
        {
            lock (locker) {
                // If cache contains the key
                if (cache.ContainsKey(key)) {
                    // Get cached item
                    CachedItem item = cache[key];

                    // Decrement cache size
                    cache_memory -= item.Value.LongLength;

                    // Remove item from cache
                    cache.Remove(key);
                }
            }
        }

        protected override Guid GetId()
        {
            return new Guid(PROVIDER_ID);
        }

        protected override string GetName()
        {
            return PROVIDER_NAME;
        }

        #endregion

        #region Properties

        public long MaxMemory
        {
            get {
                lock (locker) {
                    return max_memory;
                }
            }
            set {
                lock (locker) {
                    max_memory = value;
                }
            }
        }

        public long CacheMemory
        {
            get {
                lock (locker) {
                    return cache_memory;
                }
            }
        }

        #endregion

    }

}
