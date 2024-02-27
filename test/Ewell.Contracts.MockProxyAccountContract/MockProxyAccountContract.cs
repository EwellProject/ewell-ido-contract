using AElf.Contracts.MultiToken;
using AElf.Sdk.CSharp;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace Ewell.Contracts.MockProxyAccountContract
{
    /// <summary>
    /// The C# implementation of the contract defined in mock_proxy_account_contract.proto that is located in the "protobuf"
    /// folder.
    /// Notice that it inherits from the protobuf generated code. 
    /// </summary>
    public class MockProxyAccountContract : MockProxyAccountContractContainer.MockProxyAccountContractBase
    {
        
        public override Empty ForwardCall(ForwardCallInput input)
        {
            if (input.MethodName == "Create")
            {
                var createInput = CreateInput.Parser.ParseFrom(input.Args);
                TokenContract().Create.Send(createInput);
            }
            return new Empty();
        }

        public override ProxyAccount GetProxyAccountByProxyAccountAddress(Address input)
        {
            return new ProxyAccount
            {
                ProxyAccountHash = Hash.LoadFromByteArray(input.ToByteArray())
            };
        }
    
        private TokenContractContainer.TokenContractReferenceState TokenContract()
        {
            if (State.TokenContract.Value == null)
                State.TokenContract.Value = Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);
            return State.TokenContract;
        }
    }
}