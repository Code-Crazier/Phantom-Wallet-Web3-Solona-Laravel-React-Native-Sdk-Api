using System;
using Phantasma.RpcClient.DTOs;
using Phantasma.Cryptography;

namespace Phantom.Wallet.Models
{
    public class MenuEntry
    {
        public string Id { get; set; }
        public string Icon { get; set; }
        public string Caption { get; set; }
        public bool Enabled { get; set; }
        public int Count { get; set; }
        public bool IsSelected { get; set; }
    }

    public class AccountCache
    {
        public DateTime LastUpdated;
        public Transaction[] Transactions;
        public Holding[] Holdings;
        public BalanceSheetDto[] Tokens;
    }

    public struct Holding
    {
        public string Name;
        public string ChainName;
        public string Symbol;
        public string Chain;
        public string Icon;
        public decimal Amount;
        public decimal Rate;
        public decimal Price => (Amount * Rate);
        public string AmountFormated => Amount.ToString("#,0.###########");
        public string PriceFormated => Price.ToString("#,0");
        public string CurrencySymbol;
        public string Currency;
    }

    public struct Transaction
    {
        public string Type;
        public DateTime Date;
        public string Hash;
        public string Amount;
        public string Description;
    }

    public class Net
    {
        public string Name { get; set; }
        public int Value { get; set; }
        public bool IsEnabled { get; set; }
        public string Url { get; set; }
    }

    public struct ErrorContext
    {
        public string ErrorDescription;
        public string ErrorCode;
    }

    public struct MultisigSettings
    {
        public int addressCount;
        public int signeeCount;
        public Address[] addressArray;
    }

    public struct MultisigTx
    {
        public MultisigSettings Settings;
        public string TxData;
    }

    public struct SettleTx
    {
        public string ChainName;
        public string ChainAddress;
        public string DestinationChainAddress;
    }

    public struct TransferTx
    {
        public bool IsFungible;
        public string AddressTo;
        public string FromChain;
        public string ToChain;
        public string FinalChain;
        public string Symbol;
        public string AmountOrId;
    }

    public struct ErrorRes
    {
        public string error;
    }

}
