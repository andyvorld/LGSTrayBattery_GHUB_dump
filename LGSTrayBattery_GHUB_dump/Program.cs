using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        private static readonly Dictionary<string, string> DeviceIdDictionary = new Dictionary<string, string>();

        private struct Device
        {
            public string pid;
            public string deviceId;
            public string fullName;
            public string deviceModel;
        }

        private struct DeviceResource
        {
            public string key;
            public string url;
        }

        private static bool deviceFound = false;
        private static int extractCount = 0;
        private static int deviceCount = -1;

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
                Console.WriteLine("");

                ws.Send(JsonConvert.SerializeObject(new
                {
                    msgId = "",
                    verb = "GET",
                    path = "/devices/list"
                }));

                while (!(deviceFound && extractCount < deviceCount))
                {
                }

                Thread.Sleep(500);

                Console.WriteLine("");
                Console.WriteLine("Found all .xml files");
                Console.WriteLine("Press any key to quit.");
                Console.ReadKey();
            }
        }

        private static void MessageParse(WebsocketClient ws, ResponseMessage reply)
        {
            var replyJObject = JObject.Parse(reply.Text);

            if (replyJObject["path"].ToString() == "/devices/list")
            {
                Console.WriteLine($"Found {replyJObject["payload"]["deviceInfos"].Count()} devices");
                Console.WriteLine("---");

                List<Device> devices = new List<Device>();

                foreach (var deviceJObject in replyJObject["payload"]["deviceInfos"])
                {
                    Console.WriteLine(deviceJObject.ToString());
                    bool isWireless = false;
                    string pid = "";

                    DeviceIdDictionary.Add(deviceJObject["id"].ToString(), deviceJObject["extendedDisplayName"].ToString());

                    if (deviceJObject["capabilities"]["hasBatteryStatus"].ToObject<Boolean>() == true)
                    {
                        isWireless = true;
                        pid = deviceJObject["pid"].ToString();
                    }

                    if (isWireless)
                    {
                        devices.Add(new Device()
                        {
                            pid = pid,
                            deviceId = deviceJObject["id"].ToString(),
                            fullName = deviceJObject["extendedDisplayName"].ToString(),
                            deviceModel = deviceJObject["deviceModel"].ToString()
                        });
                    }

                    Console.WriteLine(deviceJObject["extendedDisplayName"]);
                    if (isWireless)
                    {
                        Console.WriteLine($"Device is wireless, PID: {pid}");

                    }
                    else
                    {
                        Console.WriteLine("Device is not wireless, skipping");
                    }
                    Console.WriteLine("");
                }

                foreach (var device in devices)
                {
                    ws.Send(JsonConvert.SerializeObject(new
                    {
                        msgId = $"FEATURES_{device.deviceId}_{device.pid}",
                        verb = "GET",
                        path = $"/devices/resources",
                        payload = new {
                            device.deviceModel,
                            id = device.deviceId
                        }
                    }));
                }

                deviceFound = true;
                deviceCount = devices.Count;
            }
            else if (replyJObject["msgId"].ToString().StartsWith("FEATURES_"))
            {
                var deviceId = replyJObject["msgId"].ToString().Split('_')[1];
                var pid = replyJObject["msgId"].ToString().Split('_').Last();

                List<DeviceResource> deviceResources = replyJObject["payload"]["resources"].ToObject<List<DeviceResource>>();

                DeviceResource batteryResource = deviceResources.Where(resource => resource.key == "battery").First();
                if (batteryResource.url != null)
                {
                    UInt16.TryParse(pid, out ushort pidInt);
                    WebClient cli = new WebClient();
                    var fileName = $"./46D_{pidInt:X4}.xml";

                    cli.DownloadFile(batteryResource.url, fileName);
                    FixXMLContent(fileName);

                    Console.WriteLine($"Extracted power model for [{DeviceIdDictionary[deviceId]}]: 46D_{pidInt:X4}.xml");
                    extractCount++;
                }
            }
            else
            {
                // Console.WriteLine(reply);
            }
        }

        private static void FixXMLContent (string fileName)
        {
            string text = File.ReadAllText(fileName);
            text = text.Replace(" -- ", " - ");
            File.WriteAllText(fileName, text);
        }
    }
}
