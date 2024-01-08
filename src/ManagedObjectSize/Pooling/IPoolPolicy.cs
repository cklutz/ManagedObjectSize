namespace ManagedObjectSize.Pooling
{
    public interface IPoolPolicy<T>
    {
        public abstract T Create();
        public abstract bool Return(T value);
    }
}
