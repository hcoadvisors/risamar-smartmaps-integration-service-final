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
    class Login
    {
        public string Authentication { get; set; }

        public void login(Settings.ScenarioRow scenarioParams)
        {
            var client = new RestClient(scenarioParams.RestAPI_Endpoint1);
            var request = new RestRequest($"/V1/Login/", Method.POST);
            request.RequestFormat = DataFormat.Json;
            request.AddParameter("application/json", JsonConvert.SerializeObject(new { account = scenarioParams.RestAPI_Account }), ParameterType.RequestBody);
            var response = client.Execute(request);
            if(response.StatusCode == System.Net.HttpStatusCode.OK)
                this.Authentication = response.Headers.ToList().Find(h => h.Name.Equals("Authentication")).Value.ToString();
            else
                throw new Exception(response.StatusCode + " : " + response.Content);
        }
    }
}
