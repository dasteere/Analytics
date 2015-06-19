using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Google.Apis.Analytics.v3;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace AnalyticsAggregateService
{
    public partial class AnalyticsAggregateService : ServiceBase
    {

        public AnalyticsAggregateService()
        {
            this.AutoLog = false;
            InitializeComponent();
            eventLog1 = new System.Diagnostics.EventLog();
            if (!System.Diagnostics.EventLog.SourceExists("MySource"))
            {
                System.Diagnostics.EventLog.CreateEventSource(
                    "MySource", "MyNewLog");
            }
            eventLog1.Source = "MySource";
            eventLog1.Log = "MyNewLog";
        }

        protected override void OnStart(string[] args)
        {
            System.Timers.Timer timer = new System.Timers.Timer();
            timer.Interval = 3600000; // 1 hour seconds
            timer.Elapsed += new System.Timers.ElapsedEventHandler(updateDay);
            timer.Start();
            System.Timers.Timer timer2 = new System.Timers.Timer();
            timer2.Interval = 30000; // 1 hour seconds
            timer2.Elapsed += new System.Timers.ElapsedEventHandler(updatePlaces);
            timer2.Start();
        }

        protected override void OnStop()
        {
        }

        private  void updatePlaces(object source, ElapsedEventArgs e)
        {
            var u = new Updater();
            u.getRtPlacesData(eventLog1);
        }
        private  void updateDay(object source, ElapsedEventArgs e)
        {
            var u = new Updater();
            /*u.deleteAll("AggregateWorld");
            u.deleteAll("AggregateAmerica");
            u.deleteAll("AggregateData");*/
            u.updateWorldDB(eventLog1);
        }
    }
    public class UserLoc
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public int Count { get; set; }
    }
    public class CityInfo
    {
        public string Name { get; set; }
        public int Count { get; set; }
    }
    public class Updater
    {
        private EventLog log;
        public void deleteAll(string db)
        {
            try
            {
                SqlConnection conn = new SqlConnection();
                conn.ConnectionString =
                    "Data Source=.\\SQLExpress;Initial Catalog=myDB;Integrated Security=True;Pooling=False";
                conn.Open();
                var cmd = new SqlCommand("TRUNCATE TABLE " + db, conn);
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                log.WriteEntry("ERROR in AggregateService delete: " + e);
            }
        }
        public void insert(List<UserLoc> locs, string db)
        {
            try
            {
                SqlConnection conn = new SqlConnection("Data Source=.\\SQLExpress;Initial Catalog=myDB;Integrated Security=True;Pooling=False");
                DataSet ds = ToDataSet(locs);
                var sourceData = ds.Tables[0];
                conn.Open();
                using (SqlBulkCopy bulkCopy =
                            new SqlBulkCopy(conn.ConnectionString))
                {
                    // column mappings
                    bulkCopy.ColumnMappings.Add("Latitude", "Latitude");
                    bulkCopy.ColumnMappings.Add("Longitude", "Longitude");
                    bulkCopy.ColumnMappings.Add("Count", "Count");
                    bulkCopy.DestinationTableName = db;
                    bulkCopy.WriteToServer(sourceData);
                }
            }
            catch (Exception e)
            {
                log.WriteEntry("ERROR in AggregateService insert: " + e);
            }
        }
        public static DataSet ToDataSet<T>(IList<T> list)
        {
            Type elementType = typeof(T);
            DataSet ds = new DataSet();
            DataTable t = new DataTable();
            ds.Tables.Add(t);
            //add a column to table for each public property on T
            foreach (var propInfo in elementType.GetProperties())
            {
                Type ColType = Nullable.GetUnderlyingType(propInfo.PropertyType) ?? propInfo.PropertyType;

                t.Columns.Add(propInfo.Name, ColType);
            }

            //go through each property on T and add each value to the table
            foreach (T item in list)
            {
                DataRow row = t.NewRow();

                foreach (var propInfo in elementType.GetProperties())
                {
                    row[propInfo.Name] = propInfo.GetValue(item, null) ?? DBNull.Value;
                }

                t.Rows.Add(row);
            }

            return ds;
        }
        public void insertPlaces(List<CityInfo> places, string db)
        {
            try
            {
                SqlConnection conn = new SqlConnection("Data Source=.\\SQLExpress;Initial Catalog=myDB;Integrated Security=True;Pooling=False");
                DataSet ds = ToDataSet(places);
                var sourceData = ds.Tables[0];
                conn.Open();
                using (SqlBulkCopy bulkCopy =
                            new SqlBulkCopy(conn.ConnectionString))
                {
                    // column mappings
                    bulkCopy.ColumnMappings.Add("Name", "Name");
                    bulkCopy.ColumnMappings.Add("Count", "Count");
                    bulkCopy.DestinationTableName = db;
                    bulkCopy.WriteToServer(sourceData);
                }
            }
            catch (Exception e)
            {
                log.WriteEntry("ERROR in AggregateService insertPlaces: " + e);
            }
            
            /*SqlTransaction transaction = conn.BeginTransaction("test");
            SqlCommand cmd = conn.CreateCommand();
            cmd.Connection = conn;
            cmd.Transaction = transaction;
            var id = 0;
            foreach (var u in places)
            {
                string saveStaff = "INSERT into " + db + " (Name,Count) " + " VALUES ('" + u.Name + "', '" + u.Count + "');";
                cmd.CommandText = saveStaff;
                if (u.Name.Contains("'"))
                {
                    saveStaff = "INSERT into " + db + " (Name,Count) " + " VALUES ('@Name" + id + "', '" + u.Count +
                                "');";
                    cmd.CommandText = saveStaff;
                    cmd.Parameters.AddWithValue("@Name" + id, u.Name);
                    id++;
                }
                //cmd.Parameters.AddWithValue("@Name", u.Name);
                cmd.ExecuteNonQuery();
            }
            transaction.Commit();*/
        }
        public async void getRtPlacesData(EventLog log)
        {
            this.log = log;
            try
            {
                UserCredential credential;
                using (var stream = new FileStream("C:\\Users\\asteere\\Documents\\Visual Studio 2015\\Projects\\Google Analytics 2.0\\AnalyticsAggregateService\\bin\\Debug\\client_secrets.json", FileMode.Open, FileAccess.Read))
                {
                    credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.Load(stream).Secrets,
                        new[] { AnalyticsService.Scope.Analytics },
                        "user", CancellationToken.None, new FileDataStore("Books.ListMyLibrary"));
                }
                // Create the service.
                var service = new AnalyticsService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "Analytics API Sample",
                });
                var request = service.Data.Realtime.Get("ga:" + 67281419, "rt:activeUsers");
                request.Dimensions = "rt:country,rt:city";
                request.Sort = "-rt:activeUsers";
                request.MaxResults = 10000;
                var feed = request.Execute();
                List<CityInfo> cities = new List<CityInfo>();
                List<CityInfo> countries = new List<CityInfo>();
                foreach (var list in feed.Rows)
                {
                    var row = (List<string>)list;
                    var country = new CityInfo()
                    {
                        Name = row[0],
                        Count = Int32.Parse(row[2])
                    };
                    var city = new CityInfo()
                    {
                        Name = row[1],
                        Count = Int32.Parse(row[2])
                    };
                    if (!country.Name.ToLower().Equals("zz") && !country.Name.Contains(("(Not")))
                        countries.Add(country);
                    if (!city.Name.Contains(("(Not")) && !city.Name.ToLower().Equals("zz"))
                        cities.Add(city);
                }
                countries = condensePlaces(countries);
                cities = condensePlaces(cities);
                deleteAll("Countries");
                deleteAll("Cities");
                insertPlaces(countries, "Countries");
                insertPlaces(cities, "Cities");
            }
            catch (Exception e)
            {
                log.WriteEntry("ERROR in AggregateService insertPlaces: " + e);
            }
        }

        public List<CityInfo> condensePlaces(List<CityInfo> places)
        {
            var store = new Dictionary<string, CityInfo>();
            foreach (var u in places)
            {
                if (store.ContainsKey(u.Name))
                {
                    store[u.Name] = new CityInfo
                    {
                        Name = u.Name,
                        Count = store[u.Name].Count + u.Count
                    };
                }
                else
                {
                    store.Add(u.Name, u);
                }
            }
            return store.Values.ToList();
        }
        public List<UserLoc> condenseLocs(List<UserLoc> places)
        {
            var store = new Dictionary<string, UserLoc>();
            foreach (var u in places)
            {
                if (store.ContainsKey(u.Latitude + "," + u.Longitude))
                {
                    store[u.Latitude + "," + u.Longitude] = new UserLoc
                    {
                        Latitude = u.Latitude,
                        Longitude = u.Longitude,
                        Count = store[u.Latitude + "," + u.Longitude].Count + u.Count
                    };
                }
                else
                {
                    store.Add(u.Latitude + "," + u.Longitude, u);
                }
            }
            return store.Values.ToList();
        }
        public IEnumerable<UserLoc> ReadFromDb(string db)
        {
            try
            {
                SqlConnection conn = new SqlConnection();
                conn.ConnectionString =
                    "Data Source=.\\SQLExpress;Initial Catalog=myDB;Integrated Security=True;Pooling=False";
                conn.Open();
                string str = "SELECT * FROM " + db;
                var cmd = new SqlCommand(str, conn);
                List<UserLoc> persons = new List<UserLoc>();
                var reader = cmd.ExecuteReader();
                if (reader.HasRows)
                {

                    // Read advances to the next row.
                    //Console.Write("Updating row \n");
                    while (reader.Read())
                    {
                        persons.Add(new UserLoc
                        {
                            Latitude = reader.GetDouble(reader.GetOrdinal("Latitude")),
                            Longitude = reader.GetDouble(reader.GetOrdinal("Longitude")),
                            Count = reader.GetInt32(reader.GetOrdinal("Count"))
                        });
                    }
                }
                return persons;
            }
            catch (Exception e)
            {
                log.WriteEntry("ERROR in AggregateService insertPlaces: " + e);
                return null;
            }  
        }
        public async void updateWorldDB(EventLog log)
        {
            UserCredential credential;
            using (var stream = new FileStream("C:\\Users\\asteere\\Documents\\Visual Studio 2015\\Projects\\Google Analytics 2.0\\AnalyticsAggregateService\\bin\\Debug\\client_secrets.json", FileMode.Open, FileAccess.Read))
            {
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    new[] { AnalyticsService.Scope.Analytics },
                    "user", CancellationToken.None, new FileDataStore("Books.ListMyLibrary"));
            }

            // Create the service.
            var service = new AnalyticsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Analytics API Sample",
            });
            DateTime t = DateTime.Now;
            var request2 = service.Data.Ga.Get("ga:" + 67281419, t.ToString("yyyy-MM-dd"), t.AddDays(1).ToString("yyyy-MM-dd"), "ga:users");
            request2.Dimensions = "ga:latitude,ga:longitude,ga:hour,ga:country";
            request2.Sort = "-ga:hour";
            request2.MaxResults = 10000;
            int hour = t.Hour - 2;
            if (hour < 0) hour += 24;
            string h = hour.ToString("00");
            request2.Filters = "ga:hour==" + h + ";ga:country!=United States;ga:country!=Canada;ga:country!=United Kingdom";
            var dayUsers = ReadFromDb("AggregateWorld").ToList();
            try
            {
                var gaFeed = request2.Execute();
                foreach (var list in gaFeed.Rows)
                {
                    var row = (List<string>)list;
                    var u = new UserLoc
                    {
                        Latitude = Double.Parse(row[0]),
                        Longitude = Double.Parse(row[1]),
                        Count = Int32.Parse(row[4])
                    };
                    if (u.Latitude != 0 && u.Longitude != 0)
                        dayUsers.Add(u);
                }
                dayUsers = condenseLocs(dayUsers);
                if (t.Hour == 2)
                {
                    deleteAll("AggregateWorld");
                }
                insert(dayUsers, "AggregateWorld");
                updateAmericaDB(dayUsers, log);
            }
            catch (Exception e)
            {
                log.WriteEntry(e.ToString());
                log.WriteEntry(e.ToString());
            }
        }
        public async void updateAmericaDB(List<UserLoc> worldUsers, EventLog log)
        {
            UserCredential credential;
            using (var stream = new FileStream("C:\\Users\\asteere\\Documents\\Visual Studio 2015\\Projects\\Google Analytics 2.0\\AnalyticsAggregateService\\bin\\Debug\\client_secrets.json", FileMode.Open, FileAccess.Read))
            {
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.Load(stream).Secrets,
                    new[] { AnalyticsService.Scope.Analytics },
                    "user", CancellationToken.None, new FileDataStore("Books.ListMyLibrary"));
            }

            // Create the service.
            var service = new AnalyticsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Analytics API Sample",
            });
            DateTime t = DateTime.Now;
            var request2 = service.Data.Ga.Get("ga:" + 67281419, t.ToString("yyyy-MM-dd"), t.AddDays(1).ToString("yyyy-MM-dd"), "ga:users");
            request2.Dimensions = "ga:latitude,ga:longitude,ga:hour,ga:country";
            request2.Sort = "-ga:hour";
            request2.MaxResults = 10000;
            int hour = t.Hour - 2;
            if (hour < 0) hour += 24;
            string h = hour.ToString("00");
            request2.Filters = "ga:hour==" + h + ";ga:country==United States,ga:country==Canada,ga:country==United Kingdom";
            var dayUsers = ReadFromDb("AggregateAmerica").ToList();
            try
            {
                var gaFeed = request2.Execute();
                foreach (var list in gaFeed.Rows)
                {
                    var row = (List<string>)list;
                    var u = new UserLoc
                    {
                        Latitude = Double.Parse(row[0]),
                        Longitude = Double.Parse(row[1]),
                        Count = Int32.Parse(row[4])
                    };
                    if (u.Latitude != 0 && u.Longitude != 0)
                        dayUsers.Add(u);
                }
                worldUsers.AddRange(dayUsers);
                worldUsers = condenseLocs(worldUsers);
                dayUsers = condenseLocs(dayUsers);
                if (t.Hour == 2)
                {
                    deleteAll("AggregateAmerica");
                    deleteAll("AggregateData");
                }
                insert(dayUsers, "AggregateAmerica");
                insert(worldUsers, "AggregateData");
            }
            catch (Exception e)
            {
                log.WriteEntry(e.ToString());
                return;
            }
        }
    }
}
