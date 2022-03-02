using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HCO.DI.SmartMaps
{
    class ConceptoNotaCredito
    {
        public int DocEntry { get; set; }
        public string U_HCO_CardCode { get; set; }
        public string U_HCO_CardName { get; set; }
        public string U_HCO_ItemCode { get; set; }
        public int U_HCO_BaseEntry { get; set; }
        public int U_HCO_BaseLine { get; set; }
        public double U_HCO_Quantity { get; set; }
        public string U_HCO_Concepto { get; set; }
        public string U_HCO_NumNC { get; set; }

        public List<ConceptoNotaCredito> getConcetosNC(string endpoint, string sessionId, out string nextLink, string queryOptions)
        {
            List<ConceptoNotaCredito> conceptos = new List<ConceptoNotaCredito>();

            var client = new RestClient(endpoint);
            var request = new RestRequest($"/sml.svc/NotasACrear?$" + queryOptions, Method.GET);
            request.AddCookie("B1SESSION", sessionId);
            var response = client.Execute(request);
            if (response.StatusCode != System.Net.HttpStatusCode.OK)
                throw new Exception(response.StatusCode + " : " + response.Content);

            JObject json = JObject.Parse(response.Content);
            JArray value = JArray.Parse(json["value"].ToString());
            foreach (JObject item in value)
            {
                ConceptoNotaCredito concepto = new ConceptoNotaCredito();
                concepto.DocEntry = int.Parse(item.GetValue("OCNCEntry").ToString());
                concepto.U_HCO_CardCode = item.GetValue("U_HCO_CardCode").ToString();
                concepto.U_HCO_CardName = item.GetValue("U_HCO_CardName").ToString();
                concepto.U_HCO_ItemCode = item.GetValue("U_HCO_ItemCode").ToString();
                concepto.U_HCO_BaseEntry = int.Parse(item.GetValue("U_HCO_BaseEntry").ToString());
                concepto.U_HCO_BaseLine = int.Parse(item.GetValue("U_HCO_BaseLine").ToString());
                concepto.U_HCO_Quantity = double.Parse(item.GetValue("U_HCO_Quantity").ToString());               
                concepto.U_HCO_Concepto = item.GetValue("U_HCO_Concepto").ToString();
                concepto.U_HCO_NumNC = item.GetValue("U_HCO_NumNC").ToString();
                conceptos.Add(concepto);
            }

            if (json.GetValue("@odata.nextLink") == null)
                nextLink = null;
            else
            {
                nextLink = json.GetValue("@odata.nextLink").ToString();
                nextLink = nextLink.Replace("NotasACrear?$", "");
            }

            return conceptos;
        }
    }
}
