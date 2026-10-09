using System.Threading;
using System.Threading.Tasks;

namespace SOFT2027.Core.Interfaces;

public interface IInventoryCoordinator
{
    Task AdjustStockAsync(int productId, decimal quantityChange, int adminId, string reason, CancellationToken cancellationToken = default);
}
