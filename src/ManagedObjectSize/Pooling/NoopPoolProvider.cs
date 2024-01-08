namespace ManagedObjectSize.Pooling
{
    public class NoopPoolProvider : PoolProvider
    {
        public override Pool<T> Create<T>(IPoolPolicy<T> policy) => new NoopPool<T>(policy);

        private class NoopPool<T> : Pool<T> where T : class
        {
            private readonly IPoolPolicy<T> m_policy;
            public NoopPool(IPoolPolicy<T> policy) => m_policy = policy;
            public override T Get() => m_policy.Create();
            public override void Return(T value) => m_policy.Return(value);
        }
    }
}
