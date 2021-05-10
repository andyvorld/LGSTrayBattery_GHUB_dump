using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Websocket.Client;

namespace LGSTrayBattery_GHUB_dump
{
    class Program
    {
        private const string OUTPUT_FOLDER = "./PowerModel";
        private static DateTime startTime;

        static void  Main(string[] args)
        {
            var url = new Uri("ws://localhost:9010");

            Directory.CreateDirectory(OUTPUT_FOLDER);

            var factory = new Func<ClientWebSocket>(() =>
            {
                var client = new ClientWebSocket();
                client.Options.UseDefaultCredentials = false;
                client.Options.SetRequestHeader("Origin", "file://");
                client.Options.SetRequestHeader("Pragma", "no-cache");
                client.Options.SetRequestHeader("Cache-Control", "no-cache");
                client.Options.SetRequestHeader("Sec-WebSocket-Extensions", "permessage-deflate; client_max_window_bits");
                client.Options.SetRequestHeader("Sec-WebSocket-Protocol", "json");
                return client;
            });

            using (var ws = new WebsocketClient(url, factory))
            {
                ws.MessageReceived.Subscribe(msg => MessageParse(ws, msg));

                Console.WriteLine($"Trying to connect to LGHUB_agent, at {url}");

                try
                {
                    ws.StartOrFail().Wait();
                }
                catch (Exception)
                {
                    Console.WriteLine("Failed to connect to LGHUB_agent, is Logitech G HUB running?");
                    Console.WriteLine("Press any key to quit.");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine("LGHUB_agent connected.");
                Console.WriteLine("");

                ws.Send(JsonConvert.SerializeObject(new
                {
                    msgId = "",
                    verb = "GET",
                    path = "/updates/info"
                }));

                startTime = DateTime.Now;

                while ((DateTime.Now - startTime).Milliseconds < 500)
                {
                    Thread.Sleep(100);
                }

                Console.WriteLine("");
                Console.WriteLine("Found all .xml files");
                Console.WriteLine("Press any key to quit.");
                Console.ReadKey();
            }
        }

        private static void MessageParse(WebsocketClient ws, ResponseMessage reply)
        {
            var replyJObject = JObject.Parse(reply.Text);
            Debug.WriteLine(reply);

            if (replyJObject["path"].ToString() == "/updates/info")
            {
                foreach (var depot in replyJObject["payload"]["depots"])
                {
                    List<string> xmlFiles = depot["files"].Where(x => ((string) x).EndsWith(".xml")).Select(x => (string) x).ToList();
                    if (xmlFiles.Count == 0)
                    {
                        continue;
                    }

                    Console.WriteLine($"Found device: {depot["name"]}");

                    XmlDocument xmlDoc = new XmlDocument();

                    foreach (string xmlFile in xmlFiles)
                    {
                        try
                        {
                            xmlDoc.Load(Path.Combine((string) depot["localFolder"], xmlFile));
                            var root = xmlDoc.SelectSingleNode("powermodel");

                            if (root == null)
                            {
                                throw new XmlException("powermodel not found");
                            }

                            int VID = int.Parse(root.Attributes["vid"].Value.Replace("0x", ""), NumberStyles.HexNumber);
                            int PID = int.Parse(root.Attributes["pid"].Value.Replace("0x", ""), NumberStyles.HexNumber);

                            string powerModelName = $"{VID:X3}_{PID:X3}.xml";

                            File.Copy(Path.Combine((string) depot["localFolder"], xmlFile), Path.Combine(OUTPUT_FOLDER, powerModelName), true);

                            startTime = DateTime.Now;
                        }
                        catch (XmlException)
                        {
                            Console.WriteLine($"{xmlFile} is not a valid xml batter model, skipping...");
                        }
                    }
                }
            }
        }
    }
}
