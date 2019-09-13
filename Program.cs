using System;
using System.Net.Http;
using Hl7.Fhir.Serialization;
using Hl7.Fhir.Model;
using FHIRTest;
using System.Configuration;
using System.Text;

namespace NRLBroker
{
    class Program
    {
        private static string NrlsLookupUrlTemplate = ConfigurationManager.AppSettings["NrlsLookupUrlTemplate"];
        private static string NrlsFromAsid = ConfigurationManager.AppSettings["NrlsFromAsid"];
        private static string NrlsToAsid = ConfigurationManager.AppSettings["NrlsToAsid"];
        private static string NrlsAuthorization = ConfigurationManager.AppSettings["NrlsAuthorization"];

        private static string DestinationAPIEndpoint = ConfigurationManager.AppSettings["DestinationAPIEndpoint"];

        static void Main(string[] args)
        {
            var targetPatientNumber = GetPatientId();
            var documentReference = GetDocumentReferenceFromNrl(targetPatientNumber);
            var document = GetDocument(documentReference);
            PostDocument(document);

            Console.ReadKey();
        }

        private static string GetPatientId()
        {
            Console.WriteLine("Please enter a patient identifier and press enter. Example patient = 9658218873.");
            return Console.ReadLine();
        }

        private static string GetDocumentReferenceFromNrl(object targetPatientNumber)
        {
            try
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Add("fromASID", NrlsFromAsid);
                client.DefaultRequestHeaders.Add("toASID", NrlsToAsid);
                client.DefaultRequestHeaders.Add("Authorization", NrlsAuthorization);

                var nrlsUrl = string.Format(NrlsLookupUrlTemplate, targetPatientNumber);
                var responseString = client.GetStringAsync(nrlsUrl).Result;

                FhirXmlParser parser = new FhirXmlParser();
                var bundle = parser.Parse<Bundle>(responseString);

                var documentReference = bundle.Entry[0].Resource as DocumentReference;
                var documentUrl = documentReference.Content[0].Attachment.Url;
                Console.WriteLine("The following document reference was found on NRL: {0}", documentUrl);
                Console.WriteLine("Please press any key to continue...");
                Console.ReadKey();
                return documentUrl;
            }
            catch (Exception exc)
            {
                HandleException(exc, "An error occured whilst getting the document reference from NRL");
            }

            return null;
        }

        private static Bundle GetDocument(string documentReferenceUrl)
        {
            try
            {
                var client = new HttpClient();
                client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/fhir+xml"));
                var responseString = client.GetStringAsync(documentReferenceUrl).Result;
                FhirXmlParser parser = new FhirXmlParser();
                var bundle = parser.Parse<Bundle>(responseString);
                Console.WriteLine("The following bundle was found {0}", bundle.ToString());
                Console.WriteLine("Please press any key to continue...");
                Console.ReadKey();
                return bundle;
            }
            catch (Exception exc)
            {
                HandleException(exc, "An error occured whilst getting the document from the API.");
            }

            return null;
        }

        private static void PostDocument(Bundle document)
        {
            try
            {
                var oauthClient = new OAuthFhirClient(DestinationAPIEndpoint);

                // Update the message header id, to avoid duplicates
                ((MessageHeader)document.Entry[0].Resource).Id = Guid.NewGuid().ToString();
                oauthClient.Transaction(document);
            }
            catch (Exception exc)
            {
                HandleException(exc, "An error occured whilst posting the document to the API.");
            }
        }

        private static void HandleException(Exception exc, string OperationSummary)
        {
            var errorMessage = string.Format("{0}.{1}{2}",
                OperationSummary, GetNestedErrors(exc), Environment.NewLine);

            Console.WriteLine(errorMessage);
            Console.ReadKey();
        }

        private static object GetNestedErrors(Exception exc)
        {
            var exceptions = new StringBuilder();
            exceptions.Append(exc.Message);

            while (exc.InnerException != null)
            {
                exc = exc.InnerException;
                exceptions.Append(exc.Message);
                exceptions.Append(Environment.NewLine);
            }

            return exceptions.ToString();
        }
    }
}
