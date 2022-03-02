using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HCO.DI.IntegrationFramework.Contracts;
using HCO.DI.Common;
using HCO.DI.Entities;
using HCO.DI.DA;
using HCO.SB1ServiceLayerSDK;
using HCO.SB1ServiceLayerSDK.SAPB1;
using SAPbobsCOM;
using Quartz;
using Quartz.Impl;
using Newtonsoft.Json;

namespace HCO.DI.SmartMaps
{
    public class BusinessPartnerEx : BusinessPartner
    {
        public string U_HCO_Latitude { get; set; }
        public string U_HCO_Longitude { get; set; }
    }

    [DisallowConcurrentExecution]
    public class Customers : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {

            JobKey key = context.JobDetail.Key;
            JobDataMap dataMap = context.JobDetail.JobDataMap;

            Settings.ScenarioRow scenarioParams = (Settings.ScenarioRow)dataMap.Get("scenarioParams");
            Settings.InterfaceRow interfaceParams = (Settings.InterfaceRow)dataMap.Get("interfaceParams");

            Console.WriteLine("Interface: {0} - Ejecución: {1}", interfaceParams.Name, DateTime.Now);

            int scenarioId = scenarioParams.Id;
            string scenarioName = scenarioParams.ScenarioName;
            string sourceName = scenarioParams.Sb1_Database;
            string destinationName = "SmartMaps";
            int interfaceId = interfaceParams.Id;
            string interfaceName = interfaceParams.Name;
            string refKey = "CardCode";
            string refValue = string.Empty;
            string request = string.Empty;
            string response = string.Empty;
            string message = string.Empty;
            bool writeInfoToLog = scenarioParams.LogInfo;
            bool writeErroToLog = scenarioParams.LogError;

            //Cliente que facilita el consumo de SAP Business One Service Layer
            ServiceLayerClient slClient = null;

            try
            {
                //URL del Service Layer
                string endpoint = scenarioParams.Sb1_ServiceLayerEndpoint;

                //Se instancia el cliente
                slClient = new ServiceLayerClient(endpoint);

                //Login al Service Layer
                slClient.Login(scenarioParams.Sb1_Database,
                               scenarioParams.Sb1_User,
                               Utility.Decrypt(scenarioParams.Sb1_Password));


                DateTime lastExecution = Utility.GetLastIntegrationDate(scenarioId, interfaceId);
                string strLastExecution = lastExecution.ToString("yyyy-MM-dd");
                string strTimeExecution = "000000";
                if (new DateTime(lastExecution.Year, lastExecution.Month, lastExecution.Day) == DateTime.Today)
                    strTimeExecution = lastExecution.ToString("HHmmss");

                //Consulta socios de negocios por filtros, el Do/While trae todos los registros de 20 en 20
                string nextLink = null; //Se usa como parámetro para llevar el control del siguiente paginado
                string queryOptions = "filter=CardType eq 'cCustomer' and (((UpdateDate ge '" + strLastExecution + "') and (UpdateTime ge " + strTimeExecution + ")) " +
                    "or (CreateDate ge '" + strLastExecution + "' and CreateTime ge " + strTimeExecution + "))";
                do
                {
                    List<BusinessPartnerEx> businessPartnersList = slClient.Get<List<BusinessPartnerEx>>(SB1ServiceLayerSDK.SAPB1.BoObjectTypes.oBusinessPartners, queryOptions, out nextLink, 20);
                    queryOptions = nextLink;

                    List<SmartClient> smartClients = new List<SmartClient>();

                    foreach (BusinessPartnerEx businessPartner in businessPartnersList)
                    {
                        SmartClient smartClient = new SmartClient();
                        smartClient.code = businessPartner.CardCode;
                        smartClient.description = businessPartner.CardName;
                        smartClient.descriptioncommercial = businessPartner.CardForeignName;
                        smartClient.codecategory = businessPartner.GroupCode.ToString();
                        businessPartner.BusinessPartnerGroup = slClient.GetByKey<BusinessPartnerGroup>((int)businessPartner.GroupCode, SB1ServiceLayerSDK.SAPB1.BoObjectTypes.oBusinessPartnerGroups);
                        smartClient.category = businessPartner.BusinessPartnerGroup.Name;
                        smartClient.zonezipcode = businessPartner.City;
                        smartClient.zipcode = businessPartner.ZipCode;
                        smartClient.address = businessPartner.Address;
                        smartClient.x = businessPartner.U_HCO_Longitude;
                        smartClient.y = businessPartner.U_HCO_Latitude;
                        smartClient.timeservice = 1;
                        smartClient.statusid = businessPartner.Valid.Equals("tYES") ? 1 : 2;
                        smartClient.DateModifiedRegister = ((DateTime)businessPartner.UpdateDate).ToString("yyyy-MM-dd");

                        BalanceCliente balance = new BalanceCliente() { CardCode = businessPartner.CardCode };
                        balance.consultarBalance(endpoint, slClient._b1Session);

                        smartClient.debtamount = balance.Debito.ToString();
                        smartClient.saleamount = balance.Credito.ToString();

                        smartClients.Add(smartClient);
                    }

                    request = JsonConvert.SerializeObject(smartClients);

                    try
                    {
                        if (smartClients.Count > 0)
                        {
                            Login login = new Login();
                            login.login(scenarioParams);

                            response = new SmartClient().addUpdClients(smartClients, scenarioParams, login.Authentication);
                            
                            message = "Clientes procesados correctamente";

                            if (writeInfoToLog)
                                Utility.WriteToLog(scenarioId, scenarioName, sourceName, destinationName, interfaceId, interfaceName, LogDA.Status.Successful, refKey, JsonConvert.SerializeObject(smartClients.Select(l => l.code).ToList()), LogDA.ContenTypes.Json, request, response, message);

                            //Actualiza la última fecha de ejecución de la interface
                            Utility.UpdateLastIntegrationDate(scenarioId, interfaceId, DateTime.Now);
                        }
                    }
                    catch (Exception ex)
                    {
                        message = ex.Message;
                        if (writeErroToLog)
                            Utility.WriteToLog(scenarioId, scenarioName, sourceName, destinationName, interfaceId, interfaceName, LogDA.Status.Failed, refKey, JsonConvert.SerializeObject(smartClients.Select(l => l.code).ToList()), LogDA.ContenTypes.Json, request, message, message);
                    }
                }
                while (nextLink != null);              
            }
            catch (ServiceLayerException ex) //Si alguna operación falla se captura el error mediante la excepción ServiceLayerException
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
