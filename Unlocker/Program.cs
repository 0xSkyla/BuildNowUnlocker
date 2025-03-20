using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.EventArguments;
using Titanium.Web.Proxy.Models;

namespace Unlocker
{
    class Program
    {
        static ProxyServer proxyServer;
        static HttpClient httpClient;

        static string pickaxesUrl = "https://gist.githubusercontent.com/0xSkyla/61c242648de176779b5e429f7c2471c6/raw/bc8cc3b2aa0c99731a2f913a589058ba90c830a1/pickaxes.txt";
        static string skinsUrl = "https://gist.githubusercontent.com/0xSkyla/61c242648de176779b5e429f7c2471c6/raw/bc8cc3b2aa0c99731a2f913a589058ba90c830a1/skins.txt";        //feel free to replace this with your own local files
        static string backpacksUrl = "https://gist.githubusercontent.com/0xSkyla/61c242648de176779b5e429f7c2471c6/raw/bc8cc3b2aa0c99731a2f913a589058ba90c830a1/backpacks.txt";

        static List<string> pickaxes = new List<string>();
        static List<string> skins = new List<string>();
        static List<string> backpacks = new List<string>();

        static DateTime lastFetchTime = DateTime.MinValue;

        static bool originalProxyEnabled;
        static string originalProxyServer;
        static bool originalProxyBypassLocal;
        static string originalProxyBypass;

        static async Task Main(string[] args)
        {
            httpClient = new HttpClient();
            Console.WriteLine("===================================================================");
            Console.WriteLine("    BUILDNOW.GG UNLOCKER - v1.3.8 - github.com/0xSkyla/BuildNowUnlockerUnlocker");
            Console.WriteLine("===================================================================");
            Console.CancelKeyPress += Console_CancelKeyPress;
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;

            SaveOriginalProxySettings();
            if (args.Length >= 3)
            {
                pickaxesUrl = args[0];
                skinsUrl = args[1];
                backpacksUrl = args[2];
            }

            await FetchItems();

            proxyServer = new ProxyServer();

            var explicitEP = new ExplicitProxyEndPoint(IPAddress.Any, 8080, true);
            proxyServer.AddEndPoint(explicitEP);
            proxyServer.BeforeResponse += OnResponse;

            // start server
            proxyServer.Start();
            proxyServer.CertificateManager.CreateRootCertificate();
            proxyServer.CertificateManager.TrustRootCertificate();

            SetSystemProxy("127.0.0.1:8080");

            Console.WriteLine("Started proxy succesfully!");
            Console.ReadKey();
            CleanUp();

        }

        static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            CleanUp();
        }

        static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            CleanUp();
        }

        static void CleanUp()
        {
            try
            {
                Console.WriteLine("Cleaning up...");
                RestoreOriginalProxySettings();
                if (proxyServer != null)
                {
                    proxyServer.BeforeResponse -= OnResponse;
                    proxyServer.Stop();
                }
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during cleanup: {ex.Message}");
                Environment.Exit(1);
            }
        }

        static void SaveOriginalProxySettings()
        {
            try
            {
                RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);
                originalProxyEnabled = (int)registryKey.GetValue("ProxyEnable", 0) == 1;
                originalProxyServer = (string)registryKey.GetValue("ProxyServer", "");
                originalProxyBypassLocal = originalProxyServer.Contains(";<local>");
                originalProxyBypass = (string)registryKey.GetValue("ProxyOverride", "");

                Console.WriteLine($"Saved original proxy settings: Enabled={originalProxyEnabled}, Server={originalProxyServer}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving original proxy settings: {ex.Message}");
            }
        }

        static void SetSystemProxy(string proxyAddress)
        {
            try
            {
                RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);

                // Enable proxy
                registryKey.SetValue("ProxyEnable", 1);
                registryKey.SetValue("ProxyServer", proxyAddress);
                registryKey.SetValue("ProxyOverride", "<local>");
                RefreshSystemProxySettings();

                Console.WriteLine($"System proxy set to {proxyAddress}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting proxy");
            }
        }

        static void RestoreOriginalProxySettings()
        {
            try
            {
                RegistryKey regKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Internet Settings", true);
                regKey.SetValue("ProxyEnable", originalProxyEnabled ? 1 : 0);

                if (originalProxyServer != "")
                {
                    regKey.SetValue("ProxyServer", originalProxyServer);
                }

                if (originalProxyBypass != "")
                {
                    regKey.SetValue("ProxyOverride", originalProxyBypass);
                }
                RefreshSystemProxySettings();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error when reverting proxy settings: {ex.Message}");
            }
        }

        [DllImport("wininet.dll")]
        static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        private const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
        private const int INTERNET_OPTION_REFRESH = 37;

        static void RefreshSystemProxySettings()
        {
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
        }

        static async Task FetchItems()
        {
            try
            {

                //  pickaxes
                string pickaxesContent = await httpClient.GetStringAsync(pickaxesUrl);
                pickaxes = pickaxesContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                         .Select(item => item.Trim())
                                         .Where(item => !string.IsNullOrEmpty(item))
                                         .ToList();

                //  skins
                string skinsContent = await httpClient.GetStringAsync(skinsUrl);
                skins = skinsContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                   .Select(item => item.Trim())
                                   .Where(item => !string.IsNullOrEmpty(item))
                                   .ToList();

                // backpacks
                string backpacksContent = await httpClient.GetStringAsync(backpacksUrl);
                backpacks = backpacksContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                                          .Select(item => item.Trim())
                                          .Where(item => !string.IsNullOrEmpty(item))
                                          .ToList();

                lastFetchTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"error fetching items: {ex.Message}");
            }
        }

        static async Task OnResponse(object sender, SessionEventArgs e)
        {

            var requestUrl = e.HttpClient.Request.Url;
            if (requestUrl.Contains("https://8415a.playfabapi.com/Client/LoginWithCustomID"))
            {
                string responseBody = await e.GetResponseBodyAsString();

                try
                {
                    JsonNode responseJson = JsonNode.Parse(responseBody);
                    if (responseJson == null)
                    {
                        Console.WriteLine("response not valid");
                        return;
                    }
                    JsonNode dataNode = responseJson["data"];
                    JsonNode infoResultNode = dataNode["InfoResultPayload"];
                    JsonNode inventoryNode = infoResultNode["UserInventory"];
                    if (inventoryNode == null || inventoryNode is not JsonArray)
                    {
                        inventoryNode = new JsonArray();
                        infoResultNode["UserInventory"] = inventoryNode;
                    }

                    JsonArray inventory = (JsonArray)inventoryNode;

                    foreach (var itemId in pickaxes)
                    {
                        bool exists = false;
                        foreach (JsonNode existingItem in inventory)
                        {
                            if (existingItem["ItemId"]?.ToString() == itemId &&
                                existingItem["ItemClass"]?.ToString() == "Pickaxe")
                            {
                                exists = true;
                                break;
                            }
                        }

                        if (!exists)
                        {
                            inventory.Add(CreateItemsJson(itemId, "Pickaxe"));
                            Console.WriteLine($"Added pickaxe: {itemId}");
                        }
                    }
                    foreach (var itemId in skins)
                    {
                        bool exists = false;
                        foreach (JsonNode existingItem in inventory)
                        {
                            if (existingItem["ItemId"]?.ToString() == itemId &&
                                existingItem["ItemClass"]?.ToString() == "Player")
                            {
                                exists = true;
                                break;
                            }
                        }

                        if (!exists)
                        {
                            inventory.Add(CreateItemsJson(itemId, "Player"));
                            Console.WriteLine($"Added skin: {itemId}");
                        }
                    }

                    // add backpacks
                    foreach (var itemId in backpacks)
                    {
                        bool exists = false;
                        foreach (JsonNode existingItem in inventory)
                        {
                            if (existingItem["ItemId"]?.ToString() == itemId &&
                                existingItem["ItemClass"]?.ToString() == "Backpack")
                            {
                                exists = true;
                                break;
                            }
                        }

                        if (!exists)
                        {
                            inventory.Add(CreateItemsJson(itemId, "Backpack"));
                            Console.WriteLine($"Added backpack: {itemId}");
                        }
                    }
                    string modifiedResponse = responseJson.ToJsonString();
                    e.SetResponseBodyString(modifiedResponse);
                    Console.WriteLine("UNLOCKED SUCCESFULLY!");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error unlocking items: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                }
            }
        }
        static JsonNode CreateItemsJson(string itemId, string itemClass)
        {
            Random random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            string randomId = new string(Enumerable.Repeat(chars, 15)
                .Select(s => s[random.Next(s.Length)]).ToArray());

            var itemNode = new JsonObject
            {
                ["ItemId"] = itemId,
                ["ItemInstanceId"] = randomId,
                ["ItemClass"] = itemClass,
                ["PurchaseDate"] = "1111-11-11T11:11:11.556Z",
                ["RemainingUses"] = 1,
                ["CatalogVersion"] = "EventOrFree",
                ["DisplayName"] = "1_Reward_Pass_2lvl",
                ["UnitPrice"] = 0
            };

            return itemNode;
        }
    }
}
