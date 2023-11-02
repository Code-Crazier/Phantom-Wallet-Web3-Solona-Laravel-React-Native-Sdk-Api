using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Phantasma.Blockchain;
using Phantasma.Core.Log;
using Phantasma.Cryptography;
using Phantasma.CodeGen.Assembler;
using Phantasma.Core.Types;
using Phantasma.Numerics;
using Phantasma.VM;
using System.Linq;
using Phantasma.Blockchain.Contracts;
using Phantasma.Blockchain.Tokens;
using Phantasma.Blockchain.Utils;
using Phantasma.Storage.Context;
using Phantasma.VM.Utils;
using Phantom.Wallet.Models;
using static Phantasma.Blockchain.Contracts.Native.TokenContract;

namespace Phantom.Tests
{
    [TestClass]
    public class MultisigTests
    {

        public static byte[] GenerateMultisigScript(MultisigSettings settings)
        {

            var scriptHead = new List<string>();
            bool isTrue = true;
            bool isFalse = false;

            for (int i = 0; i < settings.addressArray.Length; i++)
            {
                scriptHead.Add($"load r1 { i }");
                scriptHead.Add($"load r2 0x{ Base16.Encode(settings.addressArray[i].PublicKey) }");
                scriptHead.Add($"put r2 r3 r1");
            }

            // needs to check send/receive triggers! NOT YET DONE
            var scriptTail = new string[]
            {

                "alias r4 $minSigned",
                "alias r5 $addrCount",
                "alias r6 $i",
                "alias r7 $loopResult",
                "alias r8 $interResult",
                "alias r9 $result",
                "alias r10 $signed",
                "alias r11 $temp",
                "alias r12 $true",

                // check if we are sending/receiving
                "alias r13 $triggerSend",
                "alias r14 $currentTrigger",
                "alias r15 $false",
                $"load $false { isFalse }",

                $@"load $triggerSend, ""{AccountContract.TriggerSend}""",

                $"pop $currentTrigger",

                $"equal $triggerSend, $currentTrigger, $result",
                $"jmpnot $result, @finishNotSend",


                $"load $minSigned { settings.signeeCount }",
                $"load $addrCount { settings.addressArray.Length }",
                "load $signed 0",
                $"load $true { isTrue }",

                "load $i 0",
                "@loop: ",
                "lt $i $addrCount $loopResult",
                "jmpnot $loopResult @checkSigned",

                // get address from array
                "get r3 $temp $i",

                // push address to stack
                "push $temp",

                // convert to address object
                "extcall \"Address()\"",

                "call @checkWitness",
                "equal $interResult, $true, $result",
                "jmpif $result @increm",

                "inc $i",
                "jmp @loop",

                "@increm:",
                "inc $signed",

                "inc $i",
                "jmp @loop",

                "@checkSigned: ",
                "gte $signed $minSigned $result",
                "jmpif $result @finish",
                "jmpnot $result @break",
                "ret",

                "@finish:",
                "push $result",
                "ret",

                "@finishNotSend:",
                "push $true",
                "ret",

                "@break:",
                "throw",

                "@checkWitness: ",
                "extcall \"CheckWitness()\"",
                "pop $interResult",
                "ret",
            };

            var scriptString = scriptHead.Concat(scriptTail.ToArray()).ToArray();

            // temp test log to verify script
            List<string> tempList = new List<string>(scriptString);
            tempList.ForEach(Console.WriteLine);

            // build script
            var script = AssemblerUtils.BuildScript(scriptString);

            return script;
        }


        #region Multisig
        [TestMethod]
        public void MultiSigFail1Test()
        {

            var owner = KeyPair.Generate();
            var multiAddr = KeyPair.Generate();

            var signee1 = KeyPair.Generate();
            var signee2 = KeyPair.Generate();

            var target = KeyPair.Generate();

            List<KeyPair> signees = new List<KeyPair>() { signee1 };

            var simulator = new ChainSimulator(owner, 1234);
            var minSignees = 3;

            List<Address> addressList = new List<Address>() {
                multiAddr.Address,
                signee1.Address,
                signee2.Address
            };

            MultisigSettings settings = new MultisigSettings
            {
                addressCount = addressList.Count,
                signeeCount = minSignees,
                addressArray = addressList.ToArray()
            };

            var script = GenerateMultisigScript(settings);

            simulator.BeginBlock();

            simulator.GenerateTransfer(owner, multiAddr.Address, simulator.Nexus.RootChain, "KCAL", 100000000);
            simulator.GenerateTransfer(owner, multiAddr.Address, simulator.Nexus.RootChain, "SOUL", 2);

            // register script
            simulator.GenerateCustomTransaction(multiAddr,
                () => ScriptUtils.BeginScript().AllowGas(multiAddr.Address, Address.Null, 100000, 9999)
                    .CallContract("account", "RegisterScript", multiAddr.Address, script).SpendGas(multiAddr.Address)
                    .EndScript());
            simulator.EndBlock();

            var balance = simulator.Nexus.RootChain.GetTokenBalance("SOUL", multiAddr.Address);
            Assert.IsTrue(balance == 2);

            var accountScript = simulator.Nexus.LookUpAddressScript(multiAddr.Address);
            Assert.IsTrue(accountScript != null && accountScript.Length > 0);

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                // do multisig tx --> will fail with Phantasma.VM.VMDebugException: transfer failed
                simulator.GenerateTransfer(multiAddr, target.Address, simulator.Nexus.RootChain, "SOUL", 1, signees);
                simulator.EndBlock();
            });

            var sourceBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance("SOUL", multiAddr.Address);
            Console.WriteLine("SOUL: " + sourceBalanceSOUL);
            Assert.IsTrue(sourceBalanceSOUL == 2);

            var targetBalanceKCAL = simulator.Nexus.RootChain.GetTokenBalance("KCAL", target.Address);
            Assert.IsTrue(targetBalanceKCAL == 0);

            var targetBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance("SOUL", target.Address);
            Assert.IsTrue(targetBalanceSOUL == 0);
        }

        [TestMethod]
        public void MultiSigFail2Test()
        {
            var owner = KeyPair.Generate();
            var multiAddr = KeyPair.Generate();

            var signee1 = KeyPair.Generate();
            var signee2 = KeyPair.Generate();

            var target = KeyPair.Generate();

            List<KeyPair> signees = new List<KeyPair>() { };

            var simulator = new ChainSimulator(owner, 1234);
            var minSignees = 2;

            List<Address> addressList = new List<Address>() {
                multiAddr.Address,
                signee1.Address,
                signee2.Address
            };

            MultisigSettings settings = new MultisigSettings
            {
                addressCount = addressList.Count,
                signeeCount = minSignees,
                addressArray = addressList.ToArray()
            };

            var script = GenerateMultisigScript(settings);

            simulator.BeginBlock();

            simulator.GenerateTransfer(owner, multiAddr.Address, simulator.Nexus.RootChain, "KCAL", 100000000);
            simulator.GenerateTransfer(owner, multiAddr.Address, simulator.Nexus.RootChain, "SOUL", 2);

            // register script
            simulator.GenerateCustomTransaction(multiAddr,
                () => ScriptUtils.BeginScript().AllowGas(multiAddr.Address, Address.Null, 1, 9999)
                    .CallContract("account", "RegisterScript", multiAddr.Address, script).SpendGas(multiAddr.Address)
                    .EndScript());
            simulator.EndBlock();

            var balance = simulator.Nexus.RootChain.GetTokenBalance("SOUL", multiAddr.Address);
            Assert.IsTrue(balance == 2);

            var accountScript = simulator.Nexus.LookUpAddressScript(multiAddr.Address);
            Assert.IsTrue(accountScript != null && accountScript.Length > 0);

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                // do multisig tx --> will fail with Phantasma.VM.VMDebugException: transfer failed
                simulator.GenerateTransfer(multiAddr, target.Address, simulator.Nexus.RootChain, "SOUL", 1, signees);
                simulator.EndBlock();
            });

            var sourceBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance("SOUL", multiAddr.Address);
            Console.WriteLine("SOUL: " + sourceBalanceSOUL);
            Assert.IsTrue(sourceBalanceSOUL == 2);

            var targetBalanceKCAL = simulator.Nexus.RootChain.GetTokenBalance("KCAL", target.Address);
            Assert.IsTrue(targetBalanceKCAL == 0);

            var targetBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance("SOUL", target.Address);
            Assert.IsTrue(targetBalanceSOUL == 0);
        }

        [TestMethod]
        public void MultiSigFail3Test()
        {
            var owner = KeyPair.Generate();
            var multiAddr = KeyPair.Generate();

            var signee1 = KeyPair.Generate();
            var signee2 = KeyPair.Generate();

            var target = KeyPair.Generate();

            List<KeyPair> signees = new List<KeyPair>() { signee1, signee2 };

            var simulator = new ChainSimulator(owner, 1234);
            var minSignees = 4;

            List<Address> addressList = new List<Address>() {
                multiAddr.Address,
                signee1.Address,
                signee2.Address
            };

            MultisigSettings settings = new MultisigSettings
            {
                addressCount = addressList.Count,
                signeeCount = minSignees,
                addressArray = addressList.ToArray()
            };

            var script = GenerateMultisigScript(settings);

            simulator.BeginBlock();

            simulator.GenerateTransfer(owner, multiAddr.Address, simulator.Nexus.RootChain, "KCAL", 100000000);
            simulator.GenerateTransfer(owner, multiAddr.Address, simulator.Nexus.RootChain, "SOUL", 2);

            // register script
            simulator.GenerateCustomTransaction(multiAddr,
                () => ScriptUtils.BeginScript().AllowGas(multiAddr.Address, Address.Null, 1, 9999)
                    .CallContract("account", "RegisterScript", multiAddr.Address, script).SpendGas(multiAddr.Address)
                    .EndScript());
            simulator.EndBlock();

            var balance = simulator.Nexus.RootChain.GetTokenBalance("SOUL", multiAddr.Address);
            Assert.IsTrue(balance == 2);

            var accountScript = simulator.Nexus.LookUpAddressScript(multiAddr.Address);
            Assert.IsTrue(accountScript != null && accountScript.Length > 0);

            Assert.ThrowsException<Exception>(() =>
            {
                simulator.BeginBlock();
                // do multisig tx --> will fail with Phantasma.VM.VMDebugException: transfer failed
                simulator.GenerateTransfer(multiAddr, target.Address, simulator.Nexus.RootChain, "SOUL", 1, signees);
                simulator.EndBlock();
            });

            var sourceBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance("SOUL", multiAddr.Address);
            Console.WriteLine("SOUL: " + sourceBalanceSOUL);
            Assert.IsTrue(sourceBalanceSOUL == 2);

            var targetBalanceKCAL = simulator.Nexus.RootChain.GetTokenBalance("KCAL", target.Address);
            Assert.IsTrue(targetBalanceKCAL == 0);

            var targetBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance("SOUL", target.Address);
            Assert.IsTrue(targetBalanceSOUL == 0);
        }

        [TestMethod]
        public void MultiSigSuccess1Test()
        {
            var owner = KeyPair.Generate();
            var multiAddr = KeyPair.Generate();
            var signee1 = KeyPair.Generate();
            var signee2 = KeyPair.Generate();
            var target = KeyPair.Generate();

            List<KeyPair> signees = new List<KeyPair>() { signee2 };

            var simulator = new ChainSimulator(owner, 1234);
            var minSignees = 2;

            List<Address> addressList = new List<Address>() {
                multiAddr.Address,
                signee1.Address,
                signee2.Address
            };

            MultisigSettings settings = new MultisigSettings
            {
                addressCount = addressList.Count,
                signeeCount = minSignees,
                addressArray = addressList.ToArray()
            };

            var script = GenerateMultisigScript(settings);

            simulator.BeginBlock();

            simulator.GenerateTransfer(owner, multiAddr.Address, simulator.Nexus.RootChain, "KCAL", 100000000);
            simulator.GenerateTransfer(owner, multiAddr.Address, simulator.Nexus.RootChain, "SOUL", 2);

            // register script
            simulator.GenerateCustomTransaction(multiAddr,
                () => ScriptUtils.BeginScript().AllowGas(multiAddr.Address, Address.Null, 1, 9999)
                    .CallContract("account", "RegisterScript", multiAddr.Address, script).SpendGas(multiAddr.Address)
                    .EndScript());
            simulator.EndBlock();

            var balance = simulator.Nexus.RootChain.GetTokenBalance("SOUL", multiAddr.Address);
            Assert.IsTrue(balance == 2);

            var accountScript = simulator.Nexus.LookUpAddressScript(multiAddr.Address);
            Assert.IsTrue(accountScript != null && accountScript.Length > 0);

            simulator.BeginBlock();

            // do multisig tx
            simulator.GenerateTransfer(multiAddr, target.Address, simulator.Nexus.RootChain, "SOUL", 1, signees);
            simulator.EndBlock();

            var sourceBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance("SOUL", multiAddr.Address);
            Assert.IsTrue(sourceBalanceSOUL == 1);

            var targetBalanceKCAL = simulator.Nexus.RootChain.GetTokenBalance("KCAL", target.Address);
            Assert.IsTrue(targetBalanceKCAL == 0);

            var targetBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance("SOUL", target.Address);
            Assert.IsTrue(targetBalanceSOUL == 1);
        }

        [TestMethod]
        public void MultiSigSuccess2Test()
        {
            var owner = KeyPair.Generate();
            var multiAddr = KeyPair.Generate();
            var signee1 = KeyPair.Generate();
            var signee2 = KeyPair.Generate();
            var target = KeyPair.Generate();

            List<KeyPair> signees = new List<KeyPair>() { signee1, signee2 };

            var simulator = new ChainSimulator(owner, 1234);

            var minSignees = 2;

            List<Address> addressList = new List<Address>() {
                multiAddr.Address,
                signee1.Address,
                signee2.Address
            };

            MultisigSettings settings = new MultisigSettings
            {
                addressCount = addressList.Count,
                signeeCount = minSignees,
                addressArray = addressList.ToArray()
            };

            var script = GenerateMultisigScript(settings);

            simulator.BeginBlock();

            simulator.GenerateTransfer(owner, multiAddr.Address, simulator.Nexus.RootChain, "KCAL", 100000000);
            simulator.GenerateTransfer(owner, multiAddr.Address, simulator.Nexus.RootChain, "SOUL", 2);

            // register script
            simulator.GenerateCustomTransaction(multiAddr,
                () => ScriptUtils.BeginScript().AllowGas(multiAddr.Address, Address.Null, 1, 9999)
                    .CallContract("account", "RegisterScript", multiAddr.Address, script).SpendGas(multiAddr.Address)
                    .EndScript());
            simulator.EndBlock();

            var balance = simulator.Nexus.RootChain.GetTokenBalance("SOUL", multiAddr.Address);
            Assert.IsTrue(balance == 2);

            var accountScript = simulator.Nexus.LookUpAddressScript(multiAddr.Address);
            Assert.IsTrue(accountScript != null && accountScript.Length > 0);

            simulator.BeginBlock();

            // do multisig tx
            simulator.GenerateTransfer(multiAddr, target.Address, simulator.Nexus.RootChain, "SOUL", 1, signees);
            simulator.EndBlock();

            var sourceBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance("SOUL", multiAddr.Address);
            Assert.IsTrue(sourceBalanceSOUL == 1);

            var targetBalanceKCAL = simulator.Nexus.RootChain.GetTokenBalance("KCAL", target.Address);
            Assert.IsTrue(targetBalanceKCAL == 0);

            var targetBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance("SOUL", target.Address);
            Assert.IsTrue(targetBalanceSOUL == 1);
        }

        [TestMethod]
        public void MultiSigSuccess3Test()
        {
            var owner = KeyPair.Generate();

            var multiAddr = KeyPair.Generate();
            var signee1 = KeyPair.Generate();
            var signee2 = KeyPair.Generate();
            var signee3 = KeyPair.Generate();
            var signee4 = KeyPair.Generate();

            var target = KeyPair.Generate();

            List<KeyPair> signees = new List<KeyPair>() { signee1, signee2, signee3 };

            var simulator = new ChainSimulator(owner, 1234);

            var minSignees = 4;

            List<Address> addressList = new List<Address>() {
                multiAddr.Address,
                signee1.Address,
                signee2.Address,
                signee3.Address,
                signee4.Address
            };

            MultisigSettings settings = new MultisigSettings
            {
                addressCount = addressList.Count,
                signeeCount = minSignees,
                addressArray = addressList.ToArray()
            };

            var script = GenerateMultisigScript(settings);

            simulator.BeginBlock();

            simulator.GenerateTransfer(owner, multiAddr.Address, simulator.Nexus.RootChain, "KCAL", 100000000);
            simulator.GenerateTransfer(owner, multiAddr.Address, simulator.Nexus.RootChain, "SOUL", 2);

            // register script
            simulator.GenerateCustomTransaction(multiAddr,
                () => ScriptUtils.BeginScript().AllowGas(multiAddr.Address, Address.Null, 1, 9999)
                    .CallContract("account", "RegisterScript", multiAddr.Address, script).SpendGas(multiAddr.Address)
                    .EndScript());
            simulator.EndBlock();

            var balance = simulator.Nexus.RootChain.GetTokenBalance("SOUL", multiAddr.Address);
            Assert.IsTrue(balance == 2);

            var accountScript = simulator.Nexus.LookUpAddressScript(multiAddr.Address);
            Assert.IsTrue(accountScript != null && accountScript.Length > 0);

            simulator.BeginBlock();

            // do multisig tx
            simulator.GenerateTransfer(multiAddr, target.Address, simulator.Nexus.RootChain, "SOUL", 1, signees);
            simulator.EndBlock();

            var sourceBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance("SOUL", multiAddr.Address);
            Assert.IsTrue(sourceBalanceSOUL == 1);

            var targetBalanceKCAL = simulator.Nexus.RootChain.GetTokenBalance("KCAL", target.Address);
            Assert.IsTrue(targetBalanceKCAL == 0);

            var targetBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance("SOUL", target.Address);
            Assert.IsTrue(targetBalanceSOUL == 1);
        }

        [TestMethod]
        public void MultiSigReceive1Test()
        {
            var owner = KeyPair.Generate();

            var multiAddr = KeyPair.Generate();
            var signee1 = KeyPair.Generate();
            var signee2 = KeyPair.Generate();
            var signee3 = KeyPair.Generate();
            var signee4 = KeyPair.Generate();

            var target = KeyPair.Generate();

            List<KeyPair> signees = new List<KeyPair>() { signee1, signee2, signee3 };

            var simulator = new ChainSimulator(owner, 1234);

            var minSignees = 4;

            List<Address> addressList = new List<Address>() {
                multiAddr.Address,
                signee1.Address,
                signee2.Address,
                signee3.Address,
                signee4.Address
            };

            MultisigSettings settings = new MultisigSettings
            {
                addressCount = addressList.Count,
                signeeCount = minSignees,
                addressArray = addressList.ToArray()
            };

            var script = GenerateMultisigScript(settings);

            simulator.BeginBlock();

            simulator.GenerateTransfer(owner, multiAddr.Address, simulator.Nexus.RootChain, "KCAL", 100000000);
            simulator.GenerateTransfer(owner, multiAddr.Address, simulator.Nexus.RootChain, "SOUL", 2);

            simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, "KCAL", 100000000);
            simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, "SOUL", 2);

            // register script
            simulator.GenerateCustomTransaction(multiAddr,
                () => ScriptUtils.BeginScript().AllowGas(multiAddr.Address, Address.Null, 1, 9999)
                    .CallContract("account", "RegisterScript", multiAddr.Address, script).SpendGas(multiAddr.Address)
                    .EndScript());
            simulator.EndBlock();

            var balance = simulator.Nexus.RootChain.GetTokenBalance("SOUL", multiAddr.Address);
            Assert.IsTrue(balance == 2);

            var accountScript = simulator.Nexus.LookUpAddressScript(multiAddr.Address);
            Assert.IsTrue(accountScript != null && accountScript.Length > 0);

            simulator.BeginBlock();

            // send to multisig address
            simulator.GenerateTransfer(target, multiAddr.Address, simulator.Nexus.RootChain, "SOUL", 1);
            simulator.EndBlock();

            var sourceBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance("SOUL", multiAddr.Address);
            Assert.IsTrue(sourceBalanceSOUL == 3);

            var targetBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance("SOUL", target.Address);
            Assert.IsTrue(targetBalanceSOUL == 1);
        }

        [TestMethod]
        public void MultiSigReceive2Test()
        {
            var owner = KeyPair.Generate();

            var multiAddr = KeyPair.Generate();
            var signee1 = KeyPair.Generate();
            var signee2 = KeyPair.Generate();
            var signee3 = KeyPair.Generate();
            var signee4 = KeyPair.Generate();

            var target = KeyPair.Generate();

            List<KeyPair> signees = new List<KeyPair>() { signee1, signee2, signee3 };

            var simulator = new ChainSimulator(owner, 1234);

            var minSignees = 4;

            List<Address> addressList = new List<Address>() {
                multiAddr.Address,
                signee1.Address,
                signee2.Address,
                signee3.Address,
                signee4.Address
            };

            MultisigSettings settings = new MultisigSettings
            {
                addressCount = addressList.Count,
                signeeCount = minSignees,
                addressArray = addressList.ToArray()
            };

            var script = GenerateMultisigScript(settings);

            simulator.BeginBlock();
            simulator.GenerateTransfer(owner, multiAddr.Address, simulator.Nexus.RootChain, "KCAL", 100000000);
            simulator.GenerateTransfer(owner, multiAddr.Address, simulator.Nexus.RootChain, "SOUL", 2);

            simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, "KCAL", 100000000);
            simulator.GenerateTransfer(owner, target.Address, simulator.Nexus.RootChain, "SOUL", 2);

            simulator.GenerateTransfer(owner, signee1.Address, simulator.Nexus.RootChain, "KCAL", 100000000);
            simulator.GenerateTransfer(owner, signee1.Address, simulator.Nexus.RootChain, "SOUL", 2);

            simulator.GenerateTransfer(owner, signee2.Address, simulator.Nexus.RootChain, "KCAL", 100000000);
            simulator.GenerateTransfer(owner, signee2.Address, simulator.Nexus.RootChain, "SOUL", 2);

            simulator.GenerateTransfer(owner, signee3.Address, simulator.Nexus.RootChain, "KCAL", 100000000);
            simulator.GenerateTransfer(owner, signee3.Address, simulator.Nexus.RootChain, "SOUL", 2);

            // register script
            simulator.GenerateCustomTransaction(multiAddr,
                () => ScriptUtils.BeginScript().AllowGas(multiAddr.Address, Address.Null, 1, 9999)
                    .CallContract("account", "RegisterScript", multiAddr.Address, script).SpendGas(multiAddr.Address)
                    .EndScript());
            simulator.EndBlock();

            var balance = simulator.Nexus.RootChain.GetTokenBalance("SOUL", multiAddr.Address);
            Assert.IsTrue(balance == 2);

            var accountScript = simulator.Nexus.LookUpAddressScript(multiAddr.Address);
            Assert.IsTrue(accountScript != null && accountScript.Length > 0);

            simulator.BeginBlock();

            // send to multisig address
            simulator.GenerateTransfer(target, multiAddr.Address, simulator.Nexus.RootChain, "SOUL", 1);
            simulator.GenerateTransfer(signee1, multiAddr.Address, simulator.Nexus.RootChain, "SOUL", 1);
            simulator.GenerateTransfer(signee2, multiAddr.Address, simulator.Nexus.RootChain, "SOUL", 1);
            simulator.GenerateTransfer(signee3, multiAddr.Address, simulator.Nexus.RootChain, "SOUL", 1);

            simulator.EndBlock();

            var sourceBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance("SOUL", multiAddr.Address);
            Console.WriteLine("BALANCE: " + sourceBalanceSOUL);
            Assert.IsTrue(sourceBalanceSOUL == 6);

            var targetBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance("SOUL", target.Address);
            Assert.IsTrue(targetBalanceSOUL == 1);

            targetBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance("SOUL", signee1.Address);
            Assert.IsTrue(targetBalanceSOUL == 1);

            targetBalanceSOUL = simulator.Nexus.RootChain.GetTokenBalance("SOUL", signee2.Address);
            Assert.IsTrue(targetBalanceSOUL == 1);
        }

        #endregion
    }
}
