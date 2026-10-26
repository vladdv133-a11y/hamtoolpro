using System.IO;
using HamTool.Models; using Microsoft.Data.Sqlite; using System.Text.Json;
namespace HamTool.Services;
public sealed class StorageService {
 readonly string _cs; readonly string _dir; readonly string _settingsPath;
 public StorageService(){_dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"HamTool");Directory.CreateDirectory(_dir);_cs=new SqliteConnectionStringBuilder{DataSource=Path.Combine(_dir,"hamtool.db")}.ToString();_settingsPath=Path.Combine(_dir,"settings.json");using var db=new SqliteConnection(_cs);db.Open();using var c=db.CreateCommand();c.CommandText="CREATE TABLE IF NOT EXISTS Research(Id INTEGER PRIMARY KEY AUTOINCREMENT,CaseName TEXT NOT NULL,QueryType TEXT NOT NULL,Query TEXT NOT NULL,CreatedAt TEXT NOT NULL,Notes TEXT NOT NULL DEFAULT '')";c.ExecuteNonQuery();}
 public string DataDirectory=>_dir;
 public void Add(string caseName,string type,string query,string notes=""){using var db=new SqliteConnection(_cs);db.Open();using var c=db.CreateCommand();c.CommandText="INSERT INTO Research(CaseName,QueryType,Query,CreatedAt,Notes) VALUES($c,$t,$q,$d,$n)";c.Parameters.AddWithValue("$c",caseName);c.Parameters.AddWithValue("$t",type);c.Parameters.AddWithValue("$q",query);c.Parameters.AddWithValue("$d",DateTimeOffset.Now.ToString("O"));c.Parameters.AddWithValue("$n",notes);c.ExecuteNonQuery();}
 public List<ResearchRecord> GetAll(string filter=""){var l=new List<ResearchRecord>();using var db=new SqliteConnection(_cs);db.Open();using var c=db.CreateCommand();c.CommandText="SELECT Id,CaseName,QueryType,Query,CreatedAt,Notes FROM Research WHERE ($f='' OR CaseName LIKE $like OR Query LIKE $like OR Notes LIKE $like) ORDER BY Id DESC LIMIT 5000";c.Parameters.AddWithValue("$f",filter);c.Parameters.AddWithValue("$like","%"+filter+"%");using var r=c.ExecuteReader();while(r.Read())l.Add(new(){Id=r.GetInt64(0),CaseName=r.GetString(1),QueryType=r.GetString(2),Query=r.GetString(3),CreatedAt=r.GetString(4),Notes=r.GetString(5)});return l;}
 public void Delete(long id){using var db=new SqliteConnection(_cs);db.Open();using var c=db.CreateCommand();c.CommandText="DELETE FROM Research WHERE Id=$id";c.Parameters.AddWithValue("$id",id);c.ExecuteNonQuery();}
 public void Clear(){using var db=new SqliteConnection(_cs);db.Open();using var c=db.CreateCommand();c.CommandText="DELETE FROM Research";c.ExecuteNonQuery();}
 public AppSettings LoadSettings(){try{return File.Exists(_settingsPath)?JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath))??new():new();}catch{return new();}}
 public void SaveSettings(AppSettings s)=>File.WriteAllText(_settingsPath,JsonSerializer.Serialize(s,new JsonSerializerOptions{WriteIndented=true}));
}
