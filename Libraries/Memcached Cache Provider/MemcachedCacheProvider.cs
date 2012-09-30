using System;
using System.Collections.Generic;
using System.Text;

using BeIT.MemCached;

namespace Codeology.SharpCache.Providers
{

    public class MemcacheException : CacheException
    {

        public MemcacheException(string message) : base(message) {}
        public MemcacheException(string message, Exception inner) : base(message,inner) {}

    }

    public class MemcacheCacheProvider : CacheProvider
    {

        private const string PROVIDER_ID = "{1DDE54E8-6FEF-454D-8DCC-76E6E07B6066}";
        private const string PROVIDER_NAME = "Memcache";

        private object locker;
        private string cache_name;
        private string[] cache_servers;
        private MemcachedClient client;

        public MemcacheCacheProvider(string cacheName, string cacheServer) : this(cacheName,new string[] {cacheServer})
        {
        }

        public MemcacheCacheProvider(string cacheName, string[] cacheServers)
        {
            locker = new object();
            cache_name = cacheName;
            cache_servers = cacheServers;
            client = null;
        }

        #region Methods

        public override void Initialize()
        {
            // Set up client
            MemcachedClient.Setup(cache_name,cache_servers);

            // Get client instance
            client = MemcachedClient.GetInstance(cache_name);
        }

        public override void Clear()
        {
            lock (locker) {
                bool result = client.FlushAll();

                if (!result) throw new MemcacheException("Could not clear all items from Memcache.");
            }
        }

        public override bool Exists(string key)
        {
            lock (locker) {
                object value;

                try {
                    value = client.Get(key);
                } catch {
                    value = null;
                }

                return (value != null);
            }
        }

        public override object Get(string key)
        {
            lock (locker) {
                return client.Get(key);
            }
        }

        public override void Set(string key, object value, DateTime dt)
        {
            lock (locker) {
                bool result = client.Set(key,value,dt);

                if (!result) throw new MemcacheException("Could not set item in Memcache.");
            }
        }

        public override void Unset(string key)
        {
            lock (locker) {
                bool result = client.Delete(key);

                if (!result) throw new MemcacheException("Could not unset item in Memcache.");
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

        public MemcachedClient Client
        {
            get {
                lock (locker) {
                    return client;
                }
            }
        }

        #endregion

    }

}
