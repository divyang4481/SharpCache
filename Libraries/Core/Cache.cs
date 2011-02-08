using System;
//using System.Collections;
using System.Collections.Generic;
using System.Text;

using Codeology.SharpCache.Providers;

namespace Codeology.SharpCache
{

    public class CacheException : Exception
    {

        public CacheException(string message) : base(message) {}
        public CacheException(string message, Exception inner) : base(message,inner) {}

    }

    public static class Cache
    {

        #region Static Members

        private static bool has_init;
        private static bool has_uninit;
        private static bool use_cache;
 
        private static Dictionary<string,CacheProvider> providers;
        private static CacheProvider empty;
        private static CacheProvider def;

        static Cache()
        {
            // Set up flags
            has_init = false;
            has_uninit = false;
            use_cache = false;
        }

        public static void Initialize()
        {
            // If already initialized return
            if (has_init) return;

            // Set up providers
            providers = new Dictionary<string,CacheProvider>();
            empty = new EmptyCacheProvider();
            def = empty;

            // Hook into AppDomain unloading
            AppDomain.CurrentDomain.DomainUnload += new EventHandler(DomainUnload);

            // Toggle
            use_cache = true;
            has_init = true;
        }

        private static void DomainUnload(object sender, EventArgs args)
        {
            // Uninitialize
            Uninitialize();
        }

        public static void Uninitialize()
        {
            // If already uninitialized return
            if (has_uninit) return;

            // Toggle
            has_uninit = true;

            // Clear down providers
            List<string> names = new List<string>();

            foreach(string name in providers.Keys) names.Add(name);

            foreach(string name in names) {
                providers[name].Uninitialize();
                providers.Remove(name);
            }
        }

        public static bool Use
        {
            get {
                return use_cache;
            }
            set {
                use_cache = value;
            }
        }

        public static void RegisterProvider(CacheProvider cacheProvider)
        {
            // Check for existing registration
            if (providers.ContainsKey(cacheProvider.Name)) throw new CacheException("A provider with that name is already registered.");

            // Add provider to known list
            providers.Add(cacheProvider.Name,cacheProvider);

            // Check default
            if (def == null) {
                def = cacheProvider;
            } else if (String.Compare(def.Name,"Empty",true) == 0) {
                def = cacheProvider;
            }

            // Initialize provider
            cacheProvider.Initialize();
        }

        public static void UnregisterProvider(CacheProvider cacheProvider)
        {
            if (providers.ContainsKey(cacheProvider.Name)) {
                // Remove provider
                providers.Remove(cacheProvider.Name);

                // Check provider wasn't default
                if (def != null) {
                    if (String.Compare(def.Name,cacheProvider.Name,true) == 0) {
                        def = null;

                        // Set new default
                        if (providers.Count > 0) {
                            foreach(KeyValuePair<string,CacheProvider> kvp in providers) {
                                def = kvp.Value;
                                break;
                            }
                        }
                    }
                }

                // Return
                return;
            }

            // Provider wasn't found
            throw new CacheException("Could not find registered provider with given name.");
        }

        public static object Get(string key)
        {
            if (def == null) throw new CacheException("There is no default cache provider assigned.");

            return def.Get(key);
        }

        public static void Set(string key, object value)
        {
            if (def == null) throw new CacheException("There is no default cache provider assigned.");

            def.Set(key,value);
        }

        public static void Set(string key, object value, int minutes)
        {
            if (def == null) throw new CacheException("There is no default cache provider assigned.");

            def.Set(key,value,minutes);
        }

        public static void Set(string key, object value, DateTime dt)
        {
            if (def == null) throw new CacheException("There is no default cache provider assigned.");

            def.Set(key,value,dt);
        }

        public static void Set(string key, object value, TimeSpan ts)
        {
            if (def == null) throw new CacheException("There is no default cache provider assigned.");

            def.Set(key,value,ts);
        }

        public static void Unset(string key)
        {
            if (def == null) throw new CacheException("There is no default cache provider assigned.");

            def.Unset(key);
        }

        public static bool Exists(string key)
        {
            if (def == null) throw new CacheException("There is no default cache provider assigned.");

            return def.Exists(key);
        }

        public static void Clear()
        {
            if (def == null) throw new CacheException("There is no default cache provider assigned.");

            def.Clear();
        }

        public static CacheProvider Default
        {
            get {
                return def;
            }
            set {
                def = value;
            }
        }

        public static CacheProvider Empty
        {
            get {
                return empty; 
            }
        }

        #endregion

    }




}
