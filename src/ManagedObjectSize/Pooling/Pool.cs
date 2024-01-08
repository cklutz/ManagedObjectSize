namespace ManagedObjectSize.Pooling
{
    public abstract class Pool<T> where T : class
    {
        public abstract T Get();
        public abstract void Return(T value);
    }
}
