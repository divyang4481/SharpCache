using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;

using Codeology.SharpCache.Providers;

namespace Codeology.SharpCache
{

    [Serializable]
    public class CacheException : Exception
    {

        private string cache_key;

        public CacheException(string message) : base(message)
        {
            cache_key = String.Empty;
        }

        public CacheException(string message, Exception inner) : base(message,inner)
        {
            cache_key = String.Empty;
        }

        public CacheException(string key, string message) : base(message)
        {
            cache_key = key;
        }

        public CacheException(string key, string message, Exception inner) : base(message,inner)
        {
            cache_key = key;
        }

        protected CacheException(SerializationInfo info, StreamingContext context) : base(info,context)
        {
            cache_key = (string)info.GetString("cache_key");
        }

        #region Methods

        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            // Inherited call
            base.GetObjectData(info,context);

            // Add key
            info.AddValue("cache_key",cache_key);
        }

        #endregion

        #region Properties

        public string Key
        {
            get {
                return cache_key;
            }
            set {
                cache_key = value;
            }
        }

        #endregion

    }

    [Serializable]
    public class CacheAsyncException : CacheException
    {

        private object cache_state;

        public CacheAsyncException(string message) : base(message)
        {
            cache_state = null;
        }

        public CacheAsyncException(string message, Exception inner) : base(message,inner)
        {
            cache_state = null;
        }

        public CacheAsyncException(string key, object state, string message) : base(key,message)
        {
            cache_state = state;
        }

        public CacheAsyncException(string key, object state, string message, Exception inner) : base(key,message,inner)
        {
            cache_state = state;
        }

        protected CacheAsyncException(SerializationInfo info, StreamingContext context) : base(info,context)
        {
            cache_state = null;
        }

        #region Properties

        public object State
        {
            get {
                return cache_state;
            }
        }

        #endregion

    }

    public delegate void CacheCallback(object state);
    public delegate void CacheExistsCallback(bool exists, object state);
    public delegate void CacheGetCallback(object value, object state);
    public delegate void CacheGetCallback<T>(T value, object state);
    public delegate void CacheExceptionCallback(CacheException e);
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
        private static bool ignore_unserializable;

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

            ignore_unserializable = false;
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
                    if (prov.Id == provider.Id || String.Compare(prov.Name,provider.Name,true) == 0) {
                        var ex = new CacheException("Cache provider is already registered.");

                        InternalException(null,ex);
                    }
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

        public static string CreateUnnamedKey(object value)
        {
            return CreateUnnamedCompoundKey(value);
        }

        public static string CreateCompoundKey(params object[] values)
        {
            // Get a copy of the cache name
            string cache_name;

            lock (locker) {
                cache_name = name;
            }

            // Create list from values
            List<object> list = new List<object>(values);

            // Insert name into list
            if (!String.IsNullOrEmpty(cache_name)) list.Insert(0,cache_name);

            // Pass on
            return CreateUnnamedCompoundKey(list.ToArray());
        }

        public static string CreateUnnamedCompoundKey(params object[] values)
        {
            // Create somewhere to store values
            StringBuilder builder = new StringBuilder();

            // Process values
            for(int i = 0; i < values.Length; i++) {
                builder.Append(values[i] == null ? "null" : values[i].ToString());

                if (i != (values.Length - 1)) builder.Append(":");
            }

            // Get key
            string key = builder.ToString();

            // Return
            return key;
        }

        public static bool CanSerialize(object value)
        {
            if (value == null) return false;

            using (Stream stream = new NullStream()) {
                try {
                    BinaryFormatter formatter = new BinaryFormatter();

                    formatter.Serialize(stream,value);
                } catch {
                    return false;
                }
            }

            return true;
        }

        public static bool CanSerialize<T>(T value)
        {
            object obj = (object)value;

            return CanSerialize(obj);
        }

        public static void Clear()
        {
            lock (locker) {
                if (!enabled) return;

                try {
                    default_provider.Clear();
                } catch (Exception e) {
                    if (!InternalException(null,e)) throw;
                }
            }
        }

        public static bool Exists(string key)
        {
            lock (locker) {
                if (!enabled) return false;

                try {
                    return default_provider.Exists(key);
                } catch (Exception e) {
                    if (!InternalException(key,e)) throw;

                    return false;
                }
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

                try {
                    default_provider.Unset(key);
                } catch (Exception e) {
                    if (!InternalException(key,e)) throw;
                }
            }
        }

        private static object InternalGet(string key)
        {
            lock (locker) {
                if (!enabled) return null;

                try {
                    return default_provider.Get(key);
                } catch (Exception e) {
                    if (!InternalException(key,e)) throw;

                    return null;
                }
            }
        }

        private static void InternalSet(string key, object value, DateTime expires)
        {
            lock (locker) {
                if (!enabled) return;

                if (ignore_unserializable) {
                    if (!CanSerialize(value)) return;
                }

                try {
                    default_provider.Set(key,value,expires);
                } catch (Exception e) {
                    if (!InternalException(key,e)) throw;
                }
            }
        }

        private static bool InternalException(string key, Exception e)
        {
            if (OnException != null) {
                CacheException ex = new CacheException(key ?? String.Empty,"there was an internal cache exception.",e);

                OnException(ex);

                return true;
            } else {
                return false;
            }
        }

        private static void InternalSetAsync(string key, object value, DateTime expires, object state)
        {
            lock (locker) {
                if (!enabled) return;

                if (ignore_unserializable) {
                    if (!CanSerialize(value)) return;
                }

                try {
                    default_provider.Set(key,value,expires);
                } catch (Exception e) {
                    if (!InternalAsyncException(key,state,e)) throw;
                }
            }
        }

        private static bool InternalAsyncException(string key, object state, Exception e)
        {
            if (OnAsyncException != null) {
                CacheAsyncException ex = new CacheAsyncException(key,state,"There was an internal cache async exception.",e);

                OnAsyncException(ex);

                return true;
            } else {
                return false;
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
            CacheAsyncExceptionCallback exception_handler = OnAsyncException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    Clear();

                    // Call callback
                    if (callback != null) callback(o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(null,state,"Failed to clear the cache.",e));
                }
            }),state);
        }

        public static void ExistsAsync(string key, CacheExistsCallback callback)
        {
            ExistsAsync(key,callback,null);
        }

        public static void ExistsAsync(string key, CacheExistsCallback callback, object state)
        {
            CacheAsyncExceptionCallback exception_handler = OnAsyncException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    bool exists = Exists(key);

                    // Call callback
                    if (callback != null) callback(exists,o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(key,state,"Failed to check if the given item exists in the cahce.",e));
                }
            }),state);
        }

        public static void GetAsync(string key, CacheGetCallback callback)
        {
            GetAsync(key,callback,null);
        }

        public static void GetAsync(string key, CacheGetCallback callback, object state)
        {
            CacheAsyncExceptionCallback exception_handler = OnAsyncException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    object value = Get(key);

                    // Call callback
                    if (callback != null) callback(value,o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(key,state,"Failed to get item from the cache.",e));
                }
            }),state);
        }

        public static void GetAsync<T>(string key, CacheGetCallback<T> callback)
        {
            GetAsync<T>(key,callback,null);
        }

        public static void GetAsync<T>(string key, CacheGetCallback<T> callback, object state)
        {
            CacheAsyncExceptionCallback exception_handler = OnAsyncException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    T value = Get<T>(key);

                    // Call callback
                    if (callback != null) callback(value,o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(key,state,"Failed to get item from the cache.",e));
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
            CacheAsyncExceptionCallback exception_handler = OnAsyncException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    Set(key,value);

                    // Call callback
                    if (callback != null) callback(o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(key,state,"Failed to set item in the cache.",e));
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
            CacheAsyncExceptionCallback exception_handler = OnAsyncException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    Set<T>(key,value);

                    // Call callback
                    if (callback != null) callback(o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(key,state,"Failed to set item in the cache.",e));
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
            CacheAsyncExceptionCallback exception_handler = OnAsyncException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    Set(key,value,minutes);

                    // Call callback
                    if (callback != null) callback(o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(key,state,"Failed to set item in the cache.",e));
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
            CacheAsyncExceptionCallback exception_handler = OnAsyncException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    Set<T>(key,value,minutes);

                    // Call callback
                    if (callback != null) callback(o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(key,state,"Failed to set item in the cache.",e));
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
            CacheAsyncExceptionCallback exception_handler = OnAsyncException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    Set(key,value,ts);

                    // Call callback
                    if (callback != null) callback(o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(key,state,"Failed to set item in the cache.",e));
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
            CacheAsyncExceptionCallback exception_handler = OnAsyncException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    Set<T>(key,value,ts);

                    // Call callback
                    if (callback != null) callback(o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(key,state,"Failed to set item in the cache.",e));
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
            CacheAsyncExceptionCallback exception_handler = OnAsyncException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    InternalSetAsync(key,value,dt,state);

                    // Call callback
                    if (callback != null) callback(o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(key,state,"Failed to set item in the cache.",e));
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
            CacheAsyncExceptionCallback exception_handler = OnAsyncException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    InternalSetAsync(key,value,dt,state);

                    // Call callback
                    if (callback != null) callback(o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(key,state,"Failed to set item in the cache.",e));
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
            CacheAsyncExceptionCallback exception_handler = OnAsyncException;

            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                try {
                    // Perform
                    Unset(key);

                    // Call callback
                    if (callback != null) callback(o);
                } catch (Exception e) {
                    if (exception_handler == null) throw;

                    exception_handler(new CacheAsyncException(key,state,"Failed to unset item in the cache.",e));
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

        public static bool IgnoreUnserializable
        {
            get {
                lock (locker) {
                    return ignore_unserializable;
                }
            }
            set {
                lock (locker) {
                    ignore_unserializable = value;
                }
            }
        }

        #endregion

        #region Events

        public static event CacheExceptionCallback OnException;
        public static event CacheAsyncExceptionCallback OnAsyncException;

        #endregion

    }



}
