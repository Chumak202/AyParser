using System;
using System.Linq;
using System.Net.Http;
using AngleSharp;
using AngleSharp.Dom;

class MyClass
{
    static async System.Threading.Tasks.Task Main()
    {
        //Use the default configuration for AngleSharp
        IConfiguration config = Configuration.Default;

        //Create a new context for evaluating webpages with the given config
        IBrowsingContext context = BrowsingContext.New(config);

        HttpClient httpClient = new HttpClient();

        //Source to be parsed
        var source = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, "http://ay.by/sch/?kwd=%D1%87%D0%B0%D1%81%D1%8B+%D0%BF%D0%BE%D0%BB%D0%B5%D1%82+%D1%81%D1%81%D1%81%D1%80&order=create&f_type=2"));

        //Create a virtual request to specify the document to load (here from our fixed string)
        IDocument document = await context.OpenAsync(async req => req.Content(await source.Content.ReadAsStringAsync()));

        //Do something with document like the following
        Console.WriteLine("Serializing the (original) document:");
        IElement listElem = document.All.Where(m => m.LocalName == "ul" &&
        m.GetAttribute("id") == "lots-table").FirstOrDefault();
        IHtmlCollection<IElement> list = listElem.GetElementsByClassName("item-type-card__card");
        foreach (IElement element in list) {
            Console.WriteLine(element.GetElementsByClassName("c-hot")[0].TextContent);
        }
        // Console.WriteLine(listElem.InnerHtml);
    }
}
