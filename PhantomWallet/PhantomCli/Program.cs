using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.Serialization.Formatters.Binary;
using Phantasma.Cryptography;
using Phantasma.Numerics;
using Phantasma.VM;
using Phantasma.VM.Utils;
using Phantasma.Blockchain.Contracts;
using Phantasma.Storage;
using Phantom.Wallet.Controllers;
using Phantom.Wallet.Helpers;
using Phantom.Wallet.Models;
using Transaction = Phantom.Wallet.Models.Transaction;
using ChainTx = Phantasma.Blockchain.Transaction;
using Newtonsoft.Json;
using Phantasma.RpcClient.DTOs;
using Phantasma.CodeGen.Assembler;

using Serilog;
using Serilog.Core;


namespace PhantomCli
{
    class PhantomCli
    {
        private static PhantasmaKeys keyPair {get; set;} = null;

        private static AccountController AccountController { get; set; }

        private static CliWalletConfig config = Utils.ReadConfig<CliWalletConfig>(".config");

        private static String prompt { get; set; }

        private static Logger CliLogger = new LoggerConfiguration().MinimumLevel.Debug()
                            .WriteTo.Console(outputTemplate:"{Message:lj}{NewLine}{Exception}")
                            .CreateLogger();

       // private static Logger Logger = new LoggerConfiguration().MinimumLevel.Debug()
       //                             .WriteTo.File(config.LogFile).CreateLogger();

        public static void SetupControllers()
        {
            AccountController = new AccountController();
        }

        static void Main(string[] args)
        {
            string version = "0.1-alpha";
            prompt = config == null ? "phantom> ": config.Prompt;
            var startupMsg =  config == null ? "PhantomCli " + version : config.StartupMsg + " " + version;

            //Logger.Information(startupMsg);

            SetupControllers();
            AccountController.UpdateConfig(
                    Utils.ReadConfig<CliWalletConfig>(Utils.CfgPath));

            List<string> completionList = new List<string>(lCommands.Keys); 
            Prompt.Run(
                ((command, listCmd, lists) =>
                {

                    string command_main = command.Trim().Split(new char[] { ' ' }).First();
                    string[] arguments = command.Split(new char[] { ' ' }).Skip(1).ToArray();
                    if (lCommands.ContainsKey(command_main))
                    {
                        Tuple<Action<string[]>, string> cmd = null;
                        lCommands.TryGetValue(command_main, out cmd);
                        Action<string[]> function_to_execute = cmd.Item1;
                        function_to_execute(arguments);
                    }
                    else
                        CliLogger.Warning("Command '" + command_main + "' not found");
                    return null;
                }), prompt, startupMsg, completionList);
        }

        private static Dictionary<string, Tuple<Action<string[]>, string>> lCommands = 
            new Dictionary<string, Tuple<Action<string[]>, string>>()
        {
            { "help",       new Tuple<Action<string[]>, string>(HelpFunc,       "Shows this help")},
            { "exit",       new Tuple<Action<string[]>, string>(Exit,           "Exit the PhantomCli shell")},
            { "clear",      new Tuple<Action<string[]>, string>(Clear,          "Clears the screen")},
            { "wallet",     new Tuple<Action<string[]>, string>(Wallet,         "Opens a wallet with a private key")},
            { "tx",         new Tuple<Action<string[]>, string>(Transaction,    "Param [txid], shows the transaction in formatted json")},
            { "abi",        new Tuple<Action<string[]>, string>(ContractFunc,   "Param [chain, contract] shows the abi of the contract")},
            { "invoke",     new Tuple<Action<string[]>, string>(InvokeFunc,     "invoke a contract method that requires no signed tx [not finished]")},
            { "invokeTx",   new Tuple<Action<string[]>, string>(InvokeTxFunc,   "invoke a contract method that requires a signed tx")},
            { "history",    new Tuple<Action<string[]>, string>(HistoryFunc,    "show the command history")},
            //{ "send",       new Tuple<Action<string[]>, string>(SendFunc,       "")},
            { "test",       new Tuple<Action<string[]>, string>(TestFunc,       "test")},
            { "config",     new Tuple<Action<string[]>, string>(ConfigFunc,     "change cli config")}
        };

        private static void ConfigFunc(string[] obj)
        {

            if (obj.Length < 1)
            {
                CliLogger.Information("Too less arguments");
                return;
            }

            string action = obj[0];
            string cfgItem = null; 
            string cfgValue = null;
            try
            {
                cfgItem = obj[1];
            }
            catch (IndexOutOfRangeException)
            {
                if (action == "set") 
                {
                    CliLogger.Information("Item cannot be empty!");
                    return;
                }
            }

            try
            {
                cfgValue = obj[2];
            }
            catch (IndexOutOfRangeException)
            {
                if (action == "set") 
                {
                    CliLogger.Information("Value cannot be empty!");
                    return;
                }
            }

            if (action == "set") {
                if (cfgItem == "prompt") 
                {
                    config.Prompt = cfgValue + " ";
                    prompt = cfgValue + " ";
                }
                else if (cfgItem == "currency")
                {
                    // TODO check if currency is available
                    config.Currency = cfgValue;
                }
                else if (cfgItem == "network")
                {
                    config.Network = cfgValue;
                }
                else
                {
                    CliLogger.Information("Config item not found!");
                }
                // write new cfg
                Utils.WriteConfig<CliWalletConfig>(config, ".config");

            }
            else if (action == "show") 
            {
                if (cfgItem == "prompt") 
                {
                    CliLogger.Information("Value: " + config.Prompt);
                }
                else if (cfgItem == "currency")
                {
                    CliLogger.Information("Value: " + config.Currency);
                }
                else if (cfgItem == "network")
                {
                    CliLogger.Information("Value: " + config.Network);
                }
                else
                {
                    CliLogger.Information(config.ToJson());
 
                }

            }
        }


        private static void TestFunc(string[] obj)
        {
            var json2 = @"{ 'parameters': [  { 'name': 'address', 'vmtype': 'Object', 'type': 'Phantasma.Cryptography.Address', 'input': 'P5ySorAXMaJLwe6AqTsshW3XD8ahkwNpWHU9KLX9CwkYd', 'info': 'info1' }, 
                                             { 'name': 'address', 'vmtype': 'Enum', 'type': 'Phantasma.Blockchain.ArchiveFlags', 'input': '1', 'info': 'info1' },
                                             { 'name': 'address', 'vmtype': 'Enum', 'type': 'Phantasma.Blockchain.ArchiveFlags', 'input': '1', 'info': 'info1' },
                                             { 'name': 'address', 'vmtype': 'Enum', 'type': 'Phantasma.Blockchain.Contracts.Native.ExchangeOrderSide', 'input': '1', 'info': 'info1' },
                                             { 'name': 'address', 'vmtype': 'Enum', 'type': 'Phantasma.Blockchain.Contracts.Native.ExchangeOrderSide', 'input': '2', 'info': 'info1' },
                                             { 'name': 'address', 'vmtype': 'Enum', 'type': 'Phantasma.Blockchain.Contracts.Native.ExchangeOrderType', 'input': '3', 'info': 'info1' },
                                             { 'name': 'address', 'vmtype': 'Enum', 'type': 'Phantasma.Blockchain.Contracts.Native.ExchangeOrderType', 'input': '4', 'info': 'info1' },
                                             { 'name': 'address', 'vmtype': 'Timestamp', 'type': 'Phantasma.Core.Types.Timestamp', 'input': '07/20/2019 20:04:30', 'info': 'info1' },
                                             { 'name': 'address', 'vmtype': 'Bool', 'type': 'System.Boolean', 'input': 'False', 'info': 'info1' },
                                             { 'name': 'address', 'vmtype': 'Bool', 'type': 'System.Boolean', 'input': 'True', 'info': 'info1' },
                                             { 'name': 'address', 'vmtype': 'String', 'type': 'System.String', 'input': 'ThisIsAString', 'info': 'info1' },
                                             { 'name': 'address', 'vmtype': 'Bytes', 'type': 'System.Byte[]', 'input': 'ThisIsAByteArray', 'info': 'info1' },
                                             { 'name': 'address', 'vmtype': 'Number', 'type': 'System.Int32', 'input': '987654321', 'info': 'info1' },
                                             { 'name': 'address', 'vmtype': 'Number', 'type': 'Phantasma.Numerics.BigInteger', 'input': '12345678', 'info': 'info1' } 
                                          ] }";
            List<object> lst2 = SendUtils.BuildParamList(json2);
        }

        public static T[] ConcatArrays<T>(params T[][] list)
        {
            var result = new T[list.Sum(a => a.Length)];
            int offset = 0;
            for (int x = 0; x < list.Length; x++)
            {
                list[x].CopyTo(result, offset);
                offset += list[x].Length;
            }
            return result;
        }

        private static string GetTransaction(string[] obj)
        {
            if (obj.Length > 1)
            {
                CliLogger.Information("Too many arguments");
                return "";
            }

            if (obj.Length < 1)
            {
                CliLogger.Information("Too less arguments");
                return "";
            }

            string txHash = obj[0];
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(AccountController
                    .GetTxConfirmations(txHash).Result, Formatting.Indented);
            return json;
        }

        private static void Transaction(string[] obj)
        {
            if (obj.Length > 1)
            {
                CliLogger.Information("Too many arguments");
                return;
            }

            if (obj.Length < 1)
            {
                CliLogger.Information("Too less arguments");
                return;
            }

            string txHash = obj[0];
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(AccountController
                    .GetTxConfirmations(txHash).Result, Formatting.Indented);
            CliLogger.Information(json);
        }

        private static void ContractFunc(string[] obj)
        {
            if (obj.Length > 2)
            {
                CliLogger.Information("Too many arguments");
                return;
            }

            if (obj.Length < 2)
            {
                CliLogger.Information("Too less arguments");
                return;
            }

            string chain = obj[0];
            string contract = obj[1];
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(AccountController
                    .GetContractABI(chain, contract).Result, Formatting.Indented);
            CliLogger.Information(json);
        }

        private static void HistoryFunc(string[] obj)
        {
            CliLogger.Information("");
            foreach (List<char> item in Prompt.GetHistory()) 
            {
                CliLogger.Information(new String(item.ToArray()));
            }
            CliLogger.Information("");
        }

        private static void InvokeTxFunc(string[] obj)
        {
            if (obj.Length > 3)
            {
                CliLogger.Error("Too many arguments");
                return;
            }

            if (obj.Length < 3)
            {
                CliLogger.Error("Too less arguments");
                return;
            }

            string chain = obj[0];
            string contract = obj[1];
            string method = obj[2];
            PhantasmaKeys kp = GetLoginKey();
            object[] paramArray = new object[] {kp.Address, kp.Address};
            var result = AccountController.InvokeContractTxGeneric(kp, chain, contract, method, paramArray).Result;
            if (result == null) {
                CliLogger.Warning("Node returned null...");
                return;
            }

            CliLogger.Information("Result: " + result);

        }

        private static void InvokeFunc(string[] obj)
        {
            string chain = obj[0];
            string contract = obj[1];
            string method = obj[2];
            PhantasmaKeys kp = GetLoginKey();
            object[] paramArray = new object[] {kp.Address};
            var result = AccountController.InvokeContractGeneric(kp, chain, contract, method, paramArray).Result;
            if (result == null) {
                CliLogger.Warning("Node returned null...");
                return;
            }

            CliLogger.Information("Result: " + result);

        }

        private static void Clear(string[] obj)
        {
            Console.Clear();
        }

        private static void Exit(string[] obj)
        {
            Environment.Exit(0);
        }

        private static void Wallet(string[] obj)
        {
            GetLoginKey();
        }

        private static PhantasmaKeys GetLoginKey(bool changeWallet=false)
        {
            if (keyPair == null && !changeWallet) 
            {
                CliLogger.Information("Enter private key: ");
                var wif = Console.ReadLine();
                var kPair = PhantasmaKeys.FromWIF(wif);
                keyPair = kPair;
            }
            return keyPair;
        }


        public static void HelpFunc(string[] args)
        {
            foreach (KeyValuePair<string, Tuple<Action<string[]>, string>> kvp in lCommands)
            {
                CliLogger.Information(string.Format("{0} [ {1} ]",kvp.Key.PadRight(15),kvp.Value.Item2));
            }
        }
    }
}
