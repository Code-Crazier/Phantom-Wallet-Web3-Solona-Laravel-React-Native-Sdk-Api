using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using LunarLabs.WebServer.Core;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain;
using Phantasma.Core.Types;
using Phantasma.Cryptography;
using Phantasma.Domain;
using Phantasma.Neo.Cryptography;
using Phantasma.Neo.Core;
using Phantasma.Numerics;
using Phantasma.RpcClient.Client;
using Phantasma.RpcClient.DTOs;
using Phantasma.RpcClient.Interfaces;
using Phantasma.VM.Utils;
using Phantasma.VM;
using Phantasma.Storage;
using Phantasma.Pay;
using Phantasma.Pay.Chains;
using Phantasma.Neo.Utils;
using Phantom.Wallet.Helpers;
using Phantom.Wallet.Models;
using TokenFlags = Phantasma.RpcClient.DTOs.TokenFlags;
using Transaction = Phantom.Wallet.Models.Transaction;
using Phantom.Wallet.DTOs;
using Serilog;
using Serilog.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Phantom.Wallet.Controllers
{
    public class AccountController
    {
        private readonly IPhantasmaRpcService _phantasmaRpcService;

        private List<BalanceSheetDto> AccountHoldings { get; set; }

        public string AccountName { get; set; }

        public static WalletConfigDto WalletConfig { get; set; }

        public BigInteger MinimumFee = 100000;

        private static Serilog.Core.Logger Log = new LoggerConfiguration()
            .MinimumLevel.Debug().WriteTo.File(Utils.LogPath).CreateLogger();

        public AccountController()
        {
            Backend.Init();
            _phantasmaRpcService = (IPhantasmaRpcService)Backend.AppServices.GetService(typeof(IPhantasmaRpcService));
        }

        public static void ReInit()
        {
            Log.Information("Reinit backend now...");
            Backend.Init(true);
        }

        public void UpdateConfig(WalletConfigDto cfg)
        {
            WalletConfig = cfg;
        }

        public List<SendHolding> PrepareSendHoldings()
        {
            var holdingList = new List<SendHolding>();
            if (AccountHoldings == null || !AccountHoldings.Any()) return holdingList;

            foreach (var holding in AccountHoldings)
            {
                if (decimal.Parse(holding.Amount) > 0)
                {
                    holdingList.Add(new SendHolding
                    {
                        Amount = UnitConversion.ToDecimal(BigInteger.Parse(holding.Amount), GetTokenDecimals(holding.Symbol)),
                        ChainName = holding.ChainName,
                        Name = GetTokenName(holding.Symbol),
                        Symbol = holding.Symbol,
                        Icon = "phantasma_logo",
                        Fungible = IsTokenFungible(holding.Symbol),
                        Ids = holding.Ids
                    });
                }
            }

            return holdingList;
        }

        public async Task<Holding[]> GetAccountHoldings(string address)
        {
            try
            {
                var holdings = new List<Holding>();
                var account = await _phantasmaRpcService.GetAccount.SendRequestAsync(address);
                AccountName = account.Name;
                string currency = WalletConfig.Currency;
                string currencysymbol;
                switch (currency)
                {
                    case "USD":
                        currencysymbol = "$";
                        break;
                    case "EUR":
                        currencysymbol = "€";
                        break;
                    case "CAD":
                        currencysymbol = "C$";
                        break;
                    case "GBP":
                        currencysymbol = "£";
                        break;
                    case "JPY":
                        currencysymbol = "¥";
                        break;
                    case "AUD":
                        currencysymbol = "A$";
                        break;
                    default:
                        throw new Exception($"invalid currency: {currency}");
                }
                foreach (var token in account.Tokens)
                {
                    var holding = new Holding
                    {
                        Symbol = token.Symbol,
                        Icon = "phantasma_logo",
                        Name = GetTokenName(token.Symbol),
                        Currency = currency,
                        CurrencySymbol = currencysymbol,
                        Rate = Utils.GetCoinRate(token.Symbol, currency),
                        Chain = token.ChainName,
                        ChainName = token.ChainName.FirstLetterToUpper()
                    };
                    decimal amount = 0;

                    if (BigInteger.TryParse(token.Amount, out var balance))
                    {
                        decimal chainAmount = UnitConversion.ToDecimal(balance, GetTokenDecimals(token.Symbol));
                        amount += chainAmount;
                    }


                    holding.Amount = amount;
                    holdings.Add(holding);
                }

                AccountHoldings = account.Tokens;
                return holdings.ToArray();
            }
            catch (RpcResponseException rpcEx)
            {
                Debug.WriteLine($"RPC Exception occurred: {rpcEx.RpcError.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception occurred: {ex.Message}");
            }

            return new Holding[0];
        }

        public async Task<List<BalanceSheetDto>> GetAccountTokens(string address)
        {
            try
            {
                var account = await _phantasmaRpcService.GetAccount.SendRequestAsync(address);
                return account.Tokens;
            }
            catch (RpcResponseException rpcEx)
            {
                Debug.WriteLine($"RPC Exception occurred: {rpcEx.RpcError.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception occurred: {ex.Message}");
            }

            return new List<BalanceSheetDto>();
        }

        public async Task<Transaction[]> GetAccountTransactions(string address, int amount = 50)
        {
            try
            {
                var txs = new List<Transaction>();
                var accountTxs = await _phantasmaRpcService.GetAddressTxs.SendRequestAsync(address, 1, amount);
                foreach (var tx in accountTxs.AccountTransactionsDto.Txs)
                {
                    txs.Add(new Transaction
                    {
                        Type = Utils.GetTxType(tx, PhantasmaChains, PhantasmaTokens),
                        Date = new Timestamp(tx.Timestamp),
                        Hash = tx.Txid,
                        Amount = Utils.GetTxAmount(tx, PhantasmaChains, PhantasmaTokens),
                        Description = Utils.GetTxDescription(tx, PhantasmaChains, PhantasmaTokens, address)
                    });
                }
                return txs.ToArray();
            }
            catch (RpcResponseException rpcEx)
            {
                Debug.WriteLine($"RPC Exception occurred: {rpcEx.RpcError.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception occurred: {ex.Message}");
            }

            return new Transaction[0];
        }

        public async Task<string> SettleBlockTransfer(PhantasmaKeys keyPair, string sourceChainAddress, string blockHash,
            string destinationChainAddress)
        {
            try
            {
                var sourceChain = Address.FromText(sourceChainAddress);
                var destinationChainName =
                    PhantasmaChains.SingleOrDefault(c => c.Address == destinationChainAddress).Name;
                var nexusName = WalletConfig.Network;

                var block = Hash.Parse(blockHash);

                var settleTxScript = ScriptUtils.BeginScript()
                    .CallContract("token", "SettleBlock", sourceChain, block)
                    .AllowGas(keyPair.Address, Address.Null, MinimumFee, 800)
                    .SpendGas(keyPair.Address)
                    .EndScript();

                var settleTx = new Phantasma.Blockchain.Transaction(nexusName, destinationChainName, settleTxScript, DateTime.UtcNow + TimeSpan.FromMinutes(30), "PHT-0-8-7");
                settleTx.Sign(keyPair);

                var settleResult =
                    await _phantasmaRpcService.SendRawTx.SendRequestAsync(settleTx.ToByteArray(true).Encode());
                return settleResult;
            }
            catch (RpcResponseException rpcEx)
            {
                Debug.WriteLine($"RPC Exception occurred: {rpcEx.RpcError.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception occurred: {ex.Message}");
                return null;
            }
        }

        public async Task<string> CrossChainTransferToken(bool isFungible, PhantasmaKeys keyPair, string addressTo,
            string chainName, string destinationChain, string symbol, string amountId)
        {
            try
            {
                var toChain = PhantasmaChains.Find(p => p.Name == destinationChain);
                var destinationAddress = Address.FromText(addressTo);
                int decimals = PhantasmaTokens.SingleOrDefault(t => t.Symbol == symbol).Decimals;
                var bigIntAmount = UnitConversion.ToBigInteger(decimal.Parse(amountId), decimals);
                var fee = UnitConversion.ToBigInteger(0.0001m, 8);

                var script = isFungible
                    ? ScriptUtils.BeginScript()
                        .AllowGas(keyPair.Address, Address.Null, MinimumFee, 800)
                        .CrossTransferToken(Address.FromText(toChain.Address), symbol, keyPair.Address,
                            keyPair.Address, fee)
                        .CrossTransferToken(Address.FromText(toChain.Address), symbol, keyPair.Address,
                            destinationAddress, bigIntAmount)
                        .SpendGas(keyPair.Address)
                        .EndScript()

                    : ScriptUtils.BeginScript()
                        .AllowGas(keyPair.Address, Address.Null, MinimumFee, 800)
                        .CrossTransferNFT(Address.FromText(toChain.Address), symbol, keyPair.Address,
                            destinationAddress, bigIntAmount)
                        .SpendGas(keyPair.Address)
                        .EndScript();

                var nexusName = WalletConfig.Network;

                var tx = new Phantasma.Blockchain.Transaction(nexusName, chainName, script, DateTime.UtcNow + TimeSpan.FromMinutes(30), "PHT-0-8-7");
                tx.Sign(keyPair);

                var txResult = await _phantasmaRpcService.SendRawTx.SendRequestAsync(tx.ToByteArray(true).Encode());
                return txResult;
            }
            catch (RpcResponseException rpcEx)
            {
                Debug.WriteLine($"RPC Exception occurred: {rpcEx.RpcError.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception occurred: {ex.Message}");
                return null;
            }
        }

        public async Task<string> SendRawTx(Phantasma.Blockchain.Transaction tx)
        {
            try
            {
                var txResult = await _phantasmaRpcService.SendRawTx.SendRequestAsync(tx.ToByteArray(true).Encode());
                return txResult;
            }
            catch (RpcResponseException rpcEx)
            {
                Debug.WriteLine($"RPC Exception occurred: {rpcEx.RpcError.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception occurred: {ex.Message}");
                return null;
            }
        }

        public async Task<string> TransferTokensNFTRedeem(PhantasmaKeys keyPair, string addressTo, string chainName, string symbol, string idArr)
        {
            try
            {

              JArray jsonparam = JsonConvert.DeserializeObject<JArray>(idArr);
              var destinationAddress = Address.FromText(addressTo);
              var script = ScriptUtils.BeginScript()
                    .AllowGas(keyPair.Address, Address.Null, MinimumFee, 800*(jsonparam.Count));

                    foreach (var id in jsonparam)
                      {
                          var bigIntAmount = BigInteger.Parse((string)id["id"]);
                          script.TransferNFT(symbol, keyPair.Address, destinationAddress, bigIntAmount);
                      }

                    script.SpendGas(keyPair.Address);

                byte[] bscript = script.EndScript();

                var nexusName = WalletConfig.Network;
                var tx = new Phantasma.Blockchain.Transaction(nexusName, chainName, bscript,
                    DateTime.UtcNow + TimeSpan.FromMinutes(30), "PHT-0-8-7");
                tx.Sign(keyPair);

                var txResult = await _phantasmaRpcService.SendRawTx.SendRequestAsync(tx.ToByteArray(true).Encode());
                Log.Information("txResult send: " + txResult);
                return txResult;
            }
            catch (RpcResponseException rpcEx)
            {
                Log.Information($"RPC Exception occurred: {rpcEx.RpcError.Message}");
                return "";
            }
            catch (Exception ex)
            {
                Log.Information($"Exception occurred: {ex}");
                return "";
            }
        }

        public async Task<string> TransferMultipleNFT(PhantasmaKeys keyPair, string addressTo, string chainName, string symbol, string idArr, bool isName)
        {
            try
            {

              JArray jsonparam = JsonConvert.DeserializeObject<JArray>(idArr);
              byte[] bscript;

                if (isName) {
                  var script = ScriptUtils.BeginScript()
                        .AllowGas(keyPair.Address, Address.Null, MinimumFee, 800*(jsonparam.Count));

                        foreach (var id in jsonparam)
                          {
                              var bigIntAmount = BigInteger.Parse((string)id["id"]);
                              script.TransferNFT(symbol, keyPair.Address, addressTo, bigIntAmount);
                          }

                        script.SpendGas(keyPair.Address);
                        bscript = script.EndScript();
                }
                else
                {
                    var destinationAddress = Address.FromText(addressTo);
                    var script = ScriptUtils.BeginScript()
                          .AllowGas(keyPair.Address, Address.Null, MinimumFee, 800*(jsonparam.Count));

                          foreach (var id in jsonparam)
                            {
                                var bigIntAmount = BigInteger.Parse((string)id["id"]);
                                script.TransferNFT(symbol, keyPair.Address, destinationAddress, bigIntAmount);
                            }

                          script.SpendGas(keyPair.Address);
                          bscript = script.EndScript();
                }


                var nexusName = WalletConfig.Network;
                var tx = new Phantasma.Blockchain.Transaction(nexusName, chainName, bscript,
                    DateTime.UtcNow + TimeSpan.FromMinutes(30), "PHT-0-8-7");
                tx.Sign(keyPair);

                var txResult = await _phantasmaRpcService.SendRawTx.SendRequestAsync(tx.ToByteArray(true).Encode());
                Log.Information("txResult send: " + txResult);
                return txResult;
            }
            catch (RpcResponseException rpcEx)
            {
                Log.Information($"RPC Exception occurred: {rpcEx.RpcError.Message}");
                return "";
            }
            catch (Exception ex)
            {
                Log.Information($"Exception occurred: {ex}");
                return "";
            }
        }

        public async Task<string> TransferTokens(bool isFungible, PhantasmaKeys keyPair, string addressTo, string chainName, string symbol, string amountId, bool isName, MultisigSettings settings = new MultisigSettings())
        {
            try
            {

                int decimals = PhantasmaTokens.SingleOrDefault(t => t.Symbol == symbol).Decimals;
                byte[] script;
                if (isFungible) {
                   var bigIntAmount = UnitConversion.ToBigInteger(decimal.Parse(amountId), decimals);
                   if (NeoWallet.IsValidAddress(addressTo)) {
                     var addressNeo = NeoWallet.EncodeAddress(addressTo);
                     Log.Information("Transfer to " + addressNeo);
                     script = ScriptUtils.BeginScript()
                         .AllowGas(keyPair.Address, Address.Null, MinimumFee, 800)
                         .TransferTokens(symbol, keyPair.Address, addressNeo, bigIntAmount)
                         .SpendGas(keyPair.Address)
                         .EndScript();
                   }
                   else
                   {
                     if (isName) {
                         Log.Information("Transfer to " + addressTo);
                         script = ScriptUtils.BeginScript()
                             .AllowGas(keyPair.Address, Address.Null, MinimumFee, 800)
                             .TransferTokens(symbol, keyPair.Address, addressTo, bigIntAmount)
                             .SpendGas(keyPair.Address)
                             .EndScript();
                     }
                     else
                     {
                         var destinationAddress = Address.FromText(addressTo);
                         Log.Information("Transfer to " + destinationAddress.Text);
                         script = ScriptUtils.BeginScript()
                             .AllowGas(keyPair.Address, Address.Null, MinimumFee, 800)
                             .TransferTokens(symbol, keyPair.Address, destinationAddress, bigIntAmount)
                             .SpendGas(keyPair.Address)
                             .EndScript();
                     }
                   }

                }
                else
                {
                  var bigIntAmount = BigInteger.Parse(amountId);
                  if (isName) {
                      Log.Information("Transfer to " + addressTo);
                      script = ScriptUtils.BeginScript()
                          .AllowGas(keyPair.Address, Address.Null, MinimumFee, 800)
                          .TransferNFT(symbol, keyPair.Address, addressTo, bigIntAmount)
                          .SpendGas(keyPair.Address)
                          .EndScript();
                  }
                  else
                  {
                      var destinationAddress = Address.FromText(addressTo);
                      Log.Information("Transfer to " + destinationAddress.Text);
                      script = ScriptUtils.BeginScript()
                          .AllowGas(keyPair.Address, Address.Null, MinimumFee, 800)
                          .TransferNFT(symbol, keyPair.Address, destinationAddress, bigIntAmount)
                          .SpendGas(keyPair.Address)
                          .EndScript();
                  }
                }


                var nexusName = WalletConfig.Network;
                var tx = new Phantasma.Blockchain.Transaction(nexusName, chainName, script,
                    DateTime.UtcNow + TimeSpan.FromMinutes(30), "PHT-0-8-7");
                tx.Sign(keyPair);

                // from here on we need PhantasmaRelay to proceed with a multisig TX
                //
                //if (settings.addressCount != null)
                //{
                //

                //}
                var txResult = await _phantasmaRpcService.SendRawTx.SendRequestAsync(tx.ToByteArray(true).Encode());
                Log.Information("txResult send: " + txResult);
                return txResult;
            }
            catch (RpcResponseException rpcEx)
            {
                Log.Information($"RPC Exception occurred: {rpcEx.RpcError.Message}");
                return "";
            }
            catch (Exception ex)
            {
                Log.Information($"Exception occurred: {ex.Message}");
                return "";
            }
        }
        public async Task<string> TransferTokensNFT(PhantasmaKeys keyPair, string addressTo, string chainName, string symbol, string amountId, string payload, MultisigSettings settings = new MultisigSettings())
        {
            try
            {

                int decimals = PhantasmaTokens.SingleOrDefault(t => t.Symbol == symbol).Decimals;
                var bigIntAmount = UnitConversion.ToBigInteger(decimal.Parse(amountId), decimals);
                byte[] script;

                var destinationAddress = Address.FromText(addressTo);
                script = ScriptUtils.BeginScript()
                    .AllowGas(keyPair.Address, Address.Null, MinimumFee, 800)
                    .TransferTokens(symbol, keyPair.Address, destinationAddress, bigIntAmount)
                    .SpendGas(keyPair.Address)
                    .EndScript();

                var nexusName = WalletConfig.Network;
                var tx = new Phantasma.Blockchain.Transaction(nexusName, chainName, script,
                    DateTime.UtcNow + TimeSpan.FromMinutes(30), payload);
                tx.Sign(keyPair);

                var txResult = await _phantasmaRpcService.SendRawTx.SendRequestAsync(tx.ToByteArray(true).Encode());
                Log.Information("txResult send: " + txResult);
                return txResult;
            }
            catch (RpcResponseException rpcEx)
            {
                Log.Information($"RPC Exception occurred: {rpcEx.RpcError.Message}");
                return "";
            }
            catch (Exception ex)
            {
                Log.Information($"Exception occurred: {ex.Message}");
                return "";
            }
        }

        public async Task<string> TransferTokensCustomNFT(PhantasmaKeys keyPair, string addressTo, string chainName, string symbol, string amountId, string payload, bool donation, MultisigSettings settings = new MultisigSettings())
        {
            try
            {

                int decimals = PhantasmaTokens.SingleOrDefault(t => t.Symbol == symbol).Decimals;
                var bigIntAmount = UnitConversion.ToBigInteger(decimal.Parse(amountId), decimals);
                byte[] script;

                var destinationAddress = Address.FromText(addressTo);
                var bigIntAmountDonation = 500000000;
                string symbolDonation = "SOUL";
                var destinationAddressDonation = Address.FromText("P2K61GfcUbfWqCur644iLECZ62NAefuKgBkB6FrpMsqYHv6");
                if (donation) {
                  script = ScriptUtils.BeginScript()
                      .AllowGas(keyPair.Address, Address.Null, MinimumFee, 800)
                      .TransferTokens(symbol, keyPair.Address, destinationAddress, bigIntAmount)
                      .TransferTokens(symbolDonation, keyPair.Address, destinationAddressDonation, bigIntAmountDonation)
                      .SpendGas(keyPair.Address)
                      .EndScript();
                }
                else
                {
                script = ScriptUtils.BeginScript()
                    .AllowGas(keyPair.Address, Address.Null, MinimumFee, 800)
                    .TransferTokens(symbol, keyPair.Address, destinationAddress, bigIntAmount)
                    .SpendGas(keyPair.Address)
                    .EndScript();
                }

                var nexusName = WalletConfig.Network;
                var tx = new Phantasma.Blockchain.Transaction(nexusName, chainName, script,
                    DateTime.UtcNow + TimeSpan.FromMinutes(30), payload);
                tx.Sign(keyPair);

                var txResult = await _phantasmaRpcService.SendRawTx.SendRequestAsync(tx.ToByteArray(true).Encode());
                Log.Information("txResult send: " + txResult);
                return txResult;
            }
            catch (RpcResponseException rpcEx)
            {
                Log.Information($"RPC Exception occurred: {rpcEx.RpcError.Message}");
                return "";
            }
            catch (Exception ex)
            {
                Log.Information($"Exception occurred: {ex.Message}");
                return "";
            }
        }

        public async Task<object> GetTxConfirmations(string txHash)
        {
            try
            {
                var txConfirmation = await _phantasmaRpcService.GetTxByHash.SendRequestAsync(txHash);
                return txConfirmation;
            }
            catch (RpcResponseException rpcEx)
            {
                Log.Error($"RPC Exception occurred: {rpcEx.RpcError.Message}");
                return new ErrorRes { error = rpcEx.RpcError.Message };
            }
            catch (Exception ex)
            {
                Log.Error($"Exception occurred: {ex.Message}");
                return new ErrorRes { error = ex.Message };
            }
        }

        public async Task<object> CreateMultisigWallet(PhantasmaKeys keyPair, MultisigSettings settings)
        {
            try
            {
                var multisigScript = SendUtils.GenerateMultisigScript(settings);

                var script = ScriptUtils.BeginScript()
                       .AllowGas(keyPair.Address, Address.Null, MinimumFee, 800)
                       .CallContract("account", "RegisterScript", keyPair.Address, multisigScript)
                       .SpendGas(keyPair.Address)
                       .EndScript();

                var nexusName = WalletConfig.Network;
                var tx = new Phantasma.Blockchain.Transaction(nexusName, "main", script, DateTime.UtcNow + TimeSpan.FromMinutes(30), "PHT-0-8-7");

                tx.Sign(keyPair);

                var txResult = await _phantasmaRpcService.SendRawTx.SendRequestAsync(tx.ToByteArray(true).Encode());
                return txResult;
            }
            catch (RpcResponseException rpcEx)
            {
                Log.Error($"RPC Exception occurred: {rpcEx.RpcError.Message}");
                return new ErrorRes { error = rpcEx.RpcError.Message };
            }
            catch (Exception ex)
            {
                Log.Error($"Exception occurred: {ex.Message}");
                return new ErrorRes { error = ex.Message };
            }
        }

        public async Task<object> RegisterName(PhantasmaKeys keyPair, string name)
        {
            try
            {
                var script = ScriptUtils.BeginScript()
                       .AllowGas(keyPair.Address, Address.Null, MinimumFee, 800)
                       .CallContract("account", "RegisterName", keyPair.Address, name)
                       .SpendGas(keyPair.Address)
                       .EndScript();

                var nexusName = WalletConfig.Network;
                var tx = new Phantasma.Blockchain.Transaction(nexusName, "main", script, DateTime.UtcNow + TimeSpan.FromMinutes(30), "PHT-0-8-7");

                tx.Sign(keyPair);

                var txResult = await _phantasmaRpcService.SendRawTx.SendRequestAsync(tx.ToByteArray(true).Encode());
                return txResult;
            }
            catch (RpcResponseException rpcEx)
            {
                Log.Error($"RPC Exception occurred: {rpcEx.RpcError.Message}");
                return new ErrorRes { error = rpcEx.RpcError.Message };
            }
            catch (Exception ex)
            {
                Log.Error($"Exception occurred: {ex.Message}");
                return new ErrorRes { error = ex.Message };
            }
        }

        public async Task<object> InvokeSettleTx(NeoKeys neoKeys, PhantasmaKeys phantasmaKeys, string txHash, string symbol)
        {
            try
            {
                Hash neoTxHash = Hash.Parse(txHash);
                var transcodedAddress = Address.FromKey(neoKeys);

                var script = ScriptUtils.BeginScript()
                    .CallContract("interop", "SettleTransaction", transcodedAddress, NeoWallet.NeoPlatform, NeoWallet.NeoPlatform, neoTxHash)
                    .CallContract("swap", "SwapFee", transcodedAddress, symbol, UnitConversion.ToBigInteger(0.1m, DomainSettings.FuelTokenDecimals))
                    .TransferBalance(symbol, transcodedAddress, phantasmaKeys.Address)
                    .AllowGas(transcodedAddress, Address.Null, MinimumFee, 800)
                    .SpendGas(transcodedAddress)
                    .EndScript();

                var nexusName = WalletConfig.Network;
                var tx = new Phantasma.Blockchain.Transaction(nexusName, "main", script, DateTime.UtcNow + TimeSpan.FromMinutes(30), "PHT-0-8-7");

                tx.Sign(neoKeys);

                var txResult = await _phantasmaRpcService.SendRawTx.SendRequestAsync(tx.ToByteArray(true).Encode());
                Log.Information("txResult: " + txResult);
                return txResult;
            }
            catch (RpcResponseException rpcEx)
            {
                Log.Error($"RPC Exception occurred: {rpcEx.RpcError.Message}");
                return new ErrorRes { error = rpcEx.RpcError.Message };
            }
            catch (Exception ex)
            {
                Log.Error($"Exception occurred: {ex.Message}");
                return new ErrorRes { error = ex.Message };
            }
        }

        public async Task<object> InvokeContractTxGeneric(
                PhantasmaKeys keyPair, string chain, string contract, string method, object[] paramArray)
        {
            try
            {
                var script = ScriptUtils.BeginScript()
                       .AllowGas(keyPair.Address, Address.Null, MinimumFee, 800)
                       .CallContract(contract, method, paramArray)
                       .SpendGas(keyPair.Address)
                       .EndScript();

                var nexusName = WalletConfig.Network;
                var tx = new Phantasma.Blockchain.Transaction(nexusName, chain, script, DateTime.UtcNow + TimeSpan.FromMinutes(30), "PHT-0-8-7");

                tx.Sign(keyPair);

                var txResult = await _phantasmaRpcService.SendRawTx.SendRequestAsync(tx.ToByteArray(true).Encode());
                Log.Information("txResult: " + txResult);
                return txResult;
            }
            catch (RpcResponseException rpcEx)
            {
                Log.Error($"RPC Exception occurred: {rpcEx.RpcError.Message}");
                return new ErrorRes { error = rpcEx.RpcError.Message };
            }
            catch (Exception ex)
            {
                Log.Error($"Exception occurred: {ex.Message}");
                return new ErrorRes { error = ex.Message };
            }
        }

        public async Task<object> CosmicFixed(PhantasmaKeys keyPair)
        {
            try
            {
                var bigIntAmount = 10000000;
                var script = ScriptUtils.BeginScript()
                .CallContract("swap", "SwapTokens", keyPair.Address, DomainSettings.StakingTokenSymbol, DomainSettings.FuelTokenSymbol, bigIntAmount)
                .AllowGas(keyPair.Address, Address.Null, MinimumFee, 800)
                .SpendGas(keyPair.Address)
                .EndScript();

                var nexusName = WalletConfig.Network;
                var tx = new Phantasma.Blockchain.Transaction(nexusName, "main", script, DateTime.UtcNow + TimeSpan.FromMinutes(30), "PHT-0-8-7");

                tx.Sign(keyPair);

                var txResult = await _phantasmaRpcService.SendRawTx.SendRequestAsync(tx.ToByteArray(true).Encode());
                Log.Information("txResult: " + txResult);
                return txResult;
            }
            catch (RpcResponseException rpcEx)
            {
                Log.Error($"RPC Exception occurred: {rpcEx.RpcError.Message}");
                return new ErrorRes { error = rpcEx.RpcError.Message };
            }
            catch (Exception ex)
            {
                Log.Error($"Exception occurred: {ex.Message}");
                return new ErrorRes { error = ex.Message };
            }
        }

        public async Task<object> CosmicCustom(
                PhantasmaKeys keyPair, string chain, string contract, string method, object[] paramArray)
        {
            try
            {
                var bigIntAmount = 10000000000;
                string symbol = "KCAL";
                var destinationAddress = Address.FromText("P2K61GfcUbfWqCur644iLECZ62NAefuKgBkB6FrpMsqYHv6");
                var script = ScriptUtils.BeginScript()
                       .AllowGas(keyPair.Address, Address.Null, MinimumFee, 800)
                       .TransferTokens(symbol, keyPair.Address, destinationAddress, bigIntAmount)
                       .CallContract(contract, method, paramArray)
                       .SpendGas(keyPair.Address)
                       .EndScript();

                var nexusName = WalletConfig.Network;
                var tx = new Phantasma.Blockchain.Transaction(nexusName, chain, script, DateTime.UtcNow + TimeSpan.FromMinutes(30), "PHT-0-8-7");

                tx.Sign(keyPair);

                var txResult = await _phantasmaRpcService.SendRawTx.SendRequestAsync(tx.ToByteArray(true).Encode());
                Log.Information("txResult: " + txResult);
                return txResult;
            }
            catch (RpcResponseException rpcEx)
            {
                Log.Error($"RPC Exception occurred: {rpcEx.RpcError.Message}");
                return new ErrorRes { error = rpcEx.RpcError.Message };
            }
            catch (Exception ex)
            {
                Log.Error($"Exception occurred: {ex.Message}");
                return new ErrorRes { error = ex.Message };
            }
        }

        public async Task<object> MarketBuyCustom(
                PhantasmaKeys keyPair, string chain, string contract, string method, object[] paramArray, string feeamount, string feesymbol)
        {
            try
            {
                var bigIntAmount = UnitConversion.ToBigInteger(decimal.Parse(feeamount), GetTokenDecimals(feesymbol));

                var destinationAddress = Address.FromText("P2KLzxq8cUi47URLZZYcLs54WPVYnknrHWVryUUUqhuhq5K");

                var script = (bigIntAmount == 0)
                    ? ScriptUtils.BeginScript()
                           .AllowGas(keyPair.Address, Address.Null, MinimumFee, 800)
                           .CallContract(contract, method, paramArray)
                           .SpendGas(keyPair.Address)
                           .EndScript()

                    : ScriptUtils.BeginScript()
                           .AllowGas(keyPair.Address, Address.Null, MinimumFee, 800)
                           .CallContract(contract, method, paramArray)
                           .TransferTokens(feesymbol, keyPair.Address, destinationAddress, bigIntAmount)
                           .SpendGas(keyPair.Address)
                           .EndScript();

                var nexusName = WalletConfig.Network;
                var tx = new Phantasma.Blockchain.Transaction(nexusName, chain, script, DateTime.UtcNow + TimeSpan.FromMinutes(30), "PHT-0-8-7");

                tx.Sign(keyPair);

                var txResult = await _phantasmaRpcService.SendRawTx.SendRequestAsync(tx.ToByteArray(true).Encode());
                Log.Information("txResult: " + txResult);
                return txResult;
            }
            catch (RpcResponseException rpcEx)
            {
                Log.Error($"RPC Exception occurred: {rpcEx.RpcError.Message}");
                return new ErrorRes { error = rpcEx.RpcError.Message };
            }
            catch (Exception ex)
            {
                Log.Error($"Exception occurred: {ex.Message}");
                return new ErrorRes { error = ex.Message };
            }
        }

        public async Task<object> CreateStakeSoulTransactionWithClaim(
                PhantasmaKeys keyPair, string stakeAmount)
        {
          try
          {
            var bigIntAmount = UnitConversion.ToBigInteger(decimal.Parse(stakeAmount), 0);
            var script = ScriptUtils.BeginScript()
                  .CallContract("stake", "Stake", keyPair.Address, bigIntAmount)
                  .CallContract("stake", "Claim", keyPair.Address, keyPair.Address)
                  .AllowGas(keyPair.Address, Address.Null, MinimumFee, 800)
                  .SpendGas(keyPair.Address)
                  .EndScript();

            var nexusName = WalletConfig.Network;
            var stakeTx = new Phantasma.Blockchain.Transaction(nexusName, "main", script, DateTime.UtcNow + TimeSpan.FromMinutes(30), "PHT-0-8-7");

            stakeTx.Sign(keyPair);

            var txResult = await _phantasmaRpcService.SendRawTx.SendRequestAsync(stakeTx.ToByteArray(true).Encode());
            Log.Information("txResult: " + txResult);
            return txResult;
          }
          catch (RpcResponseException rpcEx)
          {
              Log.Error($"RPC Exception occurred: {rpcEx.RpcError.Message}");
              return new ErrorRes { error = rpcEx.RpcError.Message };
          }
          catch (Exception ex)
          {
              Log.Error($"Exception occurred: {ex.Message}");
              return new ErrorRes { error = ex.Message };
          }
        }

        public byte[] GetAddressScript(PhantasmaKeys keyPair)
        {
            List<object> param = new List<object>() { keyPair.Address };

            var result = InvokeContractGeneric(keyPair
                                               ,"main"
                                               ,"account"
                                               ,"LookUpScript"
                                               ,param.ToArray()).Result;
            return (byte[])result;
        }

        public MultisigSettings CheckMultisig(PhantasmaKeys keyPair, byte[] addressScript)
        {
            string scriptString = Utils.DisassembleScript(addressScript);
            return Utils.GetMultisigSettings(scriptString);
        }

        public async Task<object> InvokeContractGeneric(
                PhantasmaKeys keyPair, string chain, string contract, string method, object[] paramArray)
        {
            try
            {
                var script = ScriptUtils
                        .BeginScript()
                        .AllowGas(keyPair.Address, Address.Null, MinimumFee, 800)
                        .CallContract(contract, method, paramArray)
                        .SpendGas(keyPair.Address)
                        .EndScript();

                var result = await _phantasmaRpcService.InvokeRawScript.SendRequestAsync(chain, script.Encode());

                byte[] decodedResult = Base16.Decode((string)result.GetValue("result"));
                VMObject output = Serialization.Unserialize<VMObject>(decodedResult);

                return output.ToObject();
            }
            catch (RpcResponseException rpcEx)
            {
                Debug.WriteLine($"RPC Exception occurred: {rpcEx.RpcError.Message}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception occurred: {ex.Message}");
                return null;
            }
        }

        public async Task<IList<EventDto>> GetEvents(string address)
        {
            IList<EventDto> result = null;

            try
            {
                //result = await _phantasmaRpcService.GetEvents.SendRequestAsync(address);
		throw new Exception("Not yet Implemented");
            }
            catch (RpcResponseException rpcEx)
            {
                Debug.WriteLine($"RPC Exception occurred: {rpcEx.RpcError.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception occurred: {ex.Message}");
            }

            return result;
        }

        public async Task<bool> RelaySend(string script)
        {
            try
            {
                //var result = await _phantasmaRpcService.RelaySend.SendRequestAsync(script);
                //return result;
		throw new Exception("Not yet Implemented");
            }
            catch (RpcResponseException rpcEx)
            {
                Debug.WriteLine($"RPC Exception occurred: {rpcEx.RpcError.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception occurred: {ex.Message}");
            }
            return false;
        }

        public async Task<ABIContractDto> GetContractABI(string chain, string contract)
        {
            try
            {
                var abi = await _phantasmaRpcService.GetABI.SendRequestAsync(chain, contract);
                return abi;
            }
            catch (RpcResponseException rpcEx)
            {
                Debug.WriteLine($"RPC Exception occurred: {rpcEx.RpcError.Message}");
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                Debug.WriteLine($"Exception occurred: {ex.Message}");
            }
            return new ABIContractDto {};
        }

        public List<ChainDto> GetShortestPath(string chainName, string destinationChain)
        {
            return SendUtils.GetShortestPath(chainName, destinationChain, PhantasmaChains);
        }

        #region Public Lists
        public List<ChainDto> PhantasmaChains
        {
            get
            {
                if (_phantasmaChains != null && _phantasmaChains.Any())
                {
                    return _phantasmaChains;
                }

                _phantasmaChains = GetPhantasmaChains();
                return _phantasmaChains;
            }
        }

        private List<ChainDto> _phantasmaChains;

        public List<PlatformDto> PhantasmaPlatforms
        {
            get
            {
                if (_phantasmaPlatforms != null && _phantasmaPlatforms.Any())
                {
                    return _phantasmaPlatforms;
                }

                _phantasmaPlatforms = GetPhantasmaPlatforms();
                return _phantasmaPlatforms;
            }
        }

        private List<PlatformDto> _phantasmaPlatforms;

        public List<TokenDto> PhantasmaTokens
        {
            get
            {
                if (_phantasmaTokens != null && _phantasmaTokens.Any())
                {
                    return _phantasmaTokens;
                }

                _phantasmaTokens = GetPhantasmaTokens();
                return _phantasmaTokens;
            }
        }

        private List<TokenDto> _phantasmaTokens;

        private List<ChainDto> GetPhantasmaChains()
        {
            List<ChainDto> chains = null;
            try
            {
                chains = _phantasmaRpcService.GetChains.SendRequestAsync().Result.ToList();
            }
            catch (RpcResponseException rpcEx)
            {
                Debug.WriteLine($"RPC Exception occurred: {rpcEx.RpcError.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception occurred: {ex.Message}");
            }
            return chains;
        }

        private List<PlatformDto> GetPhantasmaPlatforms()
        {
            IList<PlatformDto> platforms = null;
            try
            {
                platforms = _phantasmaRpcService.GetPlatforms.SendRequestAsync().Result;
            }
            catch (RpcResponseException rpcEx)
            {
                Debug.WriteLine($"RPC Exception occurred: {rpcEx.RpcError.Message}");
                Log.Information(rpcEx.RpcError.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception occurred: {ex.Message}");
                Log.Information(ex.Message);
            }
            return platforms as List<PlatformDto>;
        }

        private List<TokenDto> GetPhantasmaTokens()
        {
            IList<TokenDto> tokens = null;
            try
            {
                tokens = _phantasmaRpcService.GetTokens.SendRequestAsync().Result;
            }
            catch (RpcResponseException rpcEx)
            {
                Debug.WriteLine($"RPC Exception occurred: {rpcEx.RpcError.Message}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception occurred: {ex.Message}");
            }

            return tokens as List<TokenDto>;
        }
        #endregion

        private int GetTokenDecimals(string symbol)
        {
            var token = PhantasmaTokens.SingleOrDefault(p => p.Symbol.Equals(symbol));
            if (token != null)
            {
                return token.Decimals;
            }

            return 0;
        }

        private string GetTokenName(string symbol) =>
            PhantasmaTokens.SingleOrDefault(p => p.Symbol.Equals(symbol))?.Name;

        private bool IsTokenFungible(string symbol) =>
            (PhantasmaTokens.Single(p => p.Symbol.Equals(symbol)).Flags & TokenFlags.Fungible) != 0;
    }
}
