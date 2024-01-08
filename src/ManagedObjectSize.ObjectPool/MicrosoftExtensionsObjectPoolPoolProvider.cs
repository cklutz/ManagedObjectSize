using ManagedObjectSize.Pooling;
using Microsoft.Extensions.ObjectPool;

namespace ManagedObjectSize.ObjectPool
{
    /// <summary>
    /// Adapts a <see cref="Microsoft.Extensions.ObjectPool.ObjectPool"/> to be used
    /// as <see cref="ObjectSizeOptions.PoolProvider"/>.
    /// </summary>
    public class MicrosoftExtensionsObjectPoolPoolProvider : PoolProvider
    {
        private class PolicyAdapter<T> : IPooledObjectPolicy<T> where T : notnull
        {
            private readonly IPoolPolicy<T> m_policy;
            public PolicyAdapter(IPoolPolicy<T> policy) => m_policy = policy;
            public T Create() => m_policy.Create();
            public bool Return(T obj) => m_policy.Return(obj);
        }

        private class PoolAdapter<T> : Pool<T> where T : class
        {
            private readonly ObjectPool<T> m_pool;
            public PoolAdapter(ObjectPool<T> pool) => m_pool = pool;
            public override T Get() => m_pool.Get();
            public override void Return(T obj) => m_pool.Return(obj);
        }

        private readonly ObjectPoolProvider m_provider;

        public MicrosoftExtensionsObjectPoolPoolProvider()
            : this(new DefaultObjectPoolProvider())
        {
        }

        public MicrosoftExtensionsObjectPoolPoolProvider(ObjectPoolProvider objectPoolProvider)
        {
            m_provider = objectPoolProvider ?? throw new ArgumentNullException(nameof(objectPoolProvider));
        }

        public override Pool<T> Create<T>(IPoolPolicy<T> policy)
        {
            return new PoolAdapter<T>(m_provider.Create(new PolicyAdapter<T>(policy)));
        }
    }
}
