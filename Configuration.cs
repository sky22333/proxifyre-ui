using System.Collections.Generic;
using Newtonsoft.Json;

namespace proxifyre_ui
{
    public class Configuration
    {
        public class Proxy
        {
            [JsonProperty("appNames")]
            public List<string> AppNames { get; set; } = new List<string>();

            [JsonProperty("socks5ProxyEndpoint")]
            public string Socks5ProxyEndpoint { get; set; }

            [JsonProperty("username")]
            public string Username { get; set; }

            [JsonProperty("password")]
            public string Password { get; set; }

            [JsonProperty("supportedProtocols")]
            public List<string> SupportedProtocols { get; set; } = new List<string> { "TCP", "UDP" };
        }

        [JsonProperty("logLevel")]
        public string LogLevel { get; set; } = "Info";

        [JsonProperty("proxies")]
        public List<Proxy> Proxies { get; set; } = new List<Proxy>();
    }
}
