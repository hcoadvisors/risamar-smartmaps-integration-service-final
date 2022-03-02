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
    class SmartCreditNote
    {
        public string invoicenumber { get; set; }
        public string creditnotenumber { get; set; }
        public string intercreditnotenumber { get; set; }
        public string linenumberinvoicesap { get; set; }
        public string productcode { get; set; }
        public string product { get; set; }
        public int quantity { get; set; }
        public int statusid { get; set; }
        public string totalremainingvolume { get; set; }
        public string totalremainingweight { get; set; }
        public string totalamount { get; set; }

        public string updCreditNotes(List<SmartCreditNote> creditNotes, Settings.ScenarioRow scenarioParams, string authorization)
        {
            var client = new RestClient(scenarioParams.RestAPI_Endpoint1);
            var request = new RestRequest($"/V1/UpdCreditNotes/", Method.POST);
            request.AddHeader("authorization", authorization);
            request.RequestFormat = DataFormat.Json;
            request.AddParameter("application/json", JsonConvert.SerializeObject(creditNotes), ParameterType.RequestBody);
            var response = client.Execute(request);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception(response.StatusCode + " : " + response.Content);

            return response.Content;
        }
    }
}
