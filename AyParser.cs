using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AngleSharp;
using AngleSharp.Dom;

public class AyItem
{
    public readonly string link;
    public readonly string title;
    public readonly float price;
    public AyItem(string link, string title, float price)
    {
        this.link = link;
        this.title = title;
        this.price = price;
    }

    public static bool operator ==(AyItem left, AyItem right)
    {
        return left.link == right.link && left.price == right.price;
    }

    public static bool operator !=(AyItem left, AyItem right)
    {
        return !(left == right);
    }

    public override string ToString() => $"{title}: {price:F2}";

    public override bool Equals(object obj)
    {
        if (obj.GetType() == typeof(AyItem))
        {
            return this == (AyItem)obj;
        }
        return false;
    }

    public override int GetHashCode() => link.GetHashCode();
}

public class AyParser
{
    private readonly IConfiguration config;
    private IBrowsingContext context;
    private HttpClient httpClient;
    private List<AyItem> ayItems;
    public Uri url { get; }
    public float maxPrice { get; }
    public float minPrice { get; }
    public Queue<AyItem> newItems { get; }

    private bool isFirstRun = true;

    public AyParser(string url, float maxPrice, float minPrice)
    {
        config = Configuration.Default;
        context = BrowsingContext.New(config);
        httpClient = new HttpClient();
        ayItems = new List<AyItem>();
        newItems = new Queue<AyItem>();
        this.url = new Uri(url);
        this.maxPrice = maxPrice;
        this.minPrice = minPrice;
    }

    public async Task parseAsync()
    {
        await Task.Run(async () =>
        {
            //Source to be parsed
            var source = await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, url));

            //Create a virtual request to specify the document to load (here from our fixed string)
            IDocument document = await context.OpenAsync(async req => req.Content(await source.Content.ReadAsStringAsync()));

            //Do something with document like the following
            IElement? listElem = document.All.Where(m => m.LocalName == "ul" &&
            m.GetAttribute("id") == "lots-table").FirstOrDefault();
            IHtmlCollection<IElement> list = listElem.GetElementsByClassName("item-type-card__card");
            string? link, title;
            float price;
            AyItem item;
            List<AyItem> itemList = new List<AyItem>();
            foreach (IElement element in list)
            {
                link = element.GetElementsByClassName("item-type-card__link").FirstOrDefault().GetAttribute("href");
                title = element.GetElementsByClassName("item-type-card__title").FirstOrDefault().TextContent;
                //var a = element.GetElementsByClassName("c-hot");
                //var c = a.FirstOrDefault();
                //var b = c.GetElementsByTagName("strong").FirstOrDefault().TextContent;
                //price = Convert.ToSingle(a);
                price = Convert.ToSingle(element.GetElementsByClassName("c-hot").FirstOrDefault().GetElementsByTagName("strong").FirstOrDefault().TextContent, new CultureInfo("ru-RU", false));
                item = new AyItem(link, title, price);
                itemList.Add(item);
                if (isFirstRun) {
                    continue;
                }
                if (!ayItems.Contains(item) && item.price <= maxPrice && item.price >= minPrice)
                {
                    newItems.Enqueue(item);
                }
                // if (item.price <= maxPrice)
                //     Console.WriteLine(item);
            }
            isFirstRun = false;
            ayItems = itemList;
        });
    }
}