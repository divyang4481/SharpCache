using System;
using System.Collections.Generic;
using System.Text;

namespace Codeology.SharpCache.Providers
{


    public class EmptyCacheProvider : CacheProvider
    {

        public override object Get(string key)
        {
            return null;
        }

        public override void Set(string key, object value)
        {
            Set(key,value,10);
        }

        public override void Set(string key, object value, int minutes)
        {
            DateTime now = DateTime.UtcNow.AddMinutes(minutes);

            Set(key,value,now);
        }

        public override void Set(string key, object value, DateTime dt)
        {
            // Do nothing...
        }

        public override void Set(string key, object value, TimeSpan ts)
        {
            // Do nothing...
        }

        public override void Unset(string key)
        {
            // Do nothing...
        }

        public override bool Exists(string key)
        {
            return false;
        }

        protected override string GetName()
        {
            return "Empty";
        }

    }

}
