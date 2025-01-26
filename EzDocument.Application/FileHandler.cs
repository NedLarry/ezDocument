using EzDocument.Shared;
using EzDocument.Shared.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EzDocument.Application
{
    public static class FileHandler
    {
        public static string WriteEndpointObjectToFileAndReturnFileName(Documentation documentation)
        {
            var drandValue = new Random().Next(8);
            var filename = $"_v{drandValue}.txt";

            using (var textWriter = File.CreateText(filename))
            {
                textWriter.WriteLine(DocumentWriter.WriteHeader($"BASEURL: {documentation.BaseUrl}", 3));

                textWriter.WriteLine();
                textWriter.WriteLine();

                textWriter.WriteLine(DocumentWriter.WriteBold($"Description: {documentation.Description}"));

                textWriter.WriteLine();
                textWriter.WriteLine();


                textWriter.WriteLine(DocumentWriter.WriteBold($"Version: {documentation.Version}"));

                textWriter.WriteLine();
                textWriter.WriteLine();

                //endpoints

                textWriter.WriteLine("-------ENDPOINTS-------");
                textWriter.WriteLine();


                foreach (var point in documentation.Endpoints)
                {
                    textWriter.WriteLine($"{DocumentWriter.WriteBold("EndpointPath")}: {DocumentWriter.WriteCode(point.EndpointUrl.ToUpper())}");

                    textWriter.WriteLine();
                    textWriter.WriteLine();

                    textWriter.WriteLine($"{DocumentWriter.WriteBold("EndpointDescription")}: {DocumentWriter.WriteCode(point.Description)}");

                    textWriter.WriteLine();
                    textWriter.WriteLine();

                    //payload

                    if (point.HttpAction.ToLower().Equals("get"))
                    {
                        textWriter.WriteLine($"{DocumentWriter.WriteBold("QueryParam")}: {DocumentWriter.WriteCode(JsonConvert.SerializeObject(point.QueryParams))}");


                        textWriter.WriteLine();
                        textWriter.WriteLine();
                    }
                    else
                    {
                        textWriter.WriteLine($"{DocumentWriter.WriteBold("Payload")}: {DocumentWriter.WriteCode(JsonConvert.SerializeObject(point.Payload))}");

                        textWriter.WriteLine();
                        textWriter.WriteLine();
                    }


                    textWriter.WriteLine($"{DocumentWriter.WriteBold("SuccessReponse")}: {DocumentWriter.WriteCode(JsonConvert.SerializeObject(point.SuccessResponsePayload))}");


                    textWriter.WriteLine();
                    textWriter.WriteLine();

                    textWriter.WriteLine($"{DocumentWriter.WriteBold("ErrorResponse")}: {DocumentWriter.WriteCode(JsonConvert.SerializeObject(point.ErrorResponsePayload))}");

                    textWriter.WriteLine();
                    textWriter.WriteLine();

                }


                return filename;

            }
        }

        public static void SaveMarkup(string content)
        {
            //var desktopPath = Environment.SpecialFolder.Desktop.ToString();
            var drandValue = new Random().Next(8);
            var filename = $"doc_v{drandValue}.txt";

            if (!Directory.Exists("Documentations"))
            {
                Directory.CreateDirectory("Documentations");
            }

            File.WriteAllText($"Documentations\\{filename}", content);

        }
    }
}
