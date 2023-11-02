using System;
using System.Text;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Phantasma.Blockchain;
using Phantasma.Cryptography;
using Phantasma.RpcClient.DTOs;
using Phantasma.Numerics;
using Phantasma.VM;
using Phantasma.CodeGen.Assembler;
using Phantasma.Core.Types;
using Phantom.Wallet.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Phantom.Wallet.Helpers
{
    public static class SendUtils
    {
        public static List<ChainDto> SelectShortestPath(string from, string to, List<string> paths, List<ChainDto> phantasmaChains)
        {
            var finalPath = "";
            foreach (var path in paths)
            {
                if (path.IndexOf(from, StringComparison.Ordinal) != -1 && path.IndexOf(to, StringComparison.Ordinal) != -1)
                {
                    if (finalPath == "")
                    {
                        finalPath = path;
                    }
                    else if (path.Count(d => d == ',') < finalPath.Count(d => d == ','))
                    {
                        finalPath = path;
                    }
                }
            }
            var listStrLineElements = finalPath.Split(',').ToList();
            List<ChainDto> chainPath = new List<ChainDto>();
            foreach (var element in listStrLineElements)
            {
                chainPath.Add(phantasmaChains.Find(p => p.Name == element.Trim()));
            }
            return chainPath;
        }

        public static byte[] GenerateMultisigScript(MultisigSettings settings)
        {
            throw new Exception("NOT SUPPORTED YET");

            var scriptHead = new List<string>();
            bool isTrue = true;

            for (int i = 0; i < settings.addressArray.Length; i++)
            {
                scriptHead.Add($"load r1 { i }");
                scriptHead.Add($"load r2 \"{ settings.addressArray[i] }\"");
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

        public static bool IsTxHashValid(string data)
        {
            if (data == "" || String.IsNullOrEmpty(data)) return false;
            return Hash.TryParse(data, out Hash result);
        }

        public static T ParseEnum<T>(string value)
        {
            return (T) Enum.Parse(typeof(T), value, true);
        }

        public static List<object> BuildParamList(string parameters)
        {
            JObject jsonparam = JsonConvert.DeserializeObject<JObject>(parameters);
            List<object> paramList = new List<object>();

            foreach (var param in jsonparam)
            {
                foreach (var param2 in param.Value)
                {
                    string name = (string) param2["name"];
                    string type = (string) param2["type"];
                    string input = (string) param2["input"];
                    string info = (string) param2["info"];

                    object result = null;
                    Console.WriteLine($"Object[name: {name} type: {type} input: {input} info: {info} ]");

                    DateTime unixEpoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

                    switch (type)
                    {
                        case "Object":
                            // for now, we assume every Object is an address
                            // complex object creation will follow new ABI
                            Address address = Address.FromText(input);
                            result = address;
                            break;
                        case "Number":
                            BigInteger num = new BigInteger(input, 10);
                            result = num;
                            break;
                        case "Hash":
                            result = Hash.Parse(input);
                            break;
                        case "Timestamp":
                            DateTime date = DateTime.ParseExact(input, "MM/dd/yyyy HH:mm:ss",
                                    System.Globalization.CultureInfo.InvariantCulture);
                            var ticks = (uint)(date.ToUniversalTime() - unixEpoch).TotalSeconds;
                            result = new Timestamp(ticks);
                            break;
                        case "TimestampEpoch":
                            DateTime dtDateTime = new DateTime(1970,1,1,0,0,0,0,DateTimeKind.Utc);
                            dtDateTime = dtDateTime.AddMilliseconds( Convert.ToDouble(input) ).ToLocalTime();
                            var ticksEpoch = (uint)(dtDateTime.ToUniversalTime() - unixEpoch).TotalSeconds;
                            result = new Timestamp(ticksEpoch);
                            break;
                        case "Bool":
                            result = bool.Parse(input);
                            break;
                        case "String":
                            result = input;
                            break;
                        case "Bytes":
                            result = Encoding.UTF8.GetBytes(input);
                            break;
                        case "Enum":
                            result = Convert.ToInt32(input);
                            break;
                        default:
                            throw new Exception($"invalid type: {type} for {input}");
                    }

                    if (result != null)
                    {
                        paramList.Add(result);
                    }
                    else
                    {
                        Console.WriteLine($"Could not create parameter from object: Object[name: {name} type: {type} input: {input} info: {info} ]");
                    }
                }
            }

            return paramList;
        }

        public static List<ChainDto> GetShortestPath(string from, string to, List<ChainDto> phantasmaChains)
        {
            var vertices = new List<string>();
            var edges = new List<Tuple<string, string>>();

            var children = new Dictionary<string, List<ChainDto>>();
            foreach (var chain in phantasmaChains)
            {
                var childs = phantasmaChains.Where(p => p.ParentAddress.Equals(chain.Address));
                if (childs.Any())
                {
                    children[chain.Name] = childs.ToList();
                }
            }

            foreach (var chain in phantasmaChains)
            {
                vertices.Add(chain.Name);
                if (children.ContainsKey(chain.Name))
                {
                    foreach (var child in children[chain.Name])
                    {
                        edges.Add(new Tuple<string, string>(chain.Name, child.Name));
                    }
                }
            }
            var graph = new Graph<string>(vertices, edges);

            var shortestPath = Algorithms.ShortestPathFunction(graph, from);

            List<string> allpaths = new List<string>();
            foreach (var vertex in vertices)
            {
                allpaths.Add(string.Join(", ", shortestPath(vertex)));
            }

            foreach (var allpath in allpaths)
            {
                Debug.WriteLine(allpath);
            }

            return SelectShortestPath(from, to, allpaths, phantasmaChains);
        }
    }
}
