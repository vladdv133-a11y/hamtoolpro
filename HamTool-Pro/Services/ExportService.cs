using System.IO;
using System.Text;using System.Text.Json;using HamTool.Models;
namespace HamTool.Services;
public static class ExportService {
 public static void SaveLiveResults(IEnumerable<LiveSearchResult> source,string path,string format){
 var rows=source.ToList();
 switch(format.ToUpperInvariant()){
 case "JSON":File.WriteAllText(path,JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));break;
 case "CSV":var sb=new StringBuilder("Source,Title,Summary,Url\r\n");foreach(var r in rows)sb.AppendLine(string.Join(",",new[]{r.Source,r.Title,r.Summary,r.Url}.Select(Csv)));File.WriteAllText(path,sb.ToString(),new UTF8Encoding(true));break;
 case "HTML":var h=new StringBuilder("<!doctype html><html><head><meta charset=\"utf-8\"><title>HamTool live results</title><style>body{font:15px Segoe UI,Arial;margin:2rem;max-width:1100px}article{border-bottom:1px solid #ddd;padding:1rem 0}a{overflow-wrap:anywhere}</style></head><body><h1>HamTool live results</h1><p>Public-source results; independently verify all findings.</p>");foreach(var r in rows)h.Append("<article><b>").Append(System.Net.WebUtility.HtmlEncode(r.Source)).Append("</b><h2>").Append(System.Net.WebUtility.HtmlEncode(r.Title)).Append("</h2><p>").Append(System.Net.WebUtility.HtmlEncode(r.Summary)).Append("</p><a href=\"").Append(System.Net.WebUtility.HtmlEncode(r.Url)).Append("\">").Append(System.Net.WebUtility.HtmlEncode(r.Url)).Append("</a></article>");h.Append("</body></html>");File.WriteAllText(path,h.ToString(),new UTF8Encoding(true));break;
 default:throw new ArgumentException("Unsupported live-results export format.");
 }
 }
 public static void Save(IEnumerable<ResearchRecord> source,string path,string format){var rows=source.ToList();switch(format.ToUpperInvariant()){
 case "JSON":File.WriteAllText(path,JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));break;
 case "CSV":var sb=new StringBuilder("Id,CaseName,QueryType,Query,CreatedAt,Notes\r\n");foreach(var r in rows)sb.AppendLine(string.Join(",",new[]{r.Id.ToString(),r.CaseName,r.QueryType,r.Query,r.CreatedAt,r.Notes}.Select(Csv)));File.WriteAllText(path,sb.ToString(),new UTF8Encoding(true));break;
 case "TOML":var t=new StringBuilder("# HamTool research export\n");foreach(var r in rows){t.AppendLine("\n[[records]]");t.AppendLine($"id = {r.Id}");t.AppendLine($"case = {Quote(r.CaseName)}");t.AppendLine($"type = {Quote(r.QueryType)}");t.AppendLine($"query = {Quote(r.Query)}");t.AppendLine($"created_at = {Quote(r.CreatedAt)}");t.AppendLine($"notes = {Quote(r.Notes)}");}File.WriteAllText(path,t.ToString(),new UTF8Encoding(false));break;
 default:throw new ArgumentException("Unsupported export format.");}}
 static string Csv(string s)
 {
  // Spreadsheet programs may execute cells beginning with formula characters.
  if (!string.IsNullOrEmpty(s) && "=+-@\t\r".Contains(s[0])) s = "'" + s;
  return "\"" + s.Replace("\"", "\"\"") + "\"";
 }
 static string Quote(string s)=>"\""+s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n")+"\"";
}
