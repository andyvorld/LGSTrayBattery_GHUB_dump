using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Websocket.Client;

namespace LGSTrayBattery_GHUB_dump
{
    class Program
    {
        static void  Main(string[] args)
        {
            var url = new Uri("ws://localhost:9010");

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
                Console.WriteLine("Press any key to quit, after power models are extracted.");

                ws.Send(JsonConvert.SerializeObject(new
                {
                    msgId = "",
                    verb = "GET",
                    path = "/devices"
                }));

                Console.ReadKey();
            }

            Console.WriteLine("End");
            Console.ReadKey();
        }

        private static void MessageParse(WebsocketClient ws, ResponseMessage reply)
        {
            var replyJObject = JObject.Parse(reply.Text);

            if (replyJObject["path"].ToString() == "/devices")
            {
                Console.WriteLine($"Found {replyJObject["payload"]["devices"].Count()} devices");
                Console.WriteLine("---");

                foreach (var deviceJObject in replyJObject["payload"]["devices"])
                {
                    bool isWireless = false;
                    string pid = "";

                    foreach (var mode in deviceJObject["virtualDevice"]["modes"])
                    {
                        if (mode["wireless"].ToObject<bool>())
                        {
                            isWireless = true;
                            pid = mode["interfaces"].First["pid"].ToString().ToUpperInvariant();
                        }
                    }

                    if (isWireless)
                    {
                        ws.Send(JsonConvert.SerializeObject(new
                        {
                            msgId = $"FEATURES_{pid}",
                            verb = "GET",
                            path = $"/devices/{deviceJObject["id"]}/resources_available"
                        }));
                    }

                    Console.WriteLine(deviceJObject["extendedDisplayName"]);
                    Console.WriteLine($"Device is wireless: {isWireless}, PID: {pid}");
                    Console.WriteLine("");
                }
            }
            else if (replyJObject["msgId"].ToString().StartsWith("FEATURES_"))
            {
                var deviceId = replyJObject["path"].ToString().Split('/')[2];
                var pid = replyJObject["msgId"].ToString().Split('_').Last();

                bool hasBattery = replyJObject["payload"]["keys"].ToObject<List<string>>().Contains("battery");

                if (hasBattery)
                {
                    ws.Send(JsonConvert.SerializeObject(new
                    {
                        msgId = $"BATTERY_{pid}",
                        verb = "GET",
                        path = $"/devices/{deviceId}/resource",
                        payload = new
                        {
                            key = "battery"
                        }
                    }));
                }
                else
                {
                    Console.WriteLine($"[ERROR] Device ${pid}, does not have a battery api");
                }
            }
            else if (replyJObject["msgId"].ToString().StartsWith("BATTERY_"))
            {
                var deviceId = replyJObject["path"].ToString().Split('/')[2];
                var pid = replyJObject["msgId"].ToString().Split('_').Last();

                var cli = new WebClient();
                cli.DownloadFile(replyJObject["payload"]["url"].ToString(), $"./46D_{pid}.xml");

                Console.WriteLine($"Extracted power model for device {pid}");
            }
            else
            {
                //Console.WriteLine(reply);
            }
        }
    }
}
