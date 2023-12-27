using Awaken.Contracts.Swap;

namespace AElf.Contracts.Ewell
{
    public partial class EwellContractState
    {
       internal  AElf.Contracts.Whitelist.WhitelistContractContainer.WhitelistContractReferenceState WhitelistContract
       {
           get;
           set;
       }

       internal Awaken.Contracts.Swap.AwakenSwapContractContainer.AwakenSwapContractReferenceState SwapContract
       {
           get;
           set;
       }
        internal AElf.Contracts.MultiToken.TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
    }
}