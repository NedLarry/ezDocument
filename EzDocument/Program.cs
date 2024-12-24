using EzDocument.Application;
using EzDocument.Shared;
using EzDocument.Shared.Models;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

Console.WriteLine("Tinkering...");

Documentation documentation = new Documentation();
documentation.Endpoints = new List<DocumentationEndpoint>();

var endpoint = new DocumentationEndpoint();

var QueryParams = new Dictionary<string, string>();

var SuccessPayload = string.Empty;
var ErrorPayload = string.Empty;
var RequestPayload = string.Empty;

string loopExistIndicator = string.Empty;


Console.WriteLine("Kindly enter the Base url");
documentation.BaseUrl = Console.ReadLine()!;

Console.WriteLine("Kindly enter the version");
documentation.Version = Console.ReadLine()!;

Console.WriteLine("Kindly enter the description of the service");
documentation.Description = Console.ReadLine()!;


//add endpoints
while (!loopExistIndicator.Equals("0"))
{
    Endpoint:
    Console.WriteLine("ADD NEW ENDPOINT...\nEnter 01 to Continue.\nEnter 00 to quit");
    var desc = Console.ReadLine()!;

    if (string.IsNullOrWhiteSpace(desc)) goto Endpoint;

    if (desc.Equals("00")) break;

    Console.WriteLine("Kindly enter the description of the endpoint");
    endpoint.Description = Console.ReadLine()!;

    Console.WriteLine("Add endpoint path. " +
        "\nEnter in the format 'httpAction endpointPath' " +
        "\nGET /Users/Login");

    //loopExistIndicator = Console.ReadLine()!;

    string endpointpath = Console.ReadLine()!;

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
            if (string.IsNullOrWhiteSpace(addparamRes)) goto AddQueryParam;

            if (addparamRes.Equals("1"))
            {

                Console.WriteLine(@"Enter propertyname");
                var prop = Console.ReadLine()!;

                Console.WriteLine("Kindly enter a value");
                var propValue = Console.ReadLine()!;

                QueryParams.Add(prop, propValue);

                endpoint.Payload = QueryParams;

                goto AddQueryParam;
            }
        break;

        default:

            payloadProp:
            Console.WriteLine(@"Enter payload in this format: { property:  value }");
            var endpointPayload = Console.ReadLine()!;

            if (!RuleEnforcer.RuleMatchJson(endpointPayload)) goto payloadProp;

            RequestPayload = endpointPayload;

            endpoint.Payload = RequestPayload;
        break;
    }

    endpoint.HttpAction = action;

    successPayloadProp:
    Console.WriteLine("Add sucess response payload.\n enter in the format '{property:value}' \nEnter 2 to skip)");
    var successPayload = Console.ReadLine()!;

    if (string.IsNullOrWhiteSpace(successPayload)) goto successPayloadProp;


    if (successPayload.Equals("2")) goto ErrorPayloadProp;

    if (!RuleEnforcer.RuleMatchJson(successPayload)) goto successPayloadProp;

    SuccessPayload = successPayload;



    ErrorPayloadProp:
    Console.WriteLine("Add error response payload\n enter in the format '{property:value}'. \nEnter 2 to skip)");
    var errorPayload = Console.ReadLine()!;

    if (string.IsNullOrWhiteSpace(errorPayload)) goto ErrorPayloadProp;


    if (errorPayload.Equals("2")) goto Endpoint;

    if (!RuleEnforcer.RuleMatchJson(errorPayload)) goto ErrorPayloadProp;

    ErrorPayload = errorPayload;

    endpoint.SuccessResponsePayload = SuccessPayload;
    endpoint.ErrorResponsePayload = ErrorPayload;


    documentation.Endpoints.Add(endpoint);
}


Console.WriteLine($"Endpoint object: {JsonConvert.SerializeObject(documentation)}");