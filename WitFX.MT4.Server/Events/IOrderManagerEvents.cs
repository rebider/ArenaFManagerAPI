using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WitFX.MT4.Server.cls;
using WitFX.MT4.Server.Implementation;
using WitFX.MT4.Server.Models;

namespace WitFX.MT4.Server.Events
{
    public interface IOrderManagerEvents
    {
        Task OnSignalOpenedAsync(
            SocialOrderRequest request, Order mainOrder,
            IReadOnlyList<MirroredOrder> mirroredOrders, CancellationToken cancellationToken);

        Task OnOrderCompletedAsync(Order order, CancellationToken cancellationToken);
    }

    public class MirroredOrder
    {
        public Follower Follower { get; set; }
        public MT4Request Request { get; set; }
    }
}
