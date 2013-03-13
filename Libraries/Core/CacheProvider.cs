using System;
using System.Collections.Generic;
using System.Text;

namespace Codeology.SharpCache.Providers
{

    public interface ICacheProvider
    {

        #region Methods

        void Initialize();
        void Uninitialize();

        void Clear();
        bool Exists(string key);
        object Get(string key);
        void Set(string key, object value, DateTime dt);
        void Unset(string key);

        #endregion

        #region Properties

        Guid Id
        {
            get;
        }

        string Name
        {
            get;
        }

        #endregion

    }

    public abstract class CacheProvider : IDisposable, ICacheProvider
    {

        #region Methods

        public void Dispose()
        {
            Uninitialize();
            GC.SuppressFinalize(this);
        }

        public virtual void Initialize()
        {
            // Do nothing...
        }

        public virtual void Uninitialize()
        {
            // Do nothing...
        }

        public abstract void Clear();
        public abstract bool Exists(string key);
        public abstract object Get(string key);
        public abstract void Set(string key, object value, DateTime dt);
        public abstract void Unset(string key);

        protected abstract Guid GetId();
        protected abstract string GetName();

        #endregion

        #region Properties

        public Guid Id
        {
            get {
                return GetId();
            }
        }

        public string Name
        {
            get {
                return GetName();
            }
        }

        #endregion

    }

    public class NullCacheProvider : CacheProvider
    {

        private const string PROVIDER_ID = "{D57E2BE7-2152-46C8-885F-882B6B55E3C7}";
        private const string PROVIDER_NAME = "Null";

        #region Methods

        public override void Clear()
        {
            // Do nothing...
        }

        public override bool Exists(string key)
        {
            return false;
        }

        public override object Get(string key)
        {
            return null;
        }

        public override void Set(string key, object value, DateTime dt)
        {
            // Do nothing...
        }

        public override void Unset(string key)
        {
            // Do nothing...
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
