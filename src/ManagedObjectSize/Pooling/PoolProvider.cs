namespace ManagedObjectSize.Pooling
{
    public abstract class PoolProvider
    {
        public virtual Pool<T> Create<T>() where T : class, new() => Create(new DefaultPoolPolicy<T>());
        public abstract Pool<T> Create<T>(IPoolPolicy<T> policy) where T : class;
    }

    internal class DefaultPoolPolicy<T> : IPoolPolicy<T> where T: class, new()
    {
        public T Create() => new T();
        public bool Return(T value) => true;
    }
}
