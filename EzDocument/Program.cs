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

var payloadProp = new Dictionary<string, string>();
var QueryParams = new Dictionary<string, string>();

var successPayload = string.Empty;
var ErrorPayload = string.Empty;

var RequestPayload = string.Empty;


dynamic forPayload = new { };
dynamic forSucess = new { };
dynamic forError = new { };

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

    AddEndpoint:
    Console.WriteLine("Add endpoint? \nEnter 1 Yes or 0 for No");
    loopExistIndicator = Console.ReadLine()!;

    if (string.IsNullOrWhiteSpace(loopExistIndicator)) goto AddEndpoint;

    if (loopExistIndicator.Equals("1"))
    {
        //int innerCounter = int.MinValue;

        Console.WriteLine("Kindly enter the endpoint path. \n" +
            "EG: GET /Users/Login ");
        endpoint.EndpointUrl = Console.ReadLine()!;

        Console.WriteLine("Kindly enter the description of the endpoint");
        endpoint.Description = Console.ReadLine()!;

        payloadProp:
        Console.WriteLine("Add endpoint payload property and values? \nEnter 1 POST(post requeest with form data) or 2 for GET(request with query params)");
        var payloadCounter = Console.ReadLine()!;

        if (string.IsNullOrWhiteSpace(payloadCounter)) goto payloadProp;


        if (payloadCounter.Equals("1"))
        {

            Console.WriteLine(@"Enter payload in this format: { property:  value }");
            var prop = Console.ReadLine()!;

            RequestPayload = prop;

            //Todo: After add query params, move to add success and error response payload


        }

        if (payloadCounter.Equals("2"))
        {
        //Todo: add query params

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
            }

            //Todo: After add query params, move to add success and error response payload

        }


        successPayloadProp:
        Console.WriteLine("Add sucess response payload properties and value. \nEnter 1 yes, 2 to skip)");
        var successCounter = Console.ReadLine()!;

        if (string.IsNullOrWhiteSpace(successCounter)) goto successPayloadProp;

        if (successCounter.Equals("1"))
        {

            Console.WriteLine("Kindly enter payload in this format {property: value}");
            var SPayload = Console.ReadLine()!;

            successPayload = SPayload;

        }


        ErrorPayloadProp:
        Console.WriteLine("Add error response payload properties and value. \nEnter 1 Yesm 2 to skip)");
        var errorCounter = Console.ReadLine()!;

        if (string.IsNullOrWhiteSpace(errorCounter)) goto ErrorPayloadProp;

        if (errorCounter.Equals("1"))
        {

            Console.WriteLine("Kindly enter payload in this format {property: value}");
            var EPayload = Console.ReadLine()!;

            ErrorPayload = EPayload;

        }
    }
}

endpoint.Payload = RequestPayload;
endpoint.SuccessResponsePayload = successPayload;
endpoint.ErrorResponsePayload = ErrorPayload;

Console.WriteLine($"Endpoint object: {JsonConvert.SerializeObject(endpoint)}");