using EzDocument.Application;
using EzDocument.Shared;
using EzDocument.Shared.Models;
using Newtonsoft.Json;

Console.WriteLine("Tinkering...");

Documentation documentation = new Documentation();
documentation.Endpoints = new List<DocumentationEndpoint>();

//var endpoint = new DocumentationEndpoint();

var QueryParams = new Dictionary<string, string>();

var SuccessPayload = string.Empty;
var ErrorPayload = string.Empty;
var RequestPayload = string.Empty;

string loopExistIndicator = string.Empty;


Console.WriteLine("Kindly enter the Base url");
documentation.BaseUrl = Console.ReadLine()!;
Console.WriteLine();


Console.WriteLine("Kindly enter the version");
documentation.Version = Console.ReadLine()!;
Console.WriteLine();

Console.WriteLine("Kindly enter the description of the service");
documentation.Description = Console.ReadLine()!;
Console.WriteLine();


//add endpoints
while (!loopExistIndicator.Equals("0"))
{
    Endpoint:
    Console.WriteLine("ADD NEW ENDPOINT...\nEnter 01 to Continue.\nEnter 00 to quit");
    var desc = Console.ReadLine()!;
    Console.WriteLine();

    if (string.IsNullOrWhiteSpace(desc)) goto Endpoint;

    if (desc.Equals("00")) break;

    var endpoint = new DocumentationEndpoint();

    Console.WriteLine("Kindly enter the description of the endpoint");
    endpoint.Description = Console.ReadLine()!;
    Console.WriteLine();

    Console.WriteLine("Add endpoint path. " +
        "\nEnter in the format 'httpAction endpointPath' " +
        "\nGET /Users/Login");
    string endpointpath = Console.ReadLine()!;
    Console.WriteLine();

    //validate 
    if (!RuleEnforcer.RuleMatchEndpointPath(endpointpath))
    {
        goto Endpoint;
    }

    endpoint.EndpointUrl = endpointpath;

    var action = RuleEnforcer.EnpointActionMethod(endpointpath);

    switch (action.ToLower())
    {
        case "get":
            AddQueryParam:
            Console.WriteLine("Add new query Param? 1. yes, 2. No");
            var addparamRes = Console.ReadLine();
            Console.WriteLine();

            if (string.IsNullOrWhiteSpace(addparamRes)) goto AddQueryParam;

            if (addparamRes.Equals("1"))
            {

                Console.WriteLine(@"Enter propertyname");
                var prop = Console.ReadLine()!;

                Console.WriteLine("Kindly enter a value");
                var propValue = Console.ReadLine()!;

                QueryParams.Add(prop, propValue);

                endpoint.QueryParams = QueryParams;

                goto AddQueryParam;
            }
        break;

        case "post":

            payloadProp:
            Console.WriteLine(@"Enter payload in this format: { property:  value }");
            var endpointPayload = Console.ReadLine()!;
            Console.WriteLine();

            if (!RuleEnforcer.RuleMatchJson(endpointPayload)) goto payloadProp;

            RequestPayload = JsonConvert.SerializeObject(endpointPayload);

            endpoint.Payload = RequestPayload;
        break;
    }

    endpoint.HttpAction = action;

    successPayloadProp:
    Console.WriteLine("Add sucess response payload.\n enter in the format '{property:value}' \nEnter 2 to skip)");
    var successPayload = Console.ReadLine()!;
    Console.WriteLine();

    if (string.IsNullOrWhiteSpace(successPayload)) goto successPayloadProp;


    if (successPayload.Equals("2")) goto ErrorPayloadProp;

    if (!RuleEnforcer.RuleMatchJson(successPayload)) goto successPayloadProp;

    SuccessPayload = JsonConvert.SerializeObject(successPayload);



    ErrorPayloadProp:
    Console.WriteLine("Add error response payload\n enter in the format '{property:value}'. \nEnter 2 to skip)");
    var errorPayload = Console.ReadLine()!;
    Console.WriteLine();

    if (string.IsNullOrWhiteSpace(errorPayload)) goto ErrorPayloadProp;


    if (errorPayload.Equals("2")) goto Endpoint;

    if (!RuleEnforcer.RuleMatchJson(errorPayload)) goto ErrorPayloadProp;

    ErrorPayload = JsonConvert.SerializeObject(errorPayload);

    endpoint.SuccessResponsePayload = SuccessPayload;
    endpoint.ErrorResponsePayload = ErrorPayload;


    documentation.Endpoints.Add(endpoint);
}

Console.WriteLine($"Endpoint object: {JsonConvert.SerializeObject(documentation)}");
Console.WriteLine();


//TODO: Write object to file with proper formatting

using (var textWriter = File.CreateText($"_v{new Random().Next(8)}.txt"))
{
    textWriter.WriteLine(DocumentWriter.WriteHeader($"BASEURL: {documentation.BaseUrl}", 3));

    textWriter.WriteLine();
    textWriter.WriteLine();

    textWriter.WriteLine(DocumentWriter.WriteBold(DocumentWriter.WriteBlockQuote($"Description: {documentation.Description}")));

    textWriter.WriteLine();
    textWriter.WriteLine();


    textWriter.WriteLine(DocumentWriter.WriteBold(DocumentWriter.WriteBlockQuote($"Version: {documentation.Version}")));

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

}

//TODO: SaveFile to desktop with predetermined name.
