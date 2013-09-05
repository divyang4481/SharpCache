using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;

using Codeology.SharpCache.Providers;

namespace Codeology.SharpCache
{

    [Serializable]
    public class CacheException : Exception
    {

        public CacheException(string message) : base(message) {}
        public CacheException(string message, Exception inner) : base(message,inner) {}
        protected CacheException(SerializationInfo info, StreamingContext context) : base(info,context) {}

    }

    [Serializable]
    public class CacheAsyncException : Exception
    {

        public CacheAsyncException(string message) : base(message)
        {
            Key = null;
            State = null;
        }

        public CacheAsyncException(string message, Exception inner) : base(message,inner)
        {
            Key = null;
            State = null;
        }

        public CacheAsyncException(string key, object state, Exception e) : base("There was an exception in the async thread.",e)
        {
            Key = key;
            State = state;
        }

        protected CacheAsyncException(SerializationInfo info, StreamingContext context) : base(info,context) {}

        #region Properties

        public string Key
        {
            get;
            private set;
        }

        public object State
        {
            get;
            private set;
        }

        #endregion

    }

    public delegate void CacheCallback(object state);
    public delegate void CacheExistsCallback(bool exists, object state);
    public delegate void CacheGetCallback(object value, object state);
    public delegate void CacheGetCallback<T>(T value, object state);
    public delegate void CacheAsyncExceptionCallback(CacheAsyncException e);

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
        private static string name;
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
            name = String.Empty;
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

            // Add name if specified
            if (!String.IsNullOrEmpty(name)) {
                lock (locker) {
                    builder.Append(name);
                    builder.Append(":");
                }
            }

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
            object result = InternalGet(key);

            return result;
        }

        public static T Get<T>(string key)
        {
            object result = InternalGet(key);

            if (result == null) {
                return default(T);
            } else {
                return (T)result;
            }
        }

        public static void Set(string key, object value)
        {
            int minutes;

            lock (locker) {
                minutes = default_timeout;
            }

            InternalSet(key,value,DateTime.UtcNow.AddMinutes(minutes));
        }

        public static void Set<T>(string key, T value)
        {
            int minutes;

            lock (locker) {
                minutes = default_timeout;
            }

            InternalSet(key,value,DateTime.UtcNow.AddMinutes(minutes));
        }

        public static void Set(string key, object value, int minutes)
        {
            InternalSet(key,value,DateTime.UtcNow.AddMinutes(minutes));
        }

        public static void Set<T>(string key, T value, int minutes)
        {
            InternalSet(key,value,DateTime.UtcNow.AddMinutes(minutes));
        }

        public static void Set(string key, object value, TimeSpan ts)
        {
            InternalSet(key,value,DateTime.UtcNow.Add(ts));
        }

        public static void Set<T>(string key, T value, TimeSpan ts)
        {
            InternalSet(key,value,DateTime.UtcNow.Add(ts));
        }

        public static void Set(string key, object value, DateTime dt)
        {
            InternalSet(key,value,dt);
        }

        public static void Set<T>(string key, object value, DateTime dt)
        {
            InternalSet(key,value,dt);
        }

        public static void Unset(string key)
        {
            lock (locker) {
                if (!enabled) return;

                default_provider.Unset(key);
            }
        }

        private static object InternalGet(string key)
        {
            lock (locker) {
                if (!enabled) return null;

                return default_provider.Get(key);
            }
        }

        private static void InternalSet(string key, object value, DateTime expires)
        {
            lock (locker) {
                if (!enabled) return;

                default_provider.Set(key,value,expires);
            }
        }

        #endregion

        #region Async Methods

        public static void ClearAsync()
        {
            ClearAsync(null);
        }

        public static void ClearAsync(CacheCallback callback)
        {
            ClearAsync(callback,null);
        }

        public static void ClearAsync(CacheCallback callback, object state)
        {
            CacheAsyncExceptionCallback exception_handler = OnException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    Clear();

                    // Call callback
                    if (callback != null) callback(o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(null,state,e));
                }
            }),state);
        }

        public static void ExistsAsync(string key, CacheExistsCallback callback)
        {
            ExistsAsync(key,callback,null);
        }

        public static void ExistsAsync(string key, CacheExistsCallback callback, object state)
        {
            CacheAsyncExceptionCallback exception_handler = OnException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    bool exists = Exists(key);

                    // Call callback
                    if (callback != null) callback(exists,o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(key,state,e));
                }
            }),state);
        }

        public static void GetAsync(string key, CacheGetCallback callback)
        {
            GetAsync(key,callback,null);
        }

        public static void GetAsync(string key, CacheGetCallback callback, object state)
        {
            CacheAsyncExceptionCallback exception_handler = OnException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    object value = Get(key);

                    // Call callback
                    if (callback != null) callback(value,o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(key,state,e));
                }
            }),state);
        }

        public static void GetAsync<T>(string key, CacheGetCallback<T> callback)
        {
            GetAsync<T>(key,callback,null);
        }

        public static void GetAsync<T>(string key, CacheGetCallback<T> callback, object state)
        {
            CacheAsyncExceptionCallback exception_handler = OnException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    T value = Get<T>(key);

                    // Call callback
                    if (callback != null) callback(value,o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(key,state,e));
                }
            }),state);
        }

        public static void SetAsync(string key, object value)
        {
            SetAsync(key,value,null);
        }

        public static void SetAsync(string key, object value, CacheCallback callback)
        {
            SetAsync(key,value,callback,null);
        }

        public static void SetAsync(string key, object value, CacheCallback callback, object state)
        {
            CacheAsyncExceptionCallback exception_handler = OnException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    Set(key,value);

                    // Call callback
                    if (callback != null) callback(o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(null,null,e));
                }
            }),state);
        }

        public static void SetAsync<T>(string key, T value)
        {
            SetAsync<T>(key,value,null);
        }

        public static void SetAsync<T>(string key, T value, CacheCallback callback)
        {
            SetAsync<T>(key,value,callback,null);
        }

        public static void SetAsync<T>(string key, T value, CacheCallback callback, object state)
        {
            CacheAsyncExceptionCallback exception_handler = OnException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    Set<T>(key,value);

                    // Call callback
                    if (callback != null) callback(o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(key,state,e));
                }
            }),state);
        }

        public static void SetAsync(string key, object value, int minutes)
        {
            SetAsync(key,value,minutes,null);
        }

        public static void SetAsync(string key, object value, int minutes, CacheCallback callback)
        {
            SetAsync(key,value,minutes,callback,null);
        }

        public static void SetAsync(string key, object value, int minutes, CacheCallback callback, object state)
        {
            CacheAsyncExceptionCallback exception_handler = OnException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    Set(key,value,minutes);

                    // Call callback
                    if (callback != null) callback(o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(key,state,e));
                }
            }),state);
        }

        public static void SetAsync<T>(string key, T value, int minutes)
        {
            SetAsync<T>(key,value,minutes,null);
        }

        public static void SetAsync<T>(string key, T value, int minutes, CacheCallback callback)
        {
            SetAsync<T>(key,value,minutes,callback,null);
        }

        public static void SetAsync<T>(string key, T value, int minutes, CacheCallback callback, object state)
        {
            CacheAsyncExceptionCallback exception_handler = OnException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    Set<T>(key,value,minutes);

                    // Call callback
                    if (callback != null) callback(o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(key,state,e));
                }
            }),state);
        }

        public static void SetAsync(string key, object value, TimeSpan ts)
        {
            SetAsync(key,value,ts,null);
        }

        public static void SetAsync(string key, object value, TimeSpan ts, CacheCallback callback)
        {
            SetAsync(key,value,ts,callback,null);
        }

        public static void SetAsync(string key, object value, TimeSpan ts, CacheCallback callback, object state)
        {
            CacheAsyncExceptionCallback exception_handler = OnException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    Set(key,value,ts);

                    // Call callback
                    if (callback != null) callback(o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(key,state,e));
                }
            }),state);
        }

        public static void SetAsync<T>(string key, T value, TimeSpan ts)
        {
            SetAsync<T>(key,value,ts,null);
        }

        public static void SetAsync<T>(string key, T value, TimeSpan ts, CacheCallback callback)
        {
            SetAsync<T>(key,value,ts,callback,null);
        }

        public static void SetAsync<T>(string key, T value, TimeSpan ts, CacheCallback callback, object state)
        {
            CacheAsyncExceptionCallback exception_handler = OnException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    Set<T>(key,value,ts);

                    // Call callback
                    if (callback != null) callback(o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(key,state,e));
                }
            }),state);
        }

        public static void SetAsync(string key, object value, DateTime dt)
        {
            SetAsync(key,value,dt,null);
        }

        public static void SetAsync(string key, object value, DateTime dt, CacheCallback callback)
        {
            SetAsync(key,value,dt,callback,null);
        }

        public static void SetAsync(string key, object value, DateTime dt, CacheCallback callback, object state)
        {
            CacheAsyncExceptionCallback exception_handler = OnException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    Set(key,value,dt);

                    // Call callback
                    if (callback != null) callback(o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(key,state,e));
                }
            }),state);
        }

        public static void SetAsync<T>(string key, T value, DateTime dt)
        {
            SetAsync<T>(key,value,dt,null);
        }

        public static void SetAsync<T>(string key, T value, DateTime dt, CacheCallback callback)
        {
            SetAsync<T>(key,value,dt,callback,null);
        }

        public static void SetAsync<T>(string key, T value, DateTime dt, CacheCallback callback, object state)
        {
            CacheAsyncExceptionCallback exception_handler = OnException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    Set<T>(key,value,dt);

                    // Call callback
                    if (callback != null) callback(o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(key,state,e));
                }
            }),state);
        }

        public static void UnsetAsync(string key)
        {
            UnsetAsync(key,null);
        }

        public static void UnsetAsync(string key, CacheCallback callback)
        {
            UnsetAsync(key,callback,null);
        }

        public static void UnsetAsync(string key, CacheCallback callback, object state)
        {
            CacheAsyncExceptionCallback exception_handler = OnException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    Unset(key);

                    // Call callback
                    if (callback != null) callback(o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(key,state,e));
                }
            }),state);
        }

        #endregion

        #region Properties

        public static string Name
        {
            get {
                lock (locker) {
                    return name;
                }
            }
            set {
                lock (locker) {
                    name = value;

                    if (name == null) name = String.Empty;
                }
            }
        }

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

        #region Events

        public static event CacheAsyncExceptionCallback OnException;

        #endregion

    }



}
