
using AElf.Standards.ACS0;

namespace Ewell.Contracts.Ido
{
    public partial class EwellContractState
    { 
        internal ACS0Container.ACS0ReferenceState GenesisContract { get; set; }

       internal  Whitelist.WhitelistContractContainer.WhitelistContractReferenceState WhitelistContract
       {
           get;
           set;
       }
       
       internal AElf.Contracts.MultiToken.TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
       
       internal AElf.Contracts.ProxyAccountContract.ProxyAccountContractContainer.ProxyAccountContractReferenceState ProxyAccountContract
       {
           get;
           set;
       }
    }
}