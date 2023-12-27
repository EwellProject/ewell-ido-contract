using AElf.Sdk.CSharp.State;
using AElf.Standards.ACS1;
using AElf.Types;

namespace Awaken.Contracts.Token
{
    public class TokenContractState : ContractState
    {
        public SingletonState<Address> Owner { get; set; }

        public MappedState<Address, bool> MinterMap{ get; set; }
        public MappedState<string, TokenInfo> TokenInfoMap { get; set; }

        /// <summary>
        /// Owner -> Symbol -> Balance
        /// </summary>
        public MappedState<Address, string, long> BalanceMap { get; set; }

        /// <summary>
        /// Owner -> Spender -> Symbol -> Allowance
        /// </summary>
        public MappedState<Address, Address, string, long> AllowanceMap { get; set; }

        public MappedState<string, MethodFees> TransactionFeesMap { get; set; }
        public SingletonState<AuthorityInfo> MethodFeeController { get; set; }
    }
}