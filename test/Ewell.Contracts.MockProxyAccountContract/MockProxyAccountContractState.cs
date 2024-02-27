using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp.State;

namespace Ewell.Contracts.MockProxyAccountContract
{
    /// <summary>
    /// The state class of the contract, it inherits from the AElf.Sdk.CSharp.State.ContractState type. 
    /// </summary>
    public class MockProxyAccountContractState : ContractState
    {
        // state definitions go here.

        internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
    }
}