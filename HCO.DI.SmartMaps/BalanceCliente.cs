using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCO.DI.SmartMaps
{
    class BalanceCliente
    {
        public string CardCode { get; set; }
        public double Debito { get; set; }
        public double Credito { get; set; }

        public void consultarBalance(string endpoint, string sessionId)
        {

            var client = new RestClient(endpoint);
            var request = new RestRequest($"/sml.svc/BalanceCliente?$filter=CardCode eq '" + this.CardCode + "'", Method.GET);
            request.AddCookie("B1SESSION", sessionId);
            var response = client.Execute(request);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception(response.StatusCode + " : " + response.Content);

            JObject json = JObject.Parse(response.Content);
            JArray value = JArray.Parse(json["value"].ToString());
            this.Debito = value.FirstOrDefault()?["Debito"]?.Value<string>() == null ? 0 : (double)value.FirstOrDefault()?["Debito"]?.Value<double>();
            this.Credito = value.FirstOrDefault()?["Credito"]?.Value<string>() == null ? 0 : (double)value.FirstOrDefault()?["Credito"]?.Value<double>();
        }
    }
}
