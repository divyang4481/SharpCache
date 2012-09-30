using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace Codeology.SharpCache.Providers
{

    public class HttpRuntimeCacheProvider : CacheProvider
    {

        private const string PROVIDER_ID = "{EF978B71-10E9-4D15-9F21-6B5AE31A4B6F}";
        private const string PROVIDER_NAME = "ASP.NET";

        #region Methods

        public override void Clear()
        {
            // Create list to hold keys
            List<string> keys = new List<string>();

            // Copy keys into list
            foreach(DictionaryEntry entry in HttpRuntime.Cache) keys.Add(entry.Key.ToString());

            // Process list and remove keys from cache
            foreach(string key in keys) keys.Remove(key);
        }

        public override bool Exists(string key)
        {
            object value = HttpRuntime.Cache[key];

            return (value != null);
        }

        public override object Get(string key)
        {
            return HttpRuntime.Cache[key];
        }

        public override void Set(string key, object value, DateTime dt)
        {
            HttpRuntime.Cache.Insert(key,value,null,dt,System.Web.Caching.Cache.NoSlidingExpiration);
        }

        public override void Unset(string key)
        {
            HttpRuntime.Cache.Remove(key);
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

    }

}
