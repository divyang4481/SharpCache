using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

using Codeology.SharpCache.Providers;

namespace Codeology.SharpCache
{

    public class CacheException : Exception
    {

        public CacheException(string message) : base(message) {}
        public CacheException(string message, Exception inner) : base(message,inner) {}

    }

    public delegate void CacheCallback(object state);
    public delegate void CacheExistsCallback(bool exists, object state);
    public delegate void CacheGetCallback(object value, object state);
    public delegate void CacheGetCallback<T>(T value, object state);

    public static class Cache
    {

        public class CacheProviders
        {

            private object locker;
            private List<ICacheProvider> providers;

            internal CacheProviders(object locker, List<ICacheProvider> providers)
            {
                this.locker = locker;
                this.providers = providers;
            }

            #region Properties

            public int Count
            {
                get {
                    lock (locker) {
                        return providers.Count;
                    }
                }
            }

            public ICacheProvider this[int index]
            {
                get {
                    lock (locker) {
                        return providers[index];
                    }
                }
            }

            public ICacheProvider this[Guid id]
            {
                get {
                    lock (locker) {
                        foreach(ICacheProvider provider in providers) {
                            if (provider.Id == id) return provider;
                        }
                    }

                    return null;
                }
            }

            public ICacheProvider this[string name]
            {
                get {
                    lock (locker) {
                        foreach(ICacheProvider provider in providers) {
                            if (String.Compare(provider.Name,name,true) == 0) return provider;
                        }
                    }

                    return null;
                }
            }

            #endregion

        }

        private static object locker;
        private static bool enabled;
        private static int default_timeout;
        private static ICacheProvider default_provider;
        private static List<ICacheProvider> providers;
        private static CacheProviders providers_wrapper;

        static Cache()
        {
            // Initialize
            Initialize();
        }

        #region Methods

        private static void Initialize()
        {
            if (locker != null) return;

            locker = new object();
            enabled = true;
            default_timeout = 10;

            ICacheProvider null_provider = new NullCacheProvider();

            default_provider = null_provider;
            providers = new List<ICacheProvider>();
            
            RegisterProvider(null_provider);

            providers_wrapper = new CacheProviders(locker,providers);
        }

        public static void RegisterProvider(ICacheProvider provider)
        {
            RegisterProvider(provider,false);
        }

        public static void RegisterProvider(ICacheProvider provider, bool makeDefault)
        {
            lock (locker) {
                // Look for existing provider
                foreach(ICacheProvider prov in providers) {
                    if (prov.Id == provider.Id || String.Compare(prov.Name,provider.Name,true) == 0) throw new CacheException("Cache provider is already registered.");
                }

                // Add new provider
                providers.Add(provider);

                // Set default if required
                if (makeDefault) default_provider = provider;
            }
        }

        public static void UnregisterProvider(ICacheProvider provider)
        {
            lock (locker) {
                // Check provider is registered
                if (!providers.Contains(provider)) return;

                // If provider is default
                if (provider == default_provider) default_provider = new NullCacheProvider();

                // Remove provider
                providers.Remove(provider);
            }
        }

        public static string CreateKey(object value)
        {
            return CreateCompoundKey(value);
        }

        public static string CreateCompoundKey(params object[] values)
        {
            // Create somewhere to store values
            StringBuilder builder = new StringBuilder();

            // Process values
            for(int i = 0; i < values.Length; i++) {
                builder.Append(values[i] == null ? "null" : values[i].ToString());

                if (i != (values.Length - 1)) builder.Append(":");
            }

            // Return
            return CacheUtils.HashString(builder.ToString());
        }

        public static void Clear()
        {
            lock (locker) {
                if (!enabled) return;

                default_provider.Clear();
            }
        }

        public static bool Exists(string key)
        {
            lock (locker) {
                if (!enabled) return false;

                return default_provider.Exists(key);
            }
        }

        public static object Get(string key)
        {
            lock (locker) {
                if (!enabled) return null;

                return default_provider.Get(key);
            }
        }

        public static T Get<T>(string key)
        {
            lock (locker) {
                if (!enabled) return default(T);

                object result = default_provider.Get(key);

                if (result == null) {
                    return default(T);
                } else {
                    return (T)result;
                }
            }
        }

        public static void Set(string key, object value)
        {
            int minutes;

            lock (locker) {
                minutes = default_timeout;
            }

            Set(key,value,minutes);
        }

        public static void Set<T>(string key, T value)
        {
            int minutes;

            lock (locker) {
                minutes = default_timeout;
            }

            Set<T>(key,value,minutes);
        }

        public static void Set(string key, object value, int minutes)
        {
            DateTime expires;

            lock (locker) {
                expires = DateTime.UtcNow.AddMinutes(minutes);
            }

            Set(key,value,expires);
        }

        public static void Set<T>(string key, T value, int minutes)
        {
            DateTime expires;

            lock (locker) {
                expires = DateTime.UtcNow.AddMinutes(minutes);
            }

            Set<T>(key,value,expires);
        }

        public static void Set(string key, object value, TimeSpan ts)
        {
            DateTime expires;

            lock (locker) {
                expires = DateTime.UtcNow.Add(ts);
            }

            Set(key,value,expires);
        }

        public static void Set<T>(string key, T value, TimeSpan ts)
        {
            DateTime expires;

            lock (locker) {
                expires = DateTime.UtcNow.Add(ts);
            }

            Set<T>(key,value,expires);
        }

        public static void Set(string key, object value, DateTime dt)
        {
            lock (locker) {
                if (!enabled) return;

                default_provider.Set(key,value,dt);
            }
        }

        public static void Set<T>(string key, object value, DateTime dt)
        {
            lock (locker) {
                if (!enabled) return;

                default_provider.Set(key,value,dt);
            }
        }

        public static void Unset(string key)
        {
            lock (locker) {
                if (!enabled) return;

                default_provider.Unset(key);
            }
        }

        #endregion

        #region Async Methods

        public static void ClearAsync(CacheCallback callback, object state)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                // Perform
                Clear();

                // Call callback
                if (callback != null) callback(o);
            }),state);
        }

        public static void ExistsAsync(string key, CacheExistsCallback callback, object state)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                // Perform
                bool exists = Exists(key);

                // Call callback
                if (callback != null) callback(exists,o);
            }),state);
        }

        public static void GetAsync(string key, CacheGetCallback callback, object state)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                // Perform
                object value = Get(key);

                // Call callback
                if (callback != null) callback(value,o);
            }),state);
        }

        public static void GetAsync<T>(string key, CacheGetCallback<T> callback, object state)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                // Perform
                T value = Get<T>(key);

                // Call callback
                if (callback != null) callback(value,o);
            }),state);
        }

        public static void SetAsync(string key, object value, CacheCallback callback, object state)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                // Perform
                Set(key,value);

                // Call callback
                if (callback != null) callback(o);
            }),state);
        }

        public static void SetAsync<T>(string key, T value, CacheCallback callback, object state)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                // Perform
                Set<T>(key,value);

                // Call callback
                if (callback != null) callback(o);
            }),state);
        }

        public static void SetAsync(string key, object value, int minutes, CacheCallback callback, object state)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                // Perform
                Set(key,value,minutes);

                // Call callback
                if (callback != null) callback(o);
            }),state);
        }

        public static void SetAsync<T>(string key, T value, int minutes, CacheCallback callback, object state)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                // Perform
                Set<T>(key,value,minutes);

                // Call callback
                if (callback != null) callback(o);
            }),state);
        }

        public static void SetAsync(string key, object value, TimeSpan ts, CacheCallback callback, object state)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                // Perform
                Set(key,value,ts);

                // Call callback
                if (callback != null) callback(o);
            }),state);
        }

        public static void SetAsync<T>(string key, T value, TimeSpan ts, CacheCallback callback, object state)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                // Perform
                Set<T>(key,value,ts);

                // Call callback
                if (callback != null) callback(o);
            }),state);
        }

        public static void SetAsync(string key, object value, DateTime dt, CacheCallback callback, object state)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                // Perform
                Set(key,value,dt);

                // Call callback
                if (callback != null) callback(o);
            }),state);
        }

        public static void SetAsync<T>(string key, T value, DateTime dt, CacheCallback callback, object state)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                // Perform
                Set<T>(key,value,dt);

                // Call callback
                if (callback != null) callback(o);
            }),state);
        }

        public static void UnsetAsync(string key, CacheCallback callback, object state)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                // Perform
                Unset(key);

                // Call callback
                if (callback != null) callback(o);
            }),state);
        }

        #endregion

        #region Properties

        public static bool Enabled
        {
            get {
                lock (locker) {
                    return enabled;
                }
            }
            set {
                lock (locker) {
                    enabled = value;
                }
            }
        }

        public static int DefaultTimeout
        {
            get {
                lock (locker) {
                    return default_timeout;
                }
            }
            set {
                lock (locker) {
                    default_timeout = value;
                }
            }
        }

        public static ICacheProvider DefaultProvider
        {
            get {
                lock (locker) {
                    return default_provider;
                }
            }
            set {
                lock (locker) {
                    if (default_provider != value) {
                        // Check provider is registered
                        if (!providers.Contains(value)) throw new CacheException("Cache provider is not registered.");

                        // If null create new null provider otherwise just assign
                        if (value == null) {
                            default_provider = new NullCacheProvider();
                        } else {
                            default_provider = value;
                        }
                    }
                }
            }
        }

        public static CacheProviders Providers
        {
            get {
                return providers_wrapper;
            }
        }

        #endregion

    }



}
