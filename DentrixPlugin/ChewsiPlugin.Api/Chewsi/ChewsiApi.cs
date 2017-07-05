using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using ChewsiPlugin.Api.Common;
using Newtonsoft.Json;
using NLog;

namespace ChewsiPlugin.Api.Chewsi
{
    public class ChewsiApi : IChewsiApi
    {
        private const string PublicKey = "<RSAKeyValue><Modulus>wucbBr9ssgvKqQwuJ+NNhnYs2ZZSePy6gCOcMFIxOPKDrqS3cLnaAhlTUOpz/zXGsFq9riLvOAy6j7U3rvHfdg+bNc8TkKX0QysuDWe17+YENU0rdoTTMOBEFjCfXpWR4SxfxJPAaTZvBi/ZDrk5KzF0JUr4PyfTP+tMHwpJU97AN7cM8eEMWoyP1yigiKgZH3JI2jfE/zigLgJPJ09htAriVIx3eMDPXxtfnvO1o8PSZbqcJvNB8I31dWepsPkGXgRBjJNY7IM7FYfEVN5KUrqmequafOZi2bzXJFchry23+f0GrvfY4Noj2Zq3M9/sjNiAGvKopkoWcCeXh41wsQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private string _token;
        private bool _useProxy;
        private string _proxyAddress;
        private int _proxyPort;
        private string _proxyUserName;
        private string _proxyPassword;

        private const string Url = "http://chewsi-stage-txapi.azurewebsites.net";
                                   //"http://chewsi-dev-txapi.azurewebsites.net";
        private const string ValidateSubscriberAndProviderUri = "ValidateSubscriberAndProvider";
        private const string ProcessClaimUri = "ProcessClaim";
        private const string RegisterPluginUri = "RegisterPlugin";
        private const string StorePluginClientRowStatusUri = "StorePluginClientRowStatus";
        private const string RetrievePluginClientRowStatusesUri = "RetrievePluginClientRowStatuses?TIN={0}";
        private const string Request835DownloadsUri = "Request835Downloads";
        private const string RequestClaimProcessingStatusUri = "RequestClaimProcessingStatus";
        private const string DownloadFileUri = "DownloadFile";
        private const string UpdatePluginRegistrationUri = "UpdatePluginRegistration";
        private const string CalculatedOrthoPaymentsUri = "RetrieveCalculatedOrthoPayments";
        private const string GetOrthoPaymentPlanHistoryUri = "GetOrthoPaymentPlanHistory";

        /// <summary>
        /// The RetrievePluginClientRowStatuses() call should be made before 
        /// any Submits are done from your end, so that you can retrieve a list, 
        /// by TIN, of any plugin rows we have stored on our end.  
        /// This will allow you to loop through the returned list to match up on TIN, 
        /// PMSClaimNbr (PMS_ID) and PMSModifiedDate on your end to make sure that you 
        /// are not displaying any row in the plugin that we’re showing as Deleted (D) or already Submitted (S).
        /// If a user is submitting a claim that comes back from us as already submitted 
        /// then the plugin should pop up a message letting them know that the claim 
        /// was already submitted by another user and that it will be removed from the display.
        /// </summary>
        public List<PluginClientRowStatus> RetrievePluginClientRowStatuses(string tin)
        {
            var response = Post<List<PluginClientRowStatus>>(null, string.Format(RetrievePluginClientRowStatusesUri, tin));
            return response ?? new List<PluginClientRowStatus>();
        }

        /// <summary>
        /// The StorePluginiClientRowStatus() API should only be called when a user Deletes a claim 
        /// through the plugin. This will store it on our end as a deleted row for your later retrieval via the RetrieveClientRowStatuses() call.  
        /// </summary>
        public bool StorePluginClientRowStatus(PluginClientRowStatus request)
        {
            var response = Post<bool>(request, StorePluginClientRowStatusUri);
            return response;
        }
        
        /// <summary>
        /// Validates the subscriber and provider.
        /// </summary>
        /// <returns>Chewsi Provider ID</returns>
        public ValidateSubscriberAndProviderResponse ValidateSubscriberAndProvider(ProviderInformation provider, ProviderAddressInformation providerAddress, 
            SubscriberInformation subscriber)
        {
            return Post<ValidateSubscriberAndProviderResponse>(new ValidateSubscriberAndProviderRequest
            {
                TIN = provider.TIN,
                RenderingState = providerAddress.RenderingState,
                RenderingZip = providerAddress.RenderingZip,
                RenderingCity = providerAddress.RenderingCity,
                RenderingAddress1 = providerAddress.RenderingAddress1,
                RenderingAddress2 = providerAddress.RenderingAddress2,
                NPI = provider.NPI,
                SubscriberDOB = subscriber.SubscriberDateOfBirth?.ToString("d"),
                SubscriberFirstName = subscriber.SubscriberFirstName,
                SubscriberLastName = subscriber.SubscriberLastName,
                ChewsiID = subscriber.Id ?? ""
            },
                ValidateSubscriberAndProviderUri);
        }

        public void ProcessClaim(string id, ProviderInformation provider, SubscriberInformation subscriber, List<ClaimLine> procedures, DateTime pmsModifiedDate, double downPayment, int numberOfPayments)
        {
            Post<string>(new ProcessClaimRequest
            {
                PMS_ID = id,
                TIN = provider.TIN,
                OfficeNbr = provider.OfficeNbr,
                ClaimLines = procedures,
                NPI = provider.NPI,
                // PIN = ,
                ProviderID = provider.Id,
                SubscriberDOB = subscriber.SubscriberDateOfBirth?.ToString("d"),
                SubscriberFirstName = subscriber.SubscriberFirstName,
                PatientFirstName = subscriber.PatientFirstName,
                SubscriberID = subscriber.Id,
                SubscriberLastName = subscriber.SubscriberLastName,
                PatientLastName = subscriber.PatientLastName,
                PMSModifiedDate = pmsModifiedDate.FormatForApiRequest(),
                OrthoDownPayment = downPayment.ToString("F"),
                OrthoNumberOfPayments = numberOfPayments
            },
            ProcessClaimUri);
        }

        /// <summary>
        /// Registers the plugin. Returns machine Id.
        /// </summary>
        public string RegisterPlugin(RegisterPluginRequest request)
        {
            var response = Post<string>(request, RegisterPluginUri);
            return response;
        }

        public void UpdatePluginRegistration(UpdatePluginRegistrationRequest request)
        {
            Post<string>(request, UpdatePluginRegistrationUri);
        }

        public Request835DownloadsResponse Get835Downloads(Request835Downloads request)
        {
            return Post<Request835DownloadsResponse>(request, Request835DownloadsUri);
        }
        
        public ClaimProcessingStatusResponse GetClaimProcessingStatus(ClaimProcessingStatusRequest request)
        {
            return Post<ClaimProcessingStatusResponse>(request, RequestClaimProcessingStatusUri);
        }

        public Stream DownloadFile(DownoadFileRequest request)
        {
            return GetStream(request, DownloadFileUri, HttpMethod.Post);
        }

        public CalculatedOrthoPaymentsResponse GetCalculatedOrthoPayments(CalculatedOrthoPaymentsRequest request)
        {
            return Post<CalculatedOrthoPaymentsResponse>(request, CalculatedOrthoPaymentsUri);
        }

        public List<OrthoPaymentPlanHistoryResponse> GetOrthoPaymentPlanHistory(string tin)
        {
            return Post<List<OrthoPaymentPlanHistoryResponse>>(new OrthoPaymentPlanHistoryRequest
            {
                TIN = tin
            }, GetOrthoPaymentPlanHistoryUri);
        }

        public void Initialize(string token, bool useProxy, string proxyAddress, int proxyPort, string proxyUserName, string proxyPassword)
        {
            _token = token;
            _useProxy = useProxy;
            _proxyAddress = proxyAddress;
            _proxyPort = proxyPort;
            _proxyUserName = proxyUserName;
            _proxyPassword = proxyPassword;
        }
        
        private T Post<T>(object request, string uri)
        {
            var stream = GetStream(request, uri, HttpMethod.Post);
            if (stream != null)
            {
                var reader = new StreamReader(stream);
                return JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
            }
            return default(T);
        }

        private T Get<T>(object request, string uri) where T: class
        {
            var stream = GetStream(request, uri, HttpMethod.Get);
            if (stream != null)
            {
                var reader = new StreamReader(stream);
                return JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
            }
            return null;
        }

        private string GetSignature()
        {
            using (var rsa = new RSACryptoServiceProvider(2048))
            {
                rsa.PersistKeyInCsp = false;
                rsa.FromXmlString(PublicKey);
                byte[] encryptedBytes = rsa.Encrypt(Encoding.UTF8.GetBytes(_token + "_" + DateTime.UtcNow), false);
                return Convert.ToBase64String(encryptedBytes);
            }
        }

        private Stream GetStream(object request, string uri, HttpMethod method)
        {
            var url = new Uri(new Uri(Url, UriKind.Absolute), uri);
            var webRequest = WebRequest.Create(url) as HttpWebRequest;
            webRequest.Headers.Clear();
            webRequest.Headers.Add("x-chewsi-authid", GetSignature());
            webRequest.Accept = "application/json";
            webRequest.ContentType = "application/json";
            webRequest.Method = method.ToString().ToUpper();
            
            if (_useProxy)
            {
                var proxy = new WebProxy(_proxyAddress, _proxyPort)
                {
                    Credentials = new NetworkCredential(_proxyUserName, _proxyPassword)
                };
                webRequest.Proxy = proxy;
            }

            try
            {
                if (method == HttpMethod.Post)
                {
                    byte[] data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request));
                    webRequest.ContentLength = data.Length;
                    using (Stream post = webRequest.GetRequestStream())
                    {
                        post.Write(data, 0, data.Length);
                    }
                }
                var response = webRequest.GetResponse() as HttpWebResponse;
                if (response != null)
                {
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        return response.GetResponseStream();
                    }
                }
                Logger.Error("Empty response. Uri={0}", uri);
            }
            catch (WebException e)
            {
                if (e.Response != null)
                {
                    var response = (HttpWebResponse) e.Response;
                    if (response.StatusCode == HttpStatusCode.NoContent)
                    {
                        // 204
                        return null;
                    }
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        // 401
                        Logger.Error("Error 401. Uri={0}", uri);
                        // throw new InvalidOperationException("Wrong user name or password");
                        // Server may respond with code 401 in case if data is inconsistent; for example when TIN not found on the server
                        return null;
                    }
                    if (response.StatusCode == HttpStatusCode.NotFound)
                    {
                        // 404
                        Logger.Error("Error 404. Uri={0}", uri);
                    }
                    if (response.StatusCode == HttpStatusCode.InternalServerError)
                    {
                        // 500
                        Logger.Error("Error 500. Uri={0}", uri);
                    }
                    Logger.Error("Unsupported status code {0}. Uri={1}", response.StatusCode, uri);                
                }
                Logger.Error(e, "Unable to connect to Chewsi server.");
            }
            return null;
        }
    }
}
