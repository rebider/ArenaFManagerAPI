using System;
using WitFX.Contracts;

namespace WitFX.MT4.Server.Models
{
    public sealed class Order
    {
        public Guid TransId { get; set; }
        public int OrderId { get; set; }
        public eMT4ServerType MT4ServerIndex { get; set; }
        public int MT4Login { get; set; }
        public int MasterLogin { get; set; }
        
        /// <summary>
        /// TransId of a signal's order. Only for followed order.
        /// </summary>
        public Guid? SignalTransId { get; set; }

        /// <summary>
        /// Only for signal's order.
        /// </summary>
        public int? SignalIndex { get; set; }

        /// <summary>
        /// Only for signal's order.
        /// </summary>
        public bool? IsSSP { get; set; }

        public string Comment { get; set; }
    }
}
