
using LamaHelper;
using PuppeteerSharp;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;


string filename = "workshops.txt";

if (File.Exists(filename))
{
    GenerateSheet();
    return;
}

using var browserFetcher = new BrowserFetcher();
await browserFetcher.DownloadAsync(BrowserFetcher.DefaultChromiumRevision);

var browser = await Puppeteer.LaunchAsync(new LaunchOptions
{
    Headless = false
});

var page = await browser.NewPageAsync();

await page.GoToAsync("https://lama.vcp.de/authentication/login");

await Task.Delay(5000);

Console.WriteLine("Enter username:");
string username = Console.ReadLine() ?? string.Empty;

Console.WriteLine("Enter password:");
string password = Console.ReadLine() ?? string.Empty;

await page.TypeAsync("#username", username.Trim());
await page.TypeAsync("#password", password.Trim());

await page.ClickAsync("#kc-login");

await Task.Delay(3000);

await page.GoToAsync("https://lama.vcp.de/bula/group/");

await page.WaitForNavigationAsync();
await Task.Delay(3000);

var url = page.Url!;

var groupId = url.Replace("https://lama.vcp.de/bula/group/", string.Empty).Replace("/info", string.Empty);

await page.GoToAsync($"https://lama.vcp.de/bula/group/{groupId}/program/attendees");

Console.WriteLine("Participant count:");

int participantCount = int.Parse(Console.ReadLine()!);

var particpantIds = new List<string>();

Console.WriteLine("scroll around to load all participants");

while (particpantIds.Count < participantCount)
{
    await Task.Delay(3000);

    var html = await page.GetContentAsync();

    var matches = Regex.Matches(html, "row-id=\\\"(.*?)\\\"");

    particpantIds.AddRange(matches.Select(m => m.Groups[1].Value));

    particpantIds = particpantIds.Distinct().ToList();

    Console.WriteLine($"{particpantIds.Count} Participants");
}

var data = new Dictionary<string, WorkshopInfo[]>();

foreach (var pid in particpantIds)
{
    await page.GoToAsync($"https://lama.vcp.de/bula/group/{groupId}/program/attendees/{pid}/sheet");

    await Task.Delay(15000);

    await page.ScreenshotAsync("4.png");

    var tnPageHtml = await page.GetContentAsync();

    string participant = Regex.Match(tnPageHtml, ">Teilnehmer\\*in (.*?)</nb-card-header>").Groups[1].Value;

    Console.WriteLine($"Check {pid} {participant}");

    var resultCount = await page.QuerySelectorAllAsync("button[size=\"large\"][status=\"primary\"]");

    var wiInfos = new List<WorkshopInfo>();

    for (int i = 0; i < resultCount.Length; i++)
    {
        var result = await page.QuerySelectorAllAsync("button[size=\"large\"][status=\"primary\"]");

        await result[i].ClickAsync();

        await Task.Delay(2000);

        var wi = GetWorkshopInfo(await page.GetContentAsync());
        wiInfos.Add(wi);

        await page.GoBackAsync();
        await Task.Delay(2000);
    }

    data.Add(participant, wiInfos.ToArray());

    //  await page.ScreenshotAsync("5.png");

    //<button _ngcontent-qqs-c200="" nbbutton="" ghost="" size="large" status="primary" tabindex="0" aria-disabled="false" class="appearance-ghost size-large shape-rectangle icon-start icon-end status-primary nb-transition"><nb-icon _ngcontent-qqs-c200="" icon="description" pack="material-icons" _nghost-qqs-c35="" class="material-icons description">description</nb-icon></button>
}

var str = JsonSerializer.Serialize(data);

File.WriteAllText(filename, str);

GenerateSheet();

Console.WriteLine("done");
Console.ReadLine();


WorkshopInfo GetWorkshopInfo(string html)
{
    var name = Regex.Match(html, "<nb-card-header(?:.*?)>(.*?)</nb-card-header>").Groups[1].Value.Replace("Workshop", string.Empty).Trim();
    var treffpunkt = Regex.Match(html, "Treffpunkt</div><div _ngcontent-(?:.*?)>(.*?)</div>").Groups[1].Value.Trim();
    var kurzbeschreibung = Regex.Match(html, "Kurzbeschreibung</div><div _ngcontent-(?:.*?)>(.*?)</div>").Groups[1].Value.Trim();
    var beschreibung = Regex.Match(html, "Beschreibung</div><div _ngcontent-(?:.*?)>(.*?)</div>").Groups[1].Value.Trim();
    var veranstalter = Regex.Match(html, "Veranstalter\\*in</div><div _ngcontent-(?:.*?)>(.*?)</div>").Groups[1].Value.Trim();

    var zeitslots = new[] { Regex.Match(html, "Zeitslots(?:.*?)<div(?:.*?)>(.*?)(?:</div>|<)").Groups[1].Value.Trim() };
    var zielgruppen = Regex.Match(html, "Zielgruppen(?:.*?)<ul(?:.*?)(?:<li(?:.*?)>(.*?)</li>)+(?:.*?)</ul>").Groups[1].Captures.Select(c => c.Value.Trim()).ToArray();

    if (zeitslots.All(z => string.IsNullOrEmpty(z)))
    {
        zeitslots = Regex.Match(html, "Zeitslots(?:.*?)<ul(?:.*?)(?:<li(?:.*?)>(.*?)</li>)+(?:.*?)</ul>").Groups[1].Captures.Select(c => c.Value.Trim()).ToArray();
        Console.WriteLine("Kein Zeitslot");
    }

    return new WorkshopInfo()
    {
        Name = name,
        Beschreibung = beschreibung,
        Kurzbeschreibung = kurzbeschreibung,
        Treffpunkt = treffpunkt,
        Zeitslots = zeitslots,
        Zielgruppen = zielgruppen,
        Veranstalter = veranstalter
    };
}


void GenerateSheet()
{
    var json = File.ReadAllText(filename);

    var wdata = JsonSerializer.Deserialize<Dictionary<string, WorkshopInfo[]>>(json)!;

    string template = File.ReadAllText("template.html");

    string html = "<html>";

    foreach (var kv in wdata)
    {
        var namesDone = new List<string>();

        foreach (var v in kv.Value)
        {
            if (namesDone.Contains(v.Name))
                continue;

            namesDone.Add(v.Name);

            string row = template;

            row = row.Replace("@Name", v.Name);
            row = row.Replace("@Zeitslots", string.Join(", ", v.Zeitslots.Select(s => ReplaceTimeslot(s))));
            row = row.Replace("@Zielgruppen", string.Join(", ", v.Zielgruppen));
            row = row.Replace("@Veranstalter", v.Veranstalter);
            row = row.Replace("@Kurzbeschreibung", v.Kurzbeschreibung);
            row = row.Replace("@Beschreibung", v.Beschreibung);
            row = row.Replace("@Treffpunkt", v.Treffpunkt);
            row = row.Replace("@Participant", kv.Key);

            html += row;
        }
    }

    html += "</html>";

    File.WriteAllText("workshops.html", html);
}

string ReplaceTimeslot(string pTimeSlot)
{
    switch (pTimeSlot)
    {
        case "Sonntag 31.07. Nachmittag S1":
            return "14:30 - 17:00 Sonntag 31.07. Nachmittag S1";

        case "Montag 01.08. Vormittag S2":
            return "10:00 - 12:00 Montag 01.08. Vormittag S2";

        case "Montag 01.08. Nachmittag S3":
            return "14:30 - 17:00 Montag 01.08. Nachmittag S3";

        case "Mittwoch 03.08. Vormittag S4":
            return "10:00 - 12:30 Mittwoch 03.08. Vormittag S4";

        case "Mittwoch 03.08. Nachmittag S5":
            return "14:30 - 17:00 Mittwoch 03.08. Nachmittag S5";

        case "Donnerstag 04.08. Vormittag S6":
            return "10:00 - 12:00 Donnerstag 04.08. Vormittag S6";

        case "Freitag 05.08. Vormittag S7":
            return "10:00 - 12:00 Freitag 05.08. Vormittag S7";

        case "Freitag 05.08. Nachmittag S8":
            return "14:30 - 17:00 Freitag 05.08. Nachmittag S8";

        case "Samstag 06.08. Vormittag S9":
            return "10:00 - 12:00 Samstag 06.08. Vormittag S9";
    }

    return pTimeSlot;
}