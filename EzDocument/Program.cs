using EzDocument.Shared;
using EzDocument.Shared.Models;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;

Console.WriteLine("Tinkering...");

Documentation documentation = new Documentation();
documentation.Endpoints = new List<DocumentationEndpoint>();

var endpoint = new DocumentationEndpoint();

var payloadProp = new Dictionary<string, string>();
var successPayload = new Dictionary<string, string>();
var ErrorPayload = new Dictionary<string, string>();


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

        Console.WriteLine("Kindly enter the endpoint path");
        endpoint.EndpointUrl = Console.ReadLine()!;

        Console.WriteLine("Kindly enter the description of the endpoint");
        endpoint.Description = Console.ReadLine()!;

        payloadProp:
        Console.WriteLine("Add endpoint payload property and values? \nEnter 1 Yes(GET, POST) or 0 for No(request with no query params)");
        var payloadCounter = Console.ReadLine()!;

        if (string.IsNullOrWhiteSpace(payloadCounter)) goto payloadProp;


        if (payloadCounter.Equals("1"))
        {

            Console.WriteLine("Kindly enter property name");
            var prop = Console.ReadLine()!;

            Console.WriteLine("Kindly enter a value");
            var propValue = Console.ReadLine()!;

            payloadProp.Add(prop, propValue);

            goto payloadProp;

        }



        successPayloadProp:
        Console.WriteLine("Add sucess response payload properties and value. \nEnter 1 to add new property and value)");
        var successCounter = Console.ReadLine()!;

        if (string.IsNullOrWhiteSpace(successCounter)) goto successPayloadProp;

        if (successCounter.Equals("1"))
        {

            Console.WriteLine("Kindly enter property name");
            var prop = Console.ReadLine()!;

            Console.WriteLine("Kindly enter a value");
            var propValue = Console.ReadLine()!;

            successPayload.Add(prop, propValue);

            goto successPayloadProp;

        }


        ErrorPayloadProp:
        Console.WriteLine("Add error response payload properties and value. \nEnter 1 to add new property and value)");
        var errorCounter = Console.ReadLine()!;

        if (string.IsNullOrWhiteSpace(errorCounter)) goto ErrorPayloadProp;

        if (errorCounter.Equals("1"))
        {

            Console.WriteLine("Kindly enter property name");
            var prop = Console.ReadLine()!;

            Console.WriteLine("Kindly enter a value");
            var propValue = Console.ReadLine()!;

            ErrorPayload.Add(prop, propValue);

            goto ErrorPayloadProp;

        }
    }
}

foreach(var kvp in payloadProp)
{
    forPayload.key = kvp.Value;
}

foreach (var kvp in successPayload)
{
    forSucess.key = kvp.Value;
}

foreach (var kvp in ErrorPayload)
{
    forError.key = kvp.Value;
}

endpoint.Payload = forPayload;
endpoint.SuccessResponsePayload = forSucess;
endpoint.ErrorResponsePayload = forError;

Console.WriteLine("Check object");