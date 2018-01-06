#r "Newtonsoft.Json"
#r "System.Web"

using System.Net;
using System.Web;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Text;
using Newtonsoft.Json;


public class ShareVilleRss : Rss
{
	public class Profile
	{
		public string backend_uri { get; set; }
	}
	
	public class Feed
	{
		public ShareVilleRss.FeedResult[] results { get; set; }
	}
	
	public class FeedResult
	{
		public ShareVilleRss.FeedEntry first { get; set; }
		public long id { get; set; }
		[JsonProperty("object")]
		public ShareVilleRss.Transaction transaction { get; set; }
		public string created_at { get; set; }
	}
	
	public class Transaction
	{
		public int side { get; set; }
		public int transaction_tag { get; set; }
		public long id { get; set; }
		public string price { get; set; }
		public TransactionData instrument { get; set; }
	}
	
	public class TransactionData
	{
		public string slug { get; set; }
		public string instrument_id { get; set; }
		public string instrument_group_type { get; set; }
		public long id { get; set; }
		public string name { get; set; }
		public string currency { get; set; }
	}
	
	public class FeedEntry
	{
		public long id { get; set; }
		public long group { get; set; }
		public string comment { get; set; }
		public FeedProfile profile { get; set; }
	}
	
	public class FeedProfile
	{
		public string name { get; set; }
	}
	
	public ShareVilleRss(string id, TraceWriter log) : base(log)
	{
		Id = id;
		Url = $"https://www.shareville.se/api/v1/profiles/{Id}";
	}
	
	static Dictionary<int,string> TagTypes = new Dictionary<int,string>{
		{0,""},
		{2,""},
		{3,"utan särskild tidshorisont"},
		{4,"för att ta hem vinsten"},
		{6,"för att lägga pengarna i kassan"}
	};
	
	static string MakeTitle(ShareVilleRss.FeedResult entry)
	{
		
		if( entry?.transaction?.instrument != null )
		{
			var act = entry?.transaction?.side == 1 ? "köpte" : "sålde";
			string tagtype = "";
			int? tag = entry?.transaction?.transaction_tag;
			if( tag != null )
			{
				if( TagTypes.ContainsKey(tag.Value) )
					tagtype = TagTypes[tag.Value];
				else
					tagtype = tag.Value.ToString();
			}
			
			return $"{entry?.first?.profile?.name} {act} {entry?.transaction?.instrument?.name} @ {entry?.transaction?.price} {entry?.transaction?.instrument?.currency} {tagtype}";
		}
		
		return $"Kommentar från {entry?.first?.profile?.name}";
	}

	protected override async Task<List<Item>> GetItems(string data)
	{
		var profile = JsonConvert.DeserializeObject<ShareVilleRss.Profile>( data );
		
		var feedjson = await Get( $"https://www.shareville.se{profile.backend_uri}/stream" );
		var feed = JsonConvert.DeserializeObject<ShareVilleRss.Feed>( feedjson );
		var list = new List<Item>();
		foreach( var result in feed.results )
		{
			var title = MakeTitle( result );
			string comment = "";
			if(result.first != null )
				comment = result.first.comment;
			list.Add( new Item {
				Title = title,
				Id = result.id.ToString(),
				Description = comment
			});
		}
		return list;
	}

	protected override string GetTitle(string data)
	{
		return $"{Id} ShareVille";
	}
}

public class HpfRss : Rss
{
	public HpfRss( string id, TraceWriter log ) : base(log)
	{
		Id = id;
		Url = $"http://www.hpfanficarchive.com/stories/viewstory.php?sid={Id}&index=1";
		TitleRegex = "<title>([^<]*)</title>";
		ItemsRegex = "<span class=\"label\">Chapters: </span>\\s*(\\d+)\\s";
	}
}

public class FfnRss : Rss
{
	public FfnRss( string id, TraceWriter log ) : base(log)
	{
		Id = id;
		Url = $"https://www.fanfiction.net/s/{Id}/1/";
		TitleRegex = "<title>([^<]*)</title>";
		ItemsRegex = "Chapters: (\\d+)\\s";
	}
}

public class Ao3Rss : Rss
{
	public Ao3Rss(string id, TraceWriter log) : base(log)
	{
		Id = id;
		Url = $"http://archiveofourown.org/works/{Id}/chapters/";
	}

	protected override IEnumerable<string> GetItemNames(string data)
	{
		if (data.Contains("<dd class=\"chapters\">"))
		{
			return GenerateChapterNames( RegexExtract("<dd class=\"chapters\">(\\d+)/[^<]*</dd>", data) );
		}
		return new []{ "Chapter 1" };
	}

	protected override string GetTitle(string data)
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
	public Ao3AuthorRss(string id, TraceWriter log) : base(log)
	{
		Id = id;
		Url = $"http://archiveofourown.org/users/{Id}/pseuds/{Id}/works";
	}

	protected override async Task<List<Item>> GetItems(string data)
	{
		var list = new List<Item>();
		var itemnames = RegexExtractAll( "<h4 class=\"heading\">\\s*<a href=\"[^\"]+\">([^<]+)</a>", data ).ToArray();
		var chapterdata = RegexExtractAll( "<dd class=\"chapters\">([^<]*)</dd>", data ).ToArray();
		for( var i = 0; i < itemnames.Length && i < chapterdata.Length && i < 100; i++ )
		{
			list.Add(new Item { Title = itemnames[i], Id = chapterdata[i] });           
		}
		return list;
	}

	protected override string GetTitle(string data)
	{
		return $"{Id} updates";
	}
}


public abstract class Rss
{
	protected string Id { get; set; }
	protected string Url { get; set; }
	protected string TitleRegex { get; set; }
	protected string ItemsRegex { get; set; }
	
	protected TraceWriter _log;

	protected Rss( TraceWriter log )
	{
		_log = log;
	}

	public async Task<string> Build()
	{
		var data = await Get( Url );
		if( data == null )
			return null;
		var title = GetTitle(data);
		var items = await GetItems(data);
		return MakeRss(title, items);
	}

	public async Task<string> Get( string url )
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
		catch(WebException we)
		{
			_log.Error(we.ToString());
			return null;
		}
	}

	protected virtual IEnumerable<string> GetItemNames(string data)
	{
		return GenerateChapterNames( RegexExtract(ItemsRegex, data) ?? "-1" );
	}

	protected virtual async Task<List<Item>> GetItems(string data)
	{
		return ExtractBasicChapterList( data );
	}
	
	protected virtual string GetTitle(string data)
	{
		return RegexExtract(TitleRegex, data);
	}

	protected List<Item> ExtractBasicChapterList(string data)
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

	string MakeRss(string feedtitle, List<Item> Items)
	{
		var titleencoded = System.Web.HttpUtility.HtmlEncode(feedtitle);
		var sb = new StringBuilder();
		sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
		sb.AppendLine("<rss version=\"2.0\">\r\n<channel>");
		sb.AppendLine($"<title>{titleencoded}</title>");
		sb.AppendLine($"<link>{Url}</link>");
		sb.AppendLine($"<description>{titleencoded}</description>");
		sb.AppendLine("<language>en-us</language>\r\n");

		foreach (var item in Items)
		{
			var description = System.Web.HttpUtility.HtmlEncode(item.Description ?? item.Title);
			var title = System.Web.HttpUtility.HtmlEncode( item.Title );

			sb.AppendLine($"<item>\r\n<title>{title}</title>");
			sb.AppendLine($"<link>{Url}</link>");
			sb.AppendLine($"<guid>{Url}{item.Id}</guid>");
			//pubdate?
			sb.AppendLine($"<description>{description}</description>\r\n</item>\r\n");
		}
		sb.AppendLine("</channel>\r\n</rss>");
		return sb.ToString();
	}
}

public class Item
{
	public string Title { get; set; }
	public string Description { get; set; }
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
		rssobj = new FfnRss(id,log);
	}
	else if( type == "hpf" )
	{
		rssobj = new HpfRss(id,log);
	}
	else if( type == "ao3a" )
	{
		rssobj = new Ao3AuthorRss(id,log);
	}
	else if( type == "sv" )
	{
		rssobj = new ShareVilleRss(id,log);
	}
	else
	{
		rssobj = new Ao3Rss(id,log);
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
