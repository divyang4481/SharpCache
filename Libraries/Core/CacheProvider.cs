using System;
using System.Collections.Generic;
using System.Text;

namespace Codeology.SharpCache
{

    public abstract class CacheProvider : IDisposable
    {

        private bool disposed;
        private bool has_init;
        private bool has_uninit;

        public CacheProvider()
        {
            disposed = false;
            has_init = false;
            has_uninit = false;
        }

        #region Methods

        public void Dispose()
        {
            if (!disposed) {
                // Uninitialize cache provider
                Uninitialize();

                // Suppress GC finalization
                GC.SuppressFinalize(this);

                // Mark as disposed
                disposed = true;
            }
        }

        public virtual void Initialize()
        {
            // If already init, return
            if (has_init) return;

            // Toggle
            has_init = true;
        }
        
        public virtual void Uninitialize()
        {
            // If already uninit, return
            if (has_uninit) return;

            // Toggle
            has_uninit = true;
        }

        public abstract object Get(string key);

        public abstract void Set(string key, object value);
        public abstract void Set(string key, object value, int minutes);
        public abstract void Set(string key, object value, DateTime dt);
        public abstract void Set(string key, object value, TimeSpan ts);

        public abstract void Unset(string key);

        public abstract bool Exists(string key);

        public virtual void Clear()
        {
            // Do nothing in the base
        }

        public virtual void ClearByKey(string key)
        {
            // Do nothing in the base
        }

        protected abstract string GetName();

        #endregion

        #region Properties

        public string Name
        {
            get {
                return GetName();
            }
        }

        protected bool HasInitialized
        {
            get {
                return has_init;
            }
        }

        protected bool HasUninitialized
        {
            get {
                return has_uninit;
            }
        }

        #endregion

    }

}
