using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HCO.DI.IntegrationFramework.Contracts;
using HCO.DI.Common;
using HCO.DI.Entities;
using HCO.DI.DA;
using HCO.DI.SB1DIAPIHelper;
using SAPbobsCOM;
using Quartz;
using Quartz.Impl;
using HCO.SB1ServiceLayerSDK;
using HCO.SB1ServiceLayerSDK.SAPB1;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HCO.DI.SmartMaps
{
    
    [DisallowConcurrentExecution]
    public class CreditNotes : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            JobKey key = context.JobDetail.Key;
            JobDataMap dataMap = context.JobDetail.JobDataMap;

            Settings.ScenarioRow scenarioParams = (Settings.ScenarioRow)dataMap.Get("scenarioParams");
            Settings.InterfaceRow interfaceParams = (Settings.InterfaceRow)dataMap.Get("interfaceParams");

            int scenarioId = scenarioParams.Id;
            string scenarioName = scenarioParams.ScenarioName;
            string sourceName = scenarioParams.Sb1_Database;
            string destinationName = "SmartMaps";
            int interfaceId = interfaceParams.Id;
            string interfaceName = interfaceParams.Name;
            string refKey = "DocEntry";
            string refValue = string.Empty;
            string request = string.Empty;
            string response = string.Empty;
            string message = string.Empty;
            bool writeInfoToLog = scenarioParams.LogInfo;
            bool writeErroToLog = scenarioParams.LogError;

            ServiceLayerClient slClient = null;

            try
            {
                string endpoint = scenarioParams.Sb1_ServiceLayerEndpoint;

                slClient = new ServiceLayerClient(endpoint);

                slClient.Login(scenarioParams.Sb1_Database,
                               scenarioParams.Sb1_User,
                               Utility.Decrypt(scenarioParams.Sb1_Password));

                DateTime lastExecution = Utility.GetLastIntegrationDate(scenarioId, interfaceId);
                string strLastExecution = lastExecution.ToString("yyyy-MM-dd");
                string strTimeExecution = "00:00:00";
                if (new DateTime(lastExecution.Year, lastExecution.Month, lastExecution.Day) == DateTime.Today)
                    strTimeExecution = lastExecution.ToString("HH:mm:ss");

                string nextLink = null;
                string queryOptions = "filter=DocType eq 'dDocument_Items' and (U_HCO_NCSmartMaps eq 'N' or U_HCO_NCSmartMaps eq NULL) and UpdateDate ge '" + strLastExecution + "' " +
                    "and UpdateTime ge '" + strTimeExecution + "'";

                do
                {
                    List<Document> creditList = slClient.Get<List<Document>>(SB1ServiceLayerSDK.SAPB1.BoObjectTypes.oCreditNotes, queryOptions, out nextLink, 20);
                    queryOptions = nextLink;

                    List<SmartCreditNote> smartCreditNotes = new List<SmartCreditNote>();

                    foreach (Document creditNote in creditList)
                    {
                        for (int i = 0; i < creditNote.DocumentLines.Count; i++)
                        {
                            SmartCreditNote smartCreditNote = new SmartCreditNote();
                            smartCreditNote.invoicenumber = creditNote.DocumentLines[i].BaseEntry.ToString();
                            smartCreditNote.creditnotenumber = creditNote.DocNum.ToString();
                            smartCreditNote.intercreditnotenumber = creditNote.DocEntry.ToString();
                            smartCreditNote.linenumberinvoicesap = creditNote.DocumentLines[i].BaseLine.ToString();
                            smartCreditNote.productcode = creditNote.DocumentLines[i].ItemCode;
                            smartCreditNote.product = creditNote.DocumentLines[i].ItemDescription;
                            smartCreditNote.quantity = Convert.ToInt32(creditNote.DocumentLines[i].Quantity);
                            smartCreditNote.totalremainingvolume = creditNote.DocumentLines[i].Volume.ToString();
                            smartCreditNote.totalremainingweight = creditNote.DocumentLines[i].Weight1.ToString();
                            smartCreditNote.totalamount = creditNote.DocumentLines[i].LineTotal.ToString();
                            smartCreditNote.statusid = 1;
                            smartCreditNotes.Add(smartCreditNote);
                        }
                    }

                    request = JsonConvert.SerializeObject(smartCreditNotes);

                    try
                    {
                        if (smartCreditNotes.Count > 0)
                        {
                            Login login = new Login();
                            login.login(scenarioParams);

                            response = new SmartCreditNote().updCreditNotes(smartCreditNotes, scenarioParams, login.Authentication);
                            message = "Notas crédito creadas correctamente";

                            if (writeInfoToLog)
                                Utility.WriteToLog(scenarioId, scenarioName, sourceName, destinationName, interfaceId, interfaceName, LogDA.Status.Successful, refKey, JsonConvert.SerializeObject(smartCreditNotes.Select(l => l.creditnotenumber).ToList()), LogDA.ContenTypes.Json, request, response, message);

                            Utility.UpdateLastIntegrationDate(scenarioId, interfaceId, DateTime.Now);
                        }
                    }
                    catch (Exception ex)
                    {
                        message = ex.Message;
                        if (writeErroToLog)
                            Utility.WriteToLog(scenarioId, scenarioName, sourceName, destinationName, interfaceId, interfaceName, LogDA.Status.Failed, refKey, JsonConvert.SerializeObject(smartCreditNotes.Select(l => l.creditnotenumber).ToList()), LogDA.ContenTypes.Json, request, message, message);
                    }
                }
                while (nextLink != null);

                crearNotasCreditoSmartMaps(ref slClient, endpoint, interfaceParams, scenarioParams);

            }
            catch (ServiceLayerException ex)
            {
                message = string.Format("Error Code: {0} - Message: {1}", ex.ErrorCode, ex.Message);
                if (writeErroToLog)
                    Utility.WriteToLog(scenarioId, scenarioName, sourceName, destinationName, interfaceId, interfaceName, LogDA.Status.Failed, String.Empty, String.Empty, LogDA.ContenTypes.Json, String.Empty, String.Empty, message);
            }
            catch (Exception ex)
            {
                message = string.Format("Error Code: {0} - Message: {1}", ex.HResult, ex.Message);
                if (writeErroToLog)
                    Utility.WriteToLog(scenarioId, scenarioName, sourceName, destinationName, interfaceId, interfaceName, LogDA.Status.Failed, String.Empty, String.Empty, LogDA.ContenTypes.Json, String.Empty, String.Empty, message);
            }
            finally
            {
                if (slClient != null)
                    slClient.Logout();  //Cierra la sesión
            }

            return Task.CompletedTask;
        }

        private void crearNotasCreditoSmartMaps(ref ServiceLayerClient slClient, string endpoint, Settings.InterfaceRow interfaceParams, Settings.ScenarioRow scenarioParams)
        {

            int scenarioId = scenarioParams.Id;
            string scenarioName = scenarioParams.ScenarioName;
            string sourceName = scenarioParams.Sb1_Database;
            string destinationName = "SmartMaps";
            int interfaceId = interfaceParams.Id;
            string interfaceName = interfaceParams.Name;
            string refKey = "U_HCO_NumNC";
            string refValue = string.Empty;
            string request = string.Empty;
            string response = string.Empty;
            string message = string.Empty;
            bool writeInfoToLog = scenarioParams.LogInfo;
            bool writeErroToLog = scenarioParams.LogError;

           
            string nextLink = null;
            string queryOptions = "filter = RINEntry eq null";

            ConceptoNotaCredito conceptoNC = new ConceptoNotaCredito();

            do
            {

                var conceptos = conceptoNC.getConcetosNC(endpoint, slClient._b1Session, out nextLink, queryOptions);
                queryOptions = nextLink;

                foreach (ConceptoNotaCredito concepto in conceptos)
                {
                    if (new string[] { "1", "2", "3", "4", "5", "6", "9", "10", "15", "16", "18" }.Contains(concepto.U_HCO_Concepto))
                    {
                        try
                        {
                            DocumentEx document = new DocumentEx();
                            document.CardCode = concepto.U_HCO_CardCode;
                            document.U_HCO_NCSmartMaps = "Y";
                            document.U_HCO_NumNCSM = concepto.U_HCO_NumNC;
                            document.DocumentLines = new List<DocumentLine>();
                            document.DocumentLines.Add(
                                new DocumentLine()
                                {
                                    ItemCode = concepto.U_HCO_ItemCode,
                                    BaseEntry = concepto.U_HCO_BaseEntry,
                                    BaseLine = concepto.U_HCO_BaseLine,
                                    Quantity = concepto.U_HCO_Quantity,
                                    BaseType = 13
                                }
                            );


                            request = JsonConvert.SerializeObject(concepto);

                            response = slClient.AddAndGetOnlyKey<DocumentEx>(document, SB1ServiceLayerSDK.SAPB1.BoObjectTypes.oCreditNotes);
                            message = "Nota crédito SmartMaps creadas correctamente";

                            if (writeInfoToLog)
                                Utility.WriteToLog(scenarioId, scenarioName, sourceName, destinationName, interfaceId, interfaceName, LogDA.Status.Successful, refKey, concepto.U_HCO_NumNC, LogDA.ContenTypes.Json, request, response, message);

                        }
                        catch (ServiceLayerException ex)
                        {
                            message = string.Format("Error Code: {0} - Message: {1}", ex.ErrorCode, ex.Message);
                            if (writeErroToLog)
                                Utility.WriteToLog(scenarioId, scenarioName, sourceName, destinationName, interfaceId, interfaceName, LogDA.Status.Failed, String.Empty, String.Empty, LogDA.ContenTypes.Json, String.Empty, String.Empty, message);
                        }
                        catch (Exception ex)
                        {
                            message = ex.Message;
                            if (writeErroToLog)
                                Utility.WriteToLog(scenarioId, scenarioName, sourceName, destinationName, interfaceId, interfaceName, LogDA.Status.Failed, refKey, concepto.U_HCO_NumNC, LogDA.ContenTypes.Json, request, message, message);
                        }
                    }
                }          
            }
            while (nextLink != null);
        }
    }
}
