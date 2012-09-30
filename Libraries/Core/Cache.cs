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

    public static class Cache
    {

        private static object locker;
        private static bool enabled;
        private static int default_timeout;
        private static ICacheProvider provider;

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
            provider = new NullCacheProvider();
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
                builder.Append(CacheUtils.HashString(values[i].ToString()));

                if (i != (values.Length - 1)) builder.Append(":");
            }

            // Return
            return builder.ToString();
        }

        public static void Clear()
        {
            lock (locker) {
                if (!enabled) return;

                provider.Clear();
            }
        }

        public static bool Exists(string key)
        {
            lock (locker) {
                if (!enabled) return false;

                return provider.Exists(key);
            }
        }

        public static object Get(string key)
        {
            lock (locker) {
                if (!enabled) return null;

                return provider.Get(key);
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

        public static void Set(string key, object value, int minutes)
        {
            DateTime expires;

            lock (locker) {
                expires = DateTime.UtcNow.AddMinutes(minutes);
            }

            Set(key,value,expires);
        }

        public static void Set(string key, object value, TimeSpan ts)
        {
            DateTime expires;

            lock (locker) {
                expires = DateTime.UtcNow.Add(ts);
            }

            Set(key,value,expires);
        }

        public static void Set(string key, object value, DateTime dt)
        {
            lock (locker) {
                if (!enabled) return;

                provider.Set(key,value,dt);
            }
        }

        public static void Unset(string key)
        {
            lock (locker) {
                if (!enabled) return;

                provider.Unset(key);
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

        public static void SetAsync(string key, object value, CacheCallback callback, object state)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                // Perform
                Set(key,value);

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

        public static void SetAsync(string key, object value, TimeSpan ts, CacheCallback callback, object state)
        {
            ThreadPool.QueueUserWorkItem(new WaitCallback(delegate(object o) {
                // Perform
                Set(key,value,ts);

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

        public static ICacheProvider Provider
        {
            get {
                lock (locker) {
                    return provider;
                }
            }
            set {
                lock (locker) {
                    if (value != provider) {
                        // Uninitialize existing provider
                        provider.Uninitialize();

                        // Set new provider
                        if (value == null) {
                            provider = new NullCacheProvider();
                        } else {
                            provider = value;
                        }

                        // Initialize new provider
                        provider.Initialize();
                    }
                }
            }
        }

        #endregion

    }



}
