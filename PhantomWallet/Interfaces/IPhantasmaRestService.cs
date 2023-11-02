using System.Threading.Tasks;
using Phantasma.RpcClient.DTOs;

namespace Phantom.Wallet.Interfaces
{
    public interface IPhantasmaRestService
    {
        Task<AccountDto> GetAccount(string address);
        Task<BlockDto> GetBlock(string blockHash);
        Task<AccountTransactionsDto> GetAccountTxs(string address, int amount);
    }
}
