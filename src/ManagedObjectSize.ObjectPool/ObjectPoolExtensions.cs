using Microsoft.Extensions.ObjectPool;

namespace ManagedObjectSize.ObjectPool
{
    public static class ObjectPoolExtensions
    {
        /// <summary>
        /// Configures <see cref="ObjectSizeOptions"/> to use an object pool based on <see cref="ObjectPoolProvider"/>.
        /// </summary>
        /// <param name="options">The options instance.</param>
        /// <param name="provider">
        /// The <see cref="ObjectPoolProvider"/> to be used. If <c>null</c>, an instance of the <see cref="DefaultObjectPoolProvider"/> will be used.</param>
        /// <returns>The options instanced given as <paramref name="options"/>.</returns>
        public static ObjectSizeOptions UseMicrosoftExtensionsObjectPool(this ObjectSizeOptions options, ObjectPoolProvider? provider = null)
        {
            if (provider == null)
            {
                options.PoolProvider = new MicrosoftExtensionsObjectPoolPoolProvider();
            }
            else
            {
                options.PoolProvider = new MicrosoftExtensionsObjectPoolPoolProvider(provider);
            }

            return options;
        }
    }
}
