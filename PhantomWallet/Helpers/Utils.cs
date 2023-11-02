using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using System.Net.Http;
using LunarLabs.Parser.JSON;
using Phantasma.Blockchain.Contracts;
using TokenEventData = Phantasma.Domain.TokenEventData;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.RpcClient.DTOs;
using Phantasma.Storage;
using Phantom.Wallet.DTOs;
using Phantom.Wallet.Models;
using Newtonsoft.Json;
using Phantasma.CodeGen.Assembler;
using Phantasma.VM;
using System.Globalization;
using Serilog;
using Serilog.Core;

namespace Phantom.Wallet.Helpers
{
    public static class Utils
    {
        public static string CfgPath { get; } = Path.Combine(Environment.GetFolderPath(
                                         Environment.SpecialFolder.ApplicationData), "phantom_wallet.cfg");

        public static string LogPath { get; } = Path.Combine(Environment.GetFolderPath(
                                         Environment.SpecialFolder.ApplicationData), "phantom_wallet.log");

        private static Serilog.Core.Logger Log = new LoggerConfiguration()
           .MinimumLevel.Debug().WriteTo.File(Utils.LogPath).CreateLogger();

        public static string GetTxAmount(TransactionDto tx, List<ChainDto> phantasmaChains, List<TokenDto> phantasmaTokens)
        {
            string amountsymbol = null;

            string senderToken = null;
            Address senderChain = Address.FromText(tx.ChainAddress);
            Address senderAddress = Address.Null;

            string receiverToken = null;
            string receiverChain = "";
            Address receiverAddress = Address.Null;

            BigInteger amount = 0;

            tx.Events.Reverse();
            foreach (var evt in tx.Events) //todo move this
            {
                switch (evt.EventKind)
                {

                  case EventKind.TokenStake:
                      {
                          var data = Serialization.Unserialize<TokenEventData>(evt.Data.Decode());
                          amount = data.Value;
                          receiverAddress = Address.FromText(evt.EventAddress);
                          receiverChain = data.ChainName;
                          if (data.Symbol == "TTRS")
                          {
                            amountsymbol = $"{data.Symbol} • NFT";
                            break;
                          }
                          var amountDecimal = UnitConversion.ToDecimal(amount, phantasmaTokens.Single(p => p.Symbol == data.Symbol).Decimals);
                          if (data.Symbol != "KCAL" && data.Symbol != "NEO" && data.Symbol != "GAS")
                          {
                            amountsymbol = $"{amountDecimal.ToString("#,0.##########").ToString(new CultureInfo("en-US"))} {data.Symbol}";
                          }
                      }
                      break;

                    case EventKind.TokenClaim:
                        {
                            var data = Serialization.Unserialize<TokenEventData>(evt.Data.Decode());
                            amount = data.Value;
                            receiverAddress = Address.FromText(evt.EventAddress);
                            receiverChain = data.ChainName;
                            if (data.Symbol == "TTRS")
                            {
                              amountsymbol = $"{data.Symbol} • NFT";
                              break;
                            }
                            var amountDecimal = UnitConversion.ToDecimal(amount, phantasmaTokens.Single(p => p.Symbol == data.Symbol).Decimals);
                            if (data.Symbol != "KCAL" && data.Symbol != "NEO" && data.Symbol != "GAS")
                            {
                              amountsymbol = $"{amountDecimal.ToString("#,0.##########").ToString(new CultureInfo("en-US"))} {data.Symbol}";
                            }
                        }
                        break;

                    case EventKind.TokenSend:
                        {
                            var data = Serialization.Unserialize<TokenEventData>(evt.Data.Decode());
                            amount = data.Value;
                            senderAddress = Address.FromText(evt.EventAddress);
                            senderToken = data.Symbol;
                            if (data.Symbol == "TTRS")
                            {
                              amountsymbol = $"{data.Symbol} • NFT";
                              break;
                            }
                            var amountDecimal = UnitConversion.ToDecimal(amount, phantasmaTokens.Single(p => p.Symbol == senderToken).Decimals);
                            amountsymbol = $"{amountDecimal.ToString("#,0.##########").ToString(new CultureInfo("en-US"))} {senderToken}";
                        }
                        break;

                    case EventKind.TokenReceive:
                        {
                            var data = Serialization.Unserialize<TokenEventData>(evt.Data.Decode());
                            amount = data.Value;
                            receiverAddress = Address.FromText(evt.EventAddress);
                            receiverChain = data.ChainName;
                            receiverToken = data.Symbol;
                            var amountDecimal = UnitConversion.ToDecimal(amount, phantasmaTokens.Single(p => p.Symbol == receiverToken).Decimals);
                            if (data.Symbol == "TTRS")
                            {
                              amountsymbol = $"{data.Symbol} • NFT";
                              break;
                            }
                            amountsymbol = $"{amountDecimal.ToString("#,0.##########").ToString(new CultureInfo("en-US"))} {receiverToken}";
                        }
                        break;

                    case EventKind.TokenMint:
                        {
                            var data = Serialization.Unserialize<TokenEventData>(evt.Data.Decode());
                            amount = data.Value;
                            receiverAddress = Address.FromText(evt.EventAddress);
                            receiverChain = data.ChainName;
                            var amountDecimal = UnitConversion.ToDecimal(amount, phantasmaTokens.Single(p => p.Symbol == data.Symbol).Decimals);
                            amountsymbol = $"{amountDecimal.ToString("#,0.##########").ToString(new CultureInfo("en-US"))} {data.Symbol}";
                            if (data.Symbol == "TTRS")
                            {
                              amountsymbol = $"{data.Symbol} • NFT";
                            }
                        }
                        break;

                      case EventKind.AddressRegister:
                          {
                              return amountsymbol = $"";
                          }
                          break;

                }
            }

            return amountsymbol;
        }

        public static string GetTxType(TransactionDto tx, List<ChainDto> phantasmaChains, List<TokenDto> phantasmaTokens)
        {
            string typetx = null;

            string senderToken = null;
            string senderChain = phantasmaChains.Where(x => x.Address == tx.ChainAddress).Select(x => x.Name).FirstOrDefault();
            Address senderAddress = Address.Null;

            string receiverToken = null;
            string receiverChain = "";
            Address receiverAddress = Address.Null;

            BigInteger amount = 0;

            foreach (var evt in tx.Events)
            {
                switch (evt.EventKind)
                {

                  case EventKind.ContractDeploy:
                      {
                          return typetx = $"Custom";
                      }
                      break;

                  case EventKind.AddressRegister:
                      {
                          return typetx = $"Custom";
                      }
                      break;

                  case EventKind.TokenClaim:
                      {
                          var data = Serialization.Unserialize<TokenEventData>(evt.Data.Decode());
                          amount = data.Value;
                          if (data.Symbol == "SOUL" || (data.Symbol == "KCAL" && amount >= 1000000000))
                          {
                            return typetx = $"Custom";
                          }
                      }
                      break;

                  case EventKind.TokenStake:
                      {
                          var data = Serialization.Unserialize<TokenEventData>(evt.Data.Decode());
                          amount = data.Value;
                          if (amount >= 1000000000)
                          {
                            if (data.Symbol != "KCAL" && data.Symbol != "NEO" && data.Symbol != "GAS")
                            {
                              //return typetx = $"Stake";
                              return typetx = $"Custom";
                            }
                          }
                      }
                      break;

                  case EventKind.TokenMint:
                      {
                          var data = Serialization.Unserialize<TokenEventData>(evt.Data.Decode());
                          if (data.Symbol == "TTRS" || data.Symbol == "GOATI")
                          {
                            return typetx = $"Custom";
                          }
                          return typetx = $"Mint";
                      }
                      break;

                  case EventKind.TokenSend:
                      {
                          var data = Serialization.Unserialize<TokenEventData>(evt.Data.Decode());
                          amount = data.Value;
                          senderAddress = Address.FromText(evt.EventAddress);
                          senderToken = data.Symbol;
                      }
                      break;

                  case EventKind.TokenReceive:
                      {
                          var data = Serialization.Unserialize<TokenEventData>(evt.Data.Decode());
                          amount = data.Value;
                          receiverAddress = Address.FromText(evt.EventAddress);
                          receiverToken = data.Symbol;
                      }
                      break;

                }
            }

            if (typetx == null)
            {
                if (amount > 0 && senderAddress != Address.Null && receiverAddress != Address.Null &&
                    senderToken != null && senderToken == receiverToken)
                {
                    typetx = $"{senderAddress.ToString()}";
                }
                else if (amount > 0 && receiverAddress != Address.Null && receiverToken != null)
                {
                    typetx = $"{receiverAddress.ToString()}";
                }
                else
                {
                    typetx = $"Custom";
                }
            }

            return typetx;
        }

        public static string GetTxDescription(TransactionDto tx, List<ChainDto> phantasmaChains, List<TokenDto> phantasmaTokens, string addressfrom)
        {
            string description = null;

            string senderToken = null;
            string senderChain = phantasmaChains.Where(x => x.Address == tx.ChainAddress).Select(x => x.Name).FirstOrDefault();
            Address senderAddress = Address.Null;

            string receiverToken = null;
            string receiverChain = "";
            Address receiverAddress = Address.Null;

            BigInteger amount = 0;

            foreach (var evt in tx.Events)
            {
                switch (evt.EventKind)
                {

                    case EventKind.TokenClaim:
                        {
                            var data = Serialization.Unserialize<TokenEventData>(evt.Data.Decode());
                            if (data.Symbol == "SOUL")
                            {
                              return description = $"Custom transaction";
                            }
                        }
                          break;


                    case EventKind.TokenStake:
                        {
                            var data = Serialization.Unserialize<TokenEventData>(evt.Data.Decode());
                            amount = data.Value;
                            if (amount >= 1000000000)
                            {
                              if (data.Symbol != "KCAL" && data.Symbol != "NEO" && data.Symbol != "GAS")
                              {
                                //return description = $"Stake transaction";
                                return description = $"Custom transaction";
                              }
                            }
                        }
                        break;

                    case EventKind.TokenMint:
                        {
                          var data = Serialization.Unserialize<TokenEventData>(evt.Data.Decode());
                            if (data.Symbol == "TTRS")
                            {
                              return description = $"Custom transaction";
                            }
                            return description = $"Claim transaction";
                        }
                        break;

                    case EventKind.AddressRegister:
                        {
                            var name = Serialization.Unserialize<string>(evt.Data.Decode());
                            description = $"Register transaction: name '{name}' registered";
                        }
                        break;

                    case EventKind.TokenSend:
                        {
                            var data = Serialization.Unserialize<TokenEventData>(evt.Data.Decode());
                            amount = data.Value;
                            senderAddress = Address.FromText(evt.EventAddress);
                            senderToken = data.Symbol;
                        }
                        break;

                    case EventKind.TokenReceive:
                        {
                            var data = Serialization.Unserialize<TokenEventData>(evt.Data.Decode());
                            amount = data.Value;
                            receiverAddress = Address.FromText(evt.EventAddress);
                            receiverChain = data.ChainName;
                            receiverToken = data.Symbol;
                        }
                        break;

                }
            }

            if (description == null)
            {
                if (amount > 0 && senderAddress != Address.Null && receiverAddress != Address.Null &&
                    senderToken != null && senderToken == receiverToken)
                {
                    var amountDecimal = UnitConversion.ToDecimal(amount, phantasmaTokens.Single(p => p.Symbol == senderToken).Decimals);

                    if (addressfrom == senderAddress.ToString())
                    {
                      description =
                          $"Send transaction: to {receiverAddress.ToString()}";
                    }
                    else
                    {
                        description =
                            $"Receive transaction: from {senderAddress.ToString()}";
                    }

                }
                else if (amount > 0 && receiverAddress != Address.Null && receiverToken != null)
                {
                    var amountDecimal = UnitConversion.ToDecimal(amount, phantasmaTokens.Single(p => p.Symbol == receiverToken).Decimals);

                    description = $"Send transaction: to {receiverAddress.Text} ";
                }
                else
                {
                    description = "Custom transaction";
                }

            }

            return description;
        }

        public static string DisassembleScript(byte[] script)
        {
            return string.Join('\n', new Disassembler(script).Instructions);
        }

        public static MultisigSettings GetMultisigSettings(string scriptString)
        {
            //HACK TODO (!!!) this method is a very ugly hack, this needs to improve badly
            List<Address> addressList = new List<Address>();
            string[] str = scriptString.Split(' ');
            Regex regex = new Regex(@"LOAD\s*r4,\s(\d*)");
            Match match = regex.Match(scriptString);
            int minSignees = Int32.Parse(match.Value.Split(" ").Last());
            foreach (var s in str)
            {
                if (s.StartsWith("\""))
                {
                    var tempStr = Regex.Replace(s.Replace('"', ' ').Trim(), Environment.NewLine, " ").Split(' ').First();
                    if (tempStr.Length == 45)
                    {
                        try
                        {
                            addressList.Add(Address.FromText(tempStr));
                        }
                        catch {}
                    }
                }
            }

            return new MultisigSettings
            {
                addressCount = addressList.Count,
                signeeCount = minSignees,
                addressArray = addressList.ToArray()
            };
        }
        private static string GetChainName(string address, List<ChainDto> phantasmaChains)
        {
            foreach (var element in phantasmaChains)
            {
                if (element.Address == address) return element.Name;
            }

            return string.Empty;
        }

        public static WalletConfigDto ReadConfig(string path)
        {
            path = FixPath(path, true);
            if (File.Exists(path))
            {
                return JsonConvert.DeserializeObject<WalletConfigDto>(File.ReadAllText(path));
            }

            return new WalletConfigDto();
        }

        public static void WriteConfig<T>(T walletConfig, string path)
        {
            path = FixPath(path, true);
            if (path == null || path == "")
            {
                Console.WriteLine("Path cannot be empty!");
                return;
            }

            File.WriteAllText(path, JsonConvert.SerializeObject(walletConfig));
        }

        public static string FixPath(string path, bool final)
        {
            String platform = System.Environment.OSVersion.Platform.ToString();

            if (platform != "Unix")
            {
                path = path.Replace(@"/", @"\");
                if (!final && !path.EndsWith(@"\"))
                {
                    path += @"\";
                }
            }
            else
            {
                path = path.Replace(@"\", @"/");
                if (!final && !path.EndsWith(@"/"))
                {
                    path += @"/";
                }
            }

            return path;
        }

        public static decimal GetCoinRate(string ticker, string currrency)
        {
            string json;
            string baseticker;
            switch (ticker)
            {
                case "SOUL":
                    baseticker = "phantasma";
                    break;
                case "KCAL":
                    baseticker = "phantasma";
                    break;
                case "NEO":
                    baseticker = "neo";
                    break;
                case "GAS":
                    baseticker = "gas";
                    break;
                default:
                    baseticker = "phantasma";
                    break;
            }

            var url = $"https://api.coingecko.com/api/v3/simple/price?ids={baseticker}&vs_currencies={currrency}";

            try
            {
                using (var httpClient = new HttpClient())
                {
                  json = httpClient.GetStringAsync(new Uri(url)).Result;
                }
                var root = JSONReader.ReadFromString(json);

                // hack for kcal price 1/5 soul & goati .10
                if (ticker == "KCAL")
                {
                  root = root["phantasma"];
                  var price = root.GetDecimal(currrency.ToLower())/5;
                  return price;
                }
                else if (ticker == "GOATI") {
                  var price = 0.10m;
                  return price;
                }
                else {
                  root = root[baseticker];
                  var price = root.GetDecimal(currrency.ToLower());
                  return price;
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred: {ex}");
                return 0;
            }
        }
    }
}
