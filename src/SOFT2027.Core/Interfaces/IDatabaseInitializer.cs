using System.Threading;
using System.Threading.Tasks;

namespace SOFT2027.Core.Interfaces
{
    public interface IDatabaseInitializer
    {
        /// <summary>
        /// Ensures the SQLite database file exists and executes the initial schema script.
        /// </summary>
        Task InitializeAsync(CancellationToken cancellationToken = default);
    }
}
