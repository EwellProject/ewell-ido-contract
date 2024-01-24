
namespace Ewell.Contracts.Ido
{
    public partial class EwellContractState
    {
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