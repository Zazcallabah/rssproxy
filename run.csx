using System.Net;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Text;
	
	public class FfnRss : Rss
	{
		public FfnRss(string id):base(id){}
		
		public override string GetUrl()
		{
			return $"https://www.fanfiction.net/s/{Id}/1/";
		}
		
		public override List<Item> GetItems(string data)
		{return null;
		}
		public override string GetTitle(string data)
		{
		return "";
		}
	}
	
	public class Ao3Rss : Rss
	{
		public Ao3Rss(string id):base(id){}
		
		public override string GetUrl()
		{
			return $"http://archiveofourown.org/works/{Id}/chapters/";
		}
		
		public override List<Item> GetItems(string data)
		{
			var list = new List<Item>();
			if (data.Contains("<dd class=\"chapters\">"))
			{
				var r = new Regex("<dd class=\"chapters\">(\\d+)/[^<]*</dd>", RegexOptions.Multiline);
				var selected = r.Match(data);
				if (selected.Groups.Count >= 2)
				{
					int c=-1;
					if (Int32.TryParse(selected.Groups[1].Value, out c))
					{
						var count=0;
						for (var i = c; i >= 1 && count<100; i--)
						{
							list.Add(new Item { Title = $"Chapter {i}", Id = i.ToString() });
							count++;
						}
					}
				}
			}
			return list;
		}
		
		public override string GetTitle(string data)
		{
			if (data.Contains("<h2 class=\"title heading\">"))
			{
				var r = new Regex("<h2 class=\"title heading\">([^<]*)</h2>", RegexOptions.Multiline);
				var selected = r.Match(data);
				if (selected.Groups.Count >= 2)
				{
					return selected.Groups[1].Value.Trim();
				}
			}
			else if (data.Contains("<h4 class=\"heading\">"))
			{
				var r = new Regex("<h4 class=\"heading\">\\s*<a href=\"/works/\\d*\">([^<]*)</a>", RegexOptions.Multiline);
				var selected = r.Match(data);
				if (selected.Groups.Count >= 2)
				{
					return selected.Groups[1].Value.Trim();
				}
			}
			return "No name";
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
			var title = GetTitle(data);
			var items = GetItems(data);
			return MakeRss(title,url,items);
		}
		
		public async Task<string> Get(string url)
        {
            var request = WebRequest.Create(url);
            using (var response = await request.GetResponseAsync())
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                return reader.ReadToEnd();
            }
        }
		
		public abstract string GetUrl();
		public abstract string GetTitle(string data);
		public abstract List<Item> GetItems(string data);
		
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
    if( string.IsNullOrEmpty(id))
    {
        return req.CreateResponse(HttpStatusCode.BadRequest,"id missing");
    }

    var type = req.GetQueryNameValuePairs().FirstOrDefault(q => string.Compare(q.Key, "type", true) == 0).Value;

	Rss rssobj = type == "ffn" ? (Rss)new FfnRss(id) : (Rss)new Ao3Rss(id);
	var rssstr = await rssobj.Build();

    return req.CreateResponse(HttpStatusCode.OK, rssstr,"text/plain");

}
