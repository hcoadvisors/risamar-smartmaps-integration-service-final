using HCO.DI.Entities;
using Newtonsoft.Json;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCO.DI.SmartMaps
{
    class SmartClient
    {
        public string code { get; set; }
        public string description { get; set; }
        public string descriptioncommercial { get; set; }
        public string x { get; set; }
        public string y { get; set; }
        public int timeservice { get; set; }
        public int statusid { get; set; }
        public string codecategory { get; set; }
        public string category { get; set; }
        public string zonezipcode { get; set; }
        public string zipcode { get; set; }
        public string debtamount { get; set; }
        public string saleamount { get; set; }
        public string DateModifiedRegister { get; set; }
        public string address { get; set; }

        public string addUpdClients(List<SmartClient> clients, Settings.ScenarioRow scenarioParams, string authorization)
        {
            var client = new RestClient(scenarioParams.RestAPI_Endpoint1);
            var request = new RestRequest($"/V1/AddUpdClient/", Method.POST);
            request.AddHeader("authorization", authorization);
            request.RequestFormat = DataFormat.Json;
            request.AddParameter("application/json", JsonConvert.SerializeObject(clients), ParameterType.RequestBody);
            var response = client.Execute(request);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception(response.StatusCode + " : " + response.Content);

            return response.Content;
        }
    }
}
