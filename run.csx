using System.Net;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Text;

public class HpfRss : Rss
{
	public HpfRss( string id ) : base(id){}
	public override string GetUrl()
	{
		return $"https://www.hpfanficarchive.com/stories/viewstory.php?sid={Id}&index=1";
	}
	public override IEnumerable<string> GetItemNames(string data)
	{
		return GenerateChapterNames(RegexExtract("<span class=\"label\">Chapters: </span>\\s*(\\d+)\\s", data) ?? "-1");
	}
	
	public override string GetTitle(string data)
	{
		return RegexExtract("<title>([^<]*)</title>", data);
	}
}

public class FfnRss : Rss
{
	public FfnRss(string id) : base(id) { }

	public override string GetUrl()
	{
		return $"https://www.fanfiction.net/s/{Id}/1/";
	}

	public override IEnumerable<string> GetItemNames(string data)
	{
		return GenerateChapterNames(RegexExtract("Chapters: (\\d+)\\s", data) ?? "-1");
	}

	public override string GetTitle(string data)
	{
		return RegexExtract("<title>([^<]*)</title>", data);
	}
}

public class Ao3Rss : Rss
{
	public Ao3Rss(string id) : base(id) { }

	public override string GetUrl()
	{
		return $"http://archiveofourown.org/works/{Id}/chapters/";
	}

	public override IEnumerable<string> GetItemNames(string data)
	{
		if (data.Contains("<dd class=\"chapters\">"))
		{
			return GenerateChapterNames( RegexExtract("<dd class=\"chapters\">(\\d+)/[^<]*</dd>", data) );
		}
		return new []{ "Chapter 1" };
	}

	public override string GetTitle(string data)
	{
		if (data.Contains("<h2 class=\"title heading\">"))
		{
			return RegexExtract("<h2 class=\"title heading\">([^<]*)</h2>", data) ?? "No name";
		}
		else if (data.Contains("<h4 class=\"heading\">"))
		{
			return RegexExtract("<h4 class=\"heading\">\\s*<a href=\"/works/\\d*\">([^<]*)</a>", data) ?? "No name";
		}
		return "No name";
	}
}

public class Ao3AuthorRss : Rss
{
	public Ao3AuthorRss(string id) : base(id) { }

	public override string GetUrl()
	{
		return $"http://archiveofourown.org/users/{Id}/pseuds/{Id}/works";
	}

	public override IEnumerable<string> GetItemNames(string data)
	{
		return RegexExtractAll( "<h4 class=\"heading\">\\s*<a href=\"[^\"]+\">([^<]+)</a>", data );
	}

	public override string GetTitle(string data)
	{
		return $"{Id} updates";
	}
}

public abstract class Rss
{
	protected string Id { get; set; }

	protected Rss(string id)
	{
		Id = id;
	}

	public async Task<string> Build()
	{
		var url = GetUrl();
		var data = await Get(url);
		if( data == null )
			return null;
		var title = GetTitle(data);
		var items = GetItems(data);
		return MakeRss(title, url, items);
	}

	public async Task<string> Get(string url)
	{
		try
		{
		var request = WebRequest.Create(url);
		using (var response = await request.GetResponseAsync())
		using (var reader = new StreamReader(response.GetResponseStream()))
		{
			return reader.ReadToEnd();
		}
		}
		catch(WebException)
		{
			return null;
		}
	}

	public abstract string GetUrl();
	public abstract string GetTitle(string data);
	public abstract IEnumerable<string> GetItemNames(string data);

	protected virtual List<Item> GetItems(string data)
	{
		var list = new List<Item>();
		var itemnames = GetItemNames(data).ToArray();
		var count = 1;
        var countdown = itemnames.Length;

        foreach( var name in itemnames )
        {
			list.Add(new Item { Title = name, Id = countdown.ToString() });           
            count++;
            countdown--;
            if( count > 100 )
                break;
        }
		return list;
	}

    protected static IEnumerable<string> GenerateChapterNames(string count)
    {
		int c = -1;
		if (!Int32.TryParse(count, out c) || c < 0)
            return new string[0];

        var names = new string[c];
		var length = 0;
        for (var i = c; i >= 1 && length < 100; i--)
        {
            names[length] = $"Chapter {i}";
            length++;
        }
		return names;
    }

	protected static string RegexExtract(string regex, string data)
	{
		var r = new Regex(regex, RegexOptions.Multiline);
		var selected = r.Match(data);
		if (selected.Groups.Count >= 2)
		{
			return selected.Groups[1].Value.Trim();
		}
		return null;
	}

	protected static IEnumerable<string> RegexExtractAll(string regex, string data)
	{
		var r = new Regex(regex, RegexOptions.Multiline);
		MatchCollection selected = r.Matches(data);
        var values = new List<string>();
        foreach (Match match in selected)
        {
            GroupCollection groups = match.Groups;
            if( groups.Count >= 2)
                values.Add(groups[1].Value.Trim());
        }
		return values;
	}

	string MakeRss(string Title, string Url, List<Item> Items)
	{
		var sb = new StringBuilder();
		sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
		sb.AppendLine("<rss version=\"2.0\">\r\n<channel>");
		sb.AppendLine($"<title>{Title}</title>");
		sb.AppendLine($"<link>{Url}</link>");
		sb.AppendLine($"<description>{Title}</description>");
		sb.AppendLine("<language>en-us</language>\r\n");

		foreach (var item in Items)
		{
			sb.AppendLine($"<item>\r\n<title>{item.Title}</title>");
			sb.AppendLine($"<link>{Url}</link>");
			sb.AppendLine($"<guid>{Url}{item.Id}</guid>");
			//pubdate
			sb.AppendLine($"<description>{item.Title}</description>\r\n</item>\r\n");
		}
		sb.AppendLine("</channel>\r\n</rss>");
		return sb.ToString();
	}
}

public class Item
{
	public string Title { get; set; }
	public string Id { get; set; }
}

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
	var id = req.GetQueryNameValuePairs().FirstOrDefault(q => string.Compare(q.Key, "id", true) == 0).Value;
	if (string.IsNullOrEmpty(id))
	{
		return req.CreateResponse(HttpStatusCode.BadRequest, "id missing");
	}

	var type = req.GetQueryNameValuePairs().FirstOrDefault(q => string.Compare(q.Key, "type", true) == 0).Value;

	Rss rssobj = null;
	if(type == "ffn")
	{
		rssobj = new FfnRss(id);
	}
	else if( type == "hpf" )
	{
		rssobj = new HpfRss(id);
	}
    else if( type == "ao3a" )
    {
        rssobj = new Ao3AuthorRss(id);
    }
	else
	{
		rssobj = new Ao3Rss(id);
	}

	try
	{
		var rssstr = await rssobj.Build();

		if( rssstr == null )
			return req.CreateResponse(HttpStatusCode.NotFound);

		return req.CreateResponse(HttpStatusCode.OK, rssstr, "text/plain");
	}
	catch( Exception e )
	{
			return req.CreateResponse(HttpStatusCode.BadRequest, e.ToString(), "text/plain");
	}
}
