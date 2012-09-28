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

        }

        private object locker;
        private long max_memory;
        private Dictionary<string,CachedItem> cache;
        private bool thread_terminate;
        private Thread thread;

        public LocalCacheProvider()
        {
            locker = new object();
            max_memory = (1024 * 1000) * 100; // Default 100mb max memory
            cache = new Dictionary<string,CachedItem>();
            thread_terminate = false;
            thread = null;
        }

        #region Methods

        public override void Initialize()
        {
            if (thread != null) return;

            // Reset thread terminate flag
            thread_terminate = false;

            // Create thread and start
            thread = new Thread(new ThreadStart(ThreadProc));
            thread.Priority = ThreadPriority.BelowNormal;
            thread.Start();
        }

        public override void Uninitialize()
        {
            if (thread == null) return;

            // Signal thread to stop
            lock (locker) {
                thread_terminate = true;
            }

            // Wait for thread to stop then nullify
            thread.Join();
            thread = null;
        }

        private void ThreadProc()
        {
            while (true) {
                // Check for thread terminate
                lock (locker) {
                    if (thread_terminate) break;
                }

                //

                // Sleep for a bit
                Thread.Sleep(1000);
            }
        }

        private CachedItem Serialize(object value)
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
                CachedItem item = Serialize(value);

                // Add item to cache
                cache.Add(key,item);
            }
        }

        public override void Unset(string key)
        {
            lock (locker) {
                if (cache.ContainsKey(key)) cache.Remove(key);
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

        #endregion

    }

}
