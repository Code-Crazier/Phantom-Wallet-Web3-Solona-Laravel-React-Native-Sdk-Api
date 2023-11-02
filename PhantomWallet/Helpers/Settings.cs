using System;
using Phantom.Wallet.Controllers;

namespace Phantom.Wallet.Helpers
{
    internal static class Settings
    {
        internal static string RpcServerUrl = "http://207.246.126.126:7077/rpc";

        internal static void SetRPCServerUrl()
        {
            RpcServerUrl = AccountController.WalletConfig != null ? AccountController.WalletConfig.RpcUrl : "http://207.246.126.126:7077/rpc";
        }

    }
}

