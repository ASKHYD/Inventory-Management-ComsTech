using System.Threading;
using System.Threading.Tasks;

namespace SOFT2027.Core.Interfaces;

public interface IDatabaseBackupService
{
    Task CreateBackupAsync(string destinationPath, CancellationToken cancellationToken = default);
    Task<string> RestoreBackupAsync(string backupPath, CancellationToken cancellationToken = default);
}
