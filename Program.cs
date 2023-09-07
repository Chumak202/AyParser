using System;
using System.Linq;
using System.Net.Http;
using AngleSharp;
using AngleSharp.Dom;

class Program
{
    static async System.Threading.Tasks.Task Main()
    {
        // AyParser ayParser = new AyParser("http://ay.by/sch/?kwd=%D1%87%D0%B0%D1%81%D1%8B+%D0%BF%D0%BE%D0%BB%D0%B5%D1%82+%D1%81%D1%81%D1%81%D1%80&order=create&f_type=2", 40.0f);
        // await ayParser.parseAsync();
        // while (ayParser.newItems.Count > 0)
        // {
        //     AyItem ayItem = ayParser.newItems.Dequeue();
        //     Console.WriteLine(ayItem.ToString());
        // }
        // System.Console.WriteLine(ayParser.newItems.Count);
        AyBot bot = new("6407370155:AAECu_bcwPWxmy6TPD_KfWYBwHxXYUKotUE");
        await bot.Start();
        Console.ReadLine();
        bot.Stop();
    }
}
