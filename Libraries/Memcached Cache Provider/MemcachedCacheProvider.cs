using System;
using System.Collections.Generic;
using System.Text;

using Enyim.Caching;
using Enyim.Caching.Configuration;
using Enyim.Caching.Memcached;

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

        private string cache_name;
        private string[] cache_servers;
        private MemcachedClientConfiguration config;
        private MemcachedClient client;

        public MemcacheCacheProvider(string cacheName, string cacheServer) : this(cacheName,new string[] {cacheServer})
        {
        }

        public MemcacheCacheProvider(string cacheName, string[] cacheServers) : base()
        {
            cache_name = cacheName;
            cache_servers = cacheServers;
            config = null;
        }

        #region Methods

        public override void Initialize()
        {
            // Set up configuration
            config = new MemcachedClientConfiguration();

            var ts = new TimeSpan(0,0,30);

            config.SocketPool.ConnectionTimeout = ts;
            config.SocketPool.ReceiveTimeout = ts;
            config.SocketPool.DeadTimeout = ts;
            //config.SocketPool.QueueTimeout = ys;
            config.SocketPool.MinPoolSize = 1;
            config.SocketPool.MaxPoolSize = 10;

            foreach(string server in cache_servers) config.AddServer(server);

            config.Protocol = MemcachedProtocol.Text;

            // Create client
            client = new MemcachedClient(config);
        }

        public override void Uninitialize()
        {
            // Release client
            client.Dispose();
        }

        public override void Clear()
        {
            client.FlushAll();
        }

        public override bool Exists(string key)
        {
            object result = Get(key);

            return (key != null);
        }

        public override object Get(string key)
        {
            string hashed_key = GetKey(key);

            return client.Get(hashed_key);
        }

        public override void Set(string key, object value, DateTime dt)
        {
            string hashed_key = GetKey(key);
            bool result = client.Store(StoreMode.Set,hashed_key,value,dt);

            if (!result) throw new MemcacheException("Could not set item in Memcache.");
        }

        public override void Unset(string key)
        {
            string hashed_key = GetKey(key);
            bool result = client.Remove(hashed_key);

            if (!result) throw new MemcacheException("Could not unset item in Memcache.");
        }

        protected override Guid GetId()
        {
            return new Guid(PROVIDER_ID);
        }

        protected override string GetName()
        {
            return PROVIDER_NAME;
        }

        private string GetKey(string key)
        {
            string buffer;

            if (String.IsNullOrEmpty(cache_name)) {
                buffer = key;
            } else {
                buffer = cache_name + ":" + key;
            }

            return CacheUtils.HashString(buffer);
        }

        #endregion

        #region Properties

        //public MemcachedClientConfiguration Configuration
        //{
        //    get {
        //        return config;
        //    }
        //}

        #endregion

    }

}
