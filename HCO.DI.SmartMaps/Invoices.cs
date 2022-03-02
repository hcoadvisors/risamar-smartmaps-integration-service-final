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

namespace HCO.DI.SmartMaps
{
    [DisallowConcurrentExecution]
    public class Invoices : IJob
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
                if(new DateTime(lastExecution.Year, lastExecution.Month, lastExecution.Day) == DateTime.Today)
                    strTimeExecution = lastExecution.ToString("HH:mm:ss");

                string nextLink = null;
                string queryOptions = "filter=DocType eq 'dDocument_Items' and UpdateDate ge '" + strLastExecution + "' " +
                    "and UpdateTime ge '" + strTimeExecution + "'";

                do
                {
                    List<DocumentEx> invoicesList = slClient.Get<List<DocumentEx>>(SB1ServiceLayerSDK.SAPB1.BoObjectTypes.oInvoices, queryOptions, out nextLink, 20);
                    queryOptions = nextLink;

                    List<SmartInvoice> smartInvoices = new List<SmartInvoice>();

                    foreach (DocumentEx invoice in invoicesList)
                    {
                        SmartInvoice smartInvoice = new SmartInvoice();
                        if (invoice.CancelStatus.Equals("csCancellation"))
                        {
                            var invoiceCancelled = slClient.GetByKey<DocumentEx>(invoice.DocumentLines[0].BaseEntry, SB1ServiceLayerSDK.SAPB1.BoObjectTypes.oInvoices);
                            smartInvoice.invoicedate = ((DateTime)invoiceCancelled.DocDate).ToString("yyyy-MM-dd");
                            smartInvoice.invoicenumber = invoiceCancelled.DocNum.ToString();
                            smartInvoice.internumberinvoice = invoiceCancelled.DocEntry.ToString();
                            smartInvoice.codeclient = invoiceCancelled.CardCode;
                            smartInvoice.nameclient = invoiceCancelled.CardName;
                            smartInvoice.Warehouse = invoiceCancelled.DocumentLines[0].WarehouseCode;
                            smartInvoice.totalamount = invoiceCancelled.DocTotal.ToString();
                            smartInvoice.x = invoiceCancelled.U_HCO_Latitude;
                            smartInvoice.y = invoiceCancelled.U_HCO_Longitude;
                            smartInvoice.totalordervolume = invoiceCancelled.DocumentLines.Sum(l => l.Volume).ToString();
                            smartInvoice.totalweightorder = invoiceCancelled.DocumentLines.Sum(l => l.Weight1).ToString();
                            smartInvoice.suggesteddeliverydate = ((DateTime)invoiceCancelled.DocDueDate).ToString("yyyy-MM-dd");
                            smartInvoice.statusid = 0;
                            smartInvoice.productsdetail = new List<SmartProductDetail>();
                            for (int i = 0; i < invoice.DocumentLines.Count; i++)
                            {
                                SmartProductDetail smartProductDetail = new SmartProductDetail();
                                smartProductDetail.productCode = invoiceCancelled.DocumentLines[i].ItemCode;
                                smartProductDetail.product = invoiceCancelled.DocumentLines[i].ItemDescription;
                                smartProductDetail.quantity = Convert.ToInt32(invoiceCancelled.DocumentLines[i].Quantity);
                                smartProductDetail.linenumber = (int)invoiceCancelled.DocumentLines[i].LineNum;
                                smartProductDetail.statusid = 0;
                                smartInvoice.productsdetail.Add(smartProductDetail);
                            }
                        }
                        else if (invoice.Cancelled.Equals("tNO"))
                        {
                            smartInvoice.invoicedate = ((DateTime)invoice.DocDate).ToString("yyyy-MM-dd");
                            smartInvoice.invoicenumber = invoice.DocNum.ToString();
                            smartInvoice.internumberinvoice = invoice.DocEntry.ToString();
                            smartInvoice.codeclient = invoice.CardCode;
                            smartInvoice.nameclient = invoice.CardName;
                            smartInvoice.Warehouse = invoice.DocumentLines[0].WarehouseCode;
                            smartInvoice.totalamount = invoice.DocTotal.ToString();
                            smartInvoice.x = invoice.U_HCO_Latitude;
                            smartInvoice.y = invoice.U_HCO_Longitude;
                            smartInvoice.totalordervolume = invoice.DocumentLines.Sum(l => l.Volume).ToString();
                            smartInvoice.totalweightorder = invoice.DocumentLines.Sum(l => l.Weight1).ToString();
                            smartInvoice.suggesteddeliverydate = ((DateTime)invoice.DocDueDate).ToString("yyyy-MM-dd");
                            smartInvoice.statusid = 1;
                            smartInvoice.productsdetail = new List<SmartProductDetail>();
                            for (int i = 0; i < invoice.DocumentLines.Count; i++)
                            {
                                SmartProductDetail smartProductDetail = new SmartProductDetail();
                                smartProductDetail.productCode = invoice.DocumentLines[i].ItemCode;
                                smartProductDetail.product = invoice.DocumentLines[i].ItemDescription;
                                smartProductDetail.quantity = Convert.ToInt32(invoice.DocumentLines[i].Quantity);
                                smartProductDetail.linenumber = (int)invoice.DocumentLines[i].LineNum;
                                smartProductDetail.statusid = 1;
                                smartInvoice.productsdetail.Add(smartProductDetail);
                            }
                        }
                        smartInvoices.Add(smartInvoice);
                    }

                    request = JsonConvert.SerializeObject(smartInvoices);

                    try
                    {
                        if (smartInvoices.Count > 0)
                        {
                            Login login = new Login();
                            login.login(scenarioParams);

                            response = new SmartInvoice().addInvoce(smartInvoices, scenarioParams, login.Authentication);
                            message = "Facturas creadas correctamente";

                            if (writeInfoToLog)
                                Utility.WriteToLog(scenarioId, scenarioName, sourceName, destinationName, interfaceId, interfaceName, LogDA.Status.Successful, refKey, JsonConvert.SerializeObject(smartInvoices.Select(l => l.invoicenumber).ToList()), LogDA.ContenTypes.Json, request, response, message);

                            Utility.UpdateLastIntegrationDate(scenarioId, interfaceId, DateTime.Now);
                        }
                    }
                    catch (Exception ex)
                    {
                        message = ex.Message;
                        if (writeErroToLog)
                            Utility.WriteToLog(scenarioId, scenarioName, sourceName, destinationName, interfaceId, interfaceName, LogDA.Status.Failed, refKey, JsonConvert.SerializeObject(smartInvoices.Select(l => l.invoicenumber).ToList()), LogDA.ContenTypes.Json, request, message, message);
                    }
                }
                while (nextLink != null);

                
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
    }
}
