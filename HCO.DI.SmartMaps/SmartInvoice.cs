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
    class SmartInvoice
    {
        public string invoicedate { get; set; }
        public string invoicenumber { get; set; }
        public string internumberinvoice { get; set; }
        public string codeclient { get; set; }
        public string nameclient { get; set; }
        public string Warehouse { get; set; }
        public string totalordervolume { get; set; }
        public string totalweightorder { get; set; }
        public string totalamount { get; set; }
        public string suggesteddeliverydate { get; set; }
        public string x { get; set; }
        public string y { get; set; }
        public int statusid { get; set; }
        public List<SmartProductDetail> productsdetail { get; set; }

        public string addInvoce(List<SmartInvoice> invoices, Settings.ScenarioRow scenarioParams, string authorization)
        {
            var client = new RestClient(scenarioParams.RestAPI_Endpoint1);
            var request = new RestRequest($"/V1/AddInvoce/", Method.POST);
            request.AddHeader("authorization", authorization);
            request.RequestFormat = DataFormat.Json;
            request.AddParameter("application/json", JsonConvert.SerializeObject(invoices), ParameterType.RequestBody);
            var response = client.Execute(request);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception(response.StatusCode + " : " + response.Content);

            return response.Content;
        }
    }
}
