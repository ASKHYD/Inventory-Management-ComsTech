using System.Data;

namespace SOFT2027.Core.Interfaces
{
    public interface ISqliteConnectionFactory
    {
        /// <summary>
        /// Creates a new openable SQLite connection configured with WAL mode and foreign keys enabled.
        /// </summary>
        IDbConnection CreateConnection();
    }
}
