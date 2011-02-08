using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;

namespace Codeology.SharpCache.Providers
{

    [StructLayout(LayoutKind.Sequential,Pack=1)]
    internal class CacheItem : IDisposable
    {

        private bool disposed;
        private MemoryStream value_stream;
        private DateTime created;
        private DateTime accessed;
        private DateTime expires;

        public CacheItem(object value, DateTime expiry)
        {
            disposed = false;
            value_stream = new MemoryStream();
            created = DateTime.Now;
            accessed = DateTime.Now;
            expires = expiry;

            SetValue(value);
        }

        #region Methods

        public void Dispose()
        {
            if (!disposed) {
                // Release memory stream
                value_stream.Close();

                // Suppress GC finalization
                GC.SuppressFinalize(this);

                // Mark as disposed
                disposed = true;
            }
        }

        public void Touch()
        {
            accessed = DateTime.Now;
        }

        private void SetValue(object value)
        {
            // Dispose current stream
            value_stream.Close();

            // Create new stream
            value_stream = new MemoryStream();

            // If value is null, return
            if (value == null) return;

            // Serialize value to stream
            Serialize(value,value_stream);
        }

        private void Serialize(object obj, Stream stream)
        {
            if (obj == null) return;

            BinaryFormatter bf = new BinaryFormatter();

            bf.Serialize(stream,obj);
        }

        private object Deserialize(Stream stream)
        {
            if (stream.Length == 0) return null;

            BinaryFormatter bf = new BinaryFormatter();

            object obj = bf.Deserialize(stream);

            return obj;
        }

        #endregion

        #region Properties

        public object Value
        {
            get {
                // If an empty stream, return null
                if (value_stream.Length == 0) return null;

                // Reset stream position
                value_stream.Seek(0,SeekOrigin.Begin);

                // Deserialize value
                return Deserialize(value_stream);
            }
            set {
                SetValue(value);
            }
        }

        public long ValueSize
        {
            get {
                return value_stream.Length;
            }
        }

        public DateTime Created
        {
            get {
                return created;
            }
        }

        public DateTime Accessed
        {
            get {
                return accessed;
            }
        }

        public DateTime Expires
        {
            get {
                return expires;
            }
        }

        #endregion

    }

    public enum LocalCachePurgeKind
    {
        Oldest,
        Largest,
        LeastUsed
    }

    public class LocalCacheProvider : CacheProvider
    {

        private object locker;
        private long max_memory;
        private LocalCachePurgeKind purge_kind;
        private Dictionary<string,CacheItem> cache;
        private bool thread_terminate;
        private Thread thread;

        public LocalCacheProvider()
        {
            locker = new object();
            max_memory = 64 * 1024 * 1024;
            purge_kind = LocalCachePurgeKind.LeastUsed;
            cache = new Dictionary<string,CacheItem>();
            thread_terminate = false;
            thread = new Thread(new ThreadStart(ThreadProc));
            thread.IsBackground = true;
        }

        public override void Initialize()
        {
            if (HasInitialized) return;

            // Start thread
            thread.Start();

            // Inherited call
            base.Initialize();
        }

        public override void Uninitialize()
        {
            if (HasUninitialized) return;

            // Stop thread
            lock (locker) {
                thread_terminate = true;
            }

            // Inherited call
            base.Uninitialize();
        }

        public override object Get(string key)
        {
            lock (locker) {
                // Check if item exists
                if (!cache.ContainsKey(key)) return null;

                // Get item
                CacheItem item = cache[key];

                // Return
                return item.Value;
            }
        }

        public override void Set(string key, object value)
        {
            Set(key,value,10);
        }

        public override void Set(string key, object value, int minutes)
        {
            DateTime expiry = DateTime.UtcNow.AddMinutes(minutes);

            Set(key,value,expiry);
        }

        public override void Set(string key, object value, DateTime dt)
        {
            lock (locker) {
                // Check if item exists
                if (cache.ContainsKey(key)) {
                    // Dispose of item
                    cache[key].Dispose();

                    // Remove item
                    cache.Remove(key);
                }
            }

            // Create new item
            CacheItem item = new CacheItem(value,dt);

            // Check memory, first pass
            if ((CurrentMemory + item.ValueSize) >= MaxMemory) Purge();

            // Check memory, second pass
            if ((CurrentMemory + item.ValueSize) >= MaxMemory) return;

            lock (locker) {
                // Add item to cache
                cache.Add(key,item);
            }
        }

        public override void Set(string key, object value, TimeSpan ts)
        {
            throw new NotSupportedException();
        }

        public override void Unset(string key)
        {
            lock (locker) {
                // Check if item exists
                if (cache.ContainsKey(key)) {
                    // Dispose of item
                    cache[key].Dispose();

                    // Remove item
                    cache.Remove(key);
                }
            }
        }

        public override bool Exists(string key)
        {
            lock (locker) {
                // Check if item exists
                return cache.ContainsKey(key);
            }
        }

        public override void Clear()
        {
            lock (locker) {
                List<string> keys = new List<string>();

                foreach(KeyValuePair<string,CacheItem> kvp in cache) keys.Add(kvp.Key);

                foreach(string key in keys) {
                    // Dispose of item
                    cache[key].Dispose();

                    // Remove item
                    cache.Remove(key);
                }
            }
        }

        public override void ClearByKey(string key)
        {
            lock (locker) {
                foreach(KeyValuePair<string,CacheItem> kvp in cache) {
                    if (kvp.Key == key) {
                        // Dispose of item
                        cache[key].Dispose();

                        // Remove item
                        cache.Remove(key);

                        // Return
                        return;
                    }
                }
            }
        }

        protected override string GetName()
        {
            return "Local";
        }

        private void Purge()
        {

        }

        private void ThreadProc()
        {
            while (true) {
                lock (locker) {
                    // If set to terminate, break
                    if (thread_terminate) break;

                    // Process cached items
                    List<string> dead_keys = new List<string>();

                    foreach(KeyValuePair<string,CacheItem> kvp in cache) {
                        if (kvp.Value.Expires <= DateTime.Now) dead_keys.Add(kvp.Key);
                    }

                    foreach(string key in dead_keys) {
                        // Dispose of item
                        cache[key].Dispose();

                        // Remove item
                        cache.Remove(key);
                    }
                }

                // Sleep for a bit
                Thread.Sleep(500);
            }
        }

        #region Properties

        public long MaxMemory
        {
            get {
                return max_memory;
            }
            set {
                max_memory = value;

                Purge();
            }
        }

        public long CurrentMemory
        {
            get {
                lock (locker) {
                    long result = 0;

                    foreach(KeyValuePair<string,CacheItem> item in cache) result += item.Value.ValueSize;

                    return result;
                }
            }
        }

        public LocalCachePurgeKind PurgeKind
        {
            get {
                return purge_kind;
            }
            set {
                purge_kind = value;
            }
        }

        #endregion

    }

}
