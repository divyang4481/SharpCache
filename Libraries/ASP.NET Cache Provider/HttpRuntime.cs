using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Text;
using System.Web;

namespace Codeology.SharpCache.Providers
{

    public class HttpRuntimeCacheProvider : CacheProvider
    {

        private int timeout;

        public HttpRuntimeCacheProvider()
        {
            timeout = 10;
        }

        public HttpRuntimeCacheProvider(int t)
        {
            timeout = t;
        }

        public override object Get(string key)
        {
            try {
                return HttpRuntime.Cache.Get(key);
            } catch (Exception e) {
                throw new CacheException("There was an exception thrown with ASP.NET whilest trying to get an item.",e);
            }
        }

        public override void Set(string key, object value)
        {
            Set(key,value,timeout);
        }

        public override void Set(string key, object value, int minutes)
        {
            DateTime expiry = DateTime.UtcNow.AddMinutes(minutes);

            Set(key,value,expiry);
        }

        public override void Set(string key, object value, DateTime dt)
        {
            try {
                HttpRuntime.Cache.Insert(key,value,null,dt,System.Web.Caching.Cache.NoSlidingExpiration);
            } catch (Exception e) {
                throw new CacheException("There was an exception thrown with ASP.NET whilest trying to cache an item.",e);
            }
        }

        public override void Set(string key, object value, TimeSpan ts)
        {
            try {
                DateTime expiry = DateTime.UtcNow.Add(ts);

                HttpRuntime.Cache.Insert(key,value,null,expiry,System.Web.Caching.Cache.NoSlidingExpiration);
            } catch (Exception e) {
                throw new CacheException("There was an exception thrown with ASP.NET whilest trying to cache an item.",e);
            }
        }

        public override void Unset(string key)
        {
            try {
                HttpRuntime.Cache.Remove(key);
            } catch (Exception e) {
                throw new CacheException("There was an exception thrown with ASP.NET whilest trying to remove an item.",e);
            }
        }

        public override bool Exists(string key)
        {
            try {
                object value = HttpRuntime.Cache.Get(key);

                return (value != null);
            } catch (Exception e) {
                throw new CacheException("There was an exception thrown with ASP.NET whilest trying to retreive an item.",e);
            }
        }

        public override void Clear()
        {
            try {
                List<string> keys = new List<string>();

                foreach(DictionaryEntry entry in HttpRuntime.Cache) keys.Add(entry.Key.ToString());
                foreach(string key in keys) HttpRuntime.Cache.Remove(key);
            } catch (Exception e) {
                throw new CacheException("There was an exception thrown with ASP.NET whilest trying to retreive an item.",e);
            }   
        }

        public override void ClearByKey(string key)
        {
            try {
                List<string> keys = new List<string>();

                foreach(DictionaryEntry entry in HttpRuntime.Cache) {
                    string entry_key = entry.Key.ToString();

                    if (entry_key.StartsWith(key)) keys.Add(entry_key);
                }
                foreach(string k in keys) HttpRuntime.Cache.Remove(k);
            } catch (Exception e) {
                throw new CacheException("There was an exception thrown with ASP.NET whilest trying to retreive an item.",e);
            }  
        }

        protected override string GetName()
        {
            return "HttpRuntime";
        }

    }

}
